using System.Diagnostics;

namespace Backend;

/// <summary>
/// Plans the pickup-and-delivery route. Exact part: Dijkstra distances between the points that
/// matter. Heuristic part: cheapest insertion + relocation + single-stop moves, with random restarts.
/// </summary>
public partial class RoutePlanner
{
    public const int DefaultRestarts = 200;
    public const double DefaultMaxSeconds = 10;
    public const int DefaultSeed = 12345;

    /// <summary>Internal compact stop: location index, pickup/delivery flag and package index.</summary>
    private readonly record struct Node(int Loc, bool IsPickup, int Pkg);

    private const int StartIndex = 0;

    private readonly Graph<string> _city;
    private readonly IReadOnlyList<Package> _packages;
    private readonly string _start;
    private readonly List<string> _points = new();                 // index 0 = start position
    private readonly Dictionary<string, int> _index = new();
    private readonly int[,] _dist;                                  // _dist[x, y] = shortest metres x -> y
    private readonly Dictionary<string, Dictionary<string, string>> _previous = new();
    private readonly int[] _pickupLoc;
    private readonly int[] _deliveryLoc;

    public RoutePlanner(Graph<string> city, IReadOnlyList<Package> packages, string start)
    {
        _city = city;
        _packages = packages;
        _start = start;

        AddPoint(start);
        foreach (Package p in packages)
        {
            AddPoint(p.Pickup);
            AddPoint(p.Delivery);
        }

        _pickupLoc = packages.Select(p => _index[p.Pickup]).ToArray();
        _deliveryLoc = packages.Select(p => _index[p.Delivery]).ToArray();

        // One Dijkstra per relevant point; the rest of the algorithm only uses this table.
        int n = _points.Count;
        _dist = new int[n, n];
        var errors = new List<string>();
        for (int x = 0; x < n; x++)
        {
            var (distance, previous) = ShortestPaths.Dijkstra(_city, _points[x]);
            _previous[_points[x]] = previous;
            for (int y = 0; y < n; y++)
            {
                if (distance.TryGetValue(_points[y], out int d))
                {
                    _dist[x, y] = d;
                }
                else if (errors.Count < 10)
                {
                    errors.Add(x == StartIndex
                        ? $"El punto {_points[y]} no es alcanzable desde la posición inicial {_start}."
                        : $"Desde {_points[x]} no se puede llegar a {_points[y]} respetando el sentido de las calles.");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new DataValidationException(errors);
        }
    }

    /// <summary>Number of random restarts of the improvement phase.</summary>
    public int Restarts { get; init; } = DefaultRestarts;

    /// <summary>Safety time limit (seconds) for the whole improvement phase.</summary>
    public double MaxSeconds { get; init; } = DefaultMaxSeconds;

    /// <summary>Fixed seed so the result is reproducible.</summary>
    public int Seed { get; init; } = DefaultSeed;

    /// <summary>Maximum number of packages carried at the same time (int.MaxValue = no limit).</summary>
    public int Capacity { get; init; } = int.MaxValue;

    /// <summary>If true, the route must end back at the start position.</summary>
    public bool ReturnToStart { get; init; }

    private void AddPoint(string location)
    {
        if (!_index.ContainsKey(location))
        {
            _index[location] = _points.Count;
            _points.Add(location);
        }
    }

    // ------------------------------------------------------------------ public API

    /// <summary>The best route the heuristic finds.</summary>
    public List<Stop> Plan()
    {
        var random = new Random(Seed);
        var stopwatch = Stopwatch.StartNew();
        List<Node>? best = null;
        int bestCost = int.MaxValue;

        for (int attempt = 0; attempt < Restarts; attempt++)
        {
            if (attempt > 0 && stopwatch.Elapsed.TotalSeconds > MaxSeconds)
            {
                break;
            }

            int[] order = Enumerable.Range(0, _packages.Count).ToArray();
            Shuffle(order, random);

            List<Node> route = new();
            foreach (int pkg in order)
            {
                route = InsertPackage(route, pkg);
            }

            route = LocalSearch(route, random);
            int cost = Cost(route);
            if (cost < bestCost)
            {
                bestCost = cost;
                best = route;
            }
        }

        return ToStops(best!);
    }

    /// <summary>Nearest-neighbour baseline (section 8.3): always valid, usually long.</summary>
    public List<Stop> PlanGreedy()
    {
        var route = new List<Node>();
        var picked = new bool[_packages.Count];
        var delivered = new bool[_packages.Count];
        int current = StartIndex;
        int load = 0;

        for (int step = 0; step < 2 * _packages.Count; step++)
        {
            Node? bestNode = null;
            int bestDistance = int.MaxValue;
            for (int pkg = 0; pkg < _packages.Count; pkg++)
            {
                if (delivered[pkg])
                {
                    continue;
                }

                bool isPickup = !picked[pkg];
                if (isPickup && load >= Capacity)
                {
                    continue;   // the bike is full: only deliveries are allowed
                }

                int target = isPickup ? _pickupLoc[pkg] : _deliveryLoc[pkg];
                if (_dist[current, target] < bestDistance)
                {
                    bestDistance = _dist[current, target];
                    bestNode = new Node(target, isPickup, pkg);
                }
            }

            Node chosen = bestNode!.Value;
            if (chosen.IsPickup)
            {
                picked[chosen.Pkg] = true;
                load++;
            }
            else
            {
                delivered[chosen.Pkg] = true;
                load--;
            }

            route.Add(chosen);
            current = chosen.Loc;
        }

        return ToStops(route);
    }

    /// <summary>Total metres of the route (start -> first stop -> ... -> last stop [-> start]).</summary>
    public double TotalDistance(List<Stop> route)
    {
        long total = 0;
        int current = StartIndex;
        foreach (Stop stop in route)
        {
            int next = _index[stop.Location];
            total += _dist[current, next];
            current = next;
        }

        if (ReturnToStart)
        {
            total += _dist[current, StartIndex];
        }

        return total;
    }

    /// <summary>Shortest distance in metres between two relevant points.</summary>
    public int Distance(string from, string to) => _dist[_index[from], _index[to]];

    /// <summary>
    /// True if every package is picked up exactly once and delivered exactly once, after its pickup,
    /// each task happens at the package's own location and the capacity is never exceeded.
    /// </summary>
    public bool IsValid(List<Stop> route)
    {
        if (route.Count != 2 * _packages.Count)
        {
            return false;
        }

        var packageById = _packages.ToDictionary(p => p.Id);
        var picked = new HashSet<int>();
        var delivered = new HashSet<int>();
        int load = 0;

        foreach (Stop stop in route)
        {
            if (!packageById.TryGetValue(stop.PackageId, out Package package))
            {
                return false;
            }

            if (stop.Kind == StopKind.Pickup)
            {
                if (stop.Location != package.Pickup || !picked.Add(stop.PackageId))
                {
                    return false;
                }

                load++;
                if (load > Capacity)
                {
                    return false;
                }
            }
            else
            {
                if (stop.Location != package.Delivery || !picked.Contains(stop.PackageId) || !delivered.Add(stop.PackageId))
                {
                    return false;
                }

                load--;
            }
        }

        return picked.Count == _packages.Count && delivered.Count == _packages.Count;
    }

    /// <summary>Streets to follow from one relevant point to another (both inclusive).</summary>
    public List<string> PathBetween(string from, string to) =>
        ShortestPaths.ReconstructPath(_previous[from], from, to);

    // ------------------------------------------------------------------ construction

    private int Cost(List<Node> route)
    {
        int total = 0;
        int current = StartIndex;
        foreach (Node node in route)
        {
            total += _dist[current, node.Loc];
            current = node.Loc;
        }

        if (ReturnToStart)
        {
            total += _dist[current, StartIndex];
        }

        return total;
    }

    /// <summary>Inserts the package's pickup and delivery at the cheapest valid positions.</summary>
    private List<Node> InsertPackage(List<Node> route, int pkg)
    {
        var (_, a, b) = BestInsertion(route, pkg);
        var result = new List<Node>(route.Count + 2);
        result.AddRange(route.Take(a));
        result.Add(new Node(_pickupLoc[pkg], true, pkg));
        result.AddRange(route.Skip(a).Take(b - a));
        result.Add(new Node(_deliveryLoc[pkg], false, pkg));
        result.AddRange(route.Skip(b));
        return result;
    }

    /// <summary>
    /// Tries every pair (a, b) with a &lt;= b: pickup goes before route[a], delivery before route[b]
    /// (original indices), so pickup is always before delivery. Cost increase is computed in O(1).
    /// Positions that would exceed the capacity are skipped.
    /// </summary>
    private (int Delta, int A, int B) BestInsertion(List<Node> route, int pkg)
    {
        int n = route.Count;
        int p = _pickupLoc[pkg];
        int q = _deliveryLoc[pkg];

        // Sequence position k: 0 = start, 1..n = route[k - 1], n + 1 = start again (only if returning).
        int seqEnd = n + (ReturnToStart ? 1 : 0);
        int At(int k) => (k == 0 || k > n) ? StartIndex : route[k - 1].Loc;

        // loadAfter[k] = packages carried after sequence position k.
        int[] loadAfter = LoadProfile(route);

        int bestDelta = int.MaxValue;
        int bestA = 0;
        int bestB = 0;

        for (int a = 0; a <= n; a++)
        {
            int u = At(a);
            bool hasNext = a < seqEnd;
            int v = hasNext ? At(a + 1) : -1;

            int pickupDelta = _dist[u, p] + (hasNext ? _dist[p, v] - _dist[u, v] : 0);
            int togetherDelta = _dist[u, p] + _dist[p, q] + (hasNext ? _dist[q, v] - _dist[u, v] : 0);

            // Carrying the new package adds 1 to the load at sequence positions a..b.
            int peak = 0;
            for (int b = a; b <= n; b++)
            {
                peak = Math.Max(peak, loadAfter[b]);
                if (peak + 1 > Capacity)
                {
                    break;   // a later delivery position can only be worse
                }

                int delta;
                if (b == a)
                {
                    delta = togetherDelta;
                }
                else
                {
                    int u2 = At(b);
                    bool hasNext2 = b < seqEnd;
                    int v2 = hasNext2 ? At(b + 1) : -1;
                    delta = pickupDelta + _dist[u2, q] + (hasNext2 ? _dist[q, v2] - _dist[u2, v2] : 0);
                }

                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    bestA = a;
                    bestB = b;
                }
            }
        }

        return (bestDelta, bestA, bestB);
    }

    /// <summary>profile[k] = packages carried after the first k stops (profile[0] = 0).</summary>
    private static int[] LoadProfile(List<Node> route)
    {
        var profile = new int[route.Count + 1];
        int load = 0;
        for (int i = 0; i < route.Count; i++)
        {
            load += route[i].IsPickup ? 1 : -1;
            profile[i + 1] = load;
        }

        return profile;
    }

    private bool RespectsCapacity(List<Node> route)
    {
        int load = 0;
        foreach (Node node in route)
        {
            load += node.IsPickup ? 1 : -1;
            if (load > Capacity)
            {
                return false;
            }
        }

        return true;
    }

    // ------------------------------------------------------------------ improvement

    private List<Node> LocalSearch(List<Node> route, Random random)
    {
        int cost = Cost(route);
        bool improved;
        do
        {
            improved = false;

            // Relocate whole packages (pickup + delivery) to their best position.
            int[] order = Enumerable.Range(0, _packages.Count).ToArray();
            Shuffle(order, random);
            foreach (int pkg in order)
            {
                List<Node> without = route.Where(node => node.Pkg != pkg).ToList();
                var (delta, _, _) = BestInsertion(without, pkg);
                int newCost = Cost(without) + delta;
                if (newCost < cost)
                {
                    route = InsertPackage(without, pkg);
                    cost = newCost;
                    improved = true;
                }
            }

            // Move single stops while keeping "pickup before delivery" and the capacity.
            if (MoveSingleStops(ref route, ref cost))
            {
                improved = true;
            }
        }
        while (improved);

        return route;
    }

    private bool MoveSingleStops(ref List<Node> route, ref int cost)
    {
        bool improved = false;
        for (int i = 0; i < route.Count; i++)
        {
            Node node = route[i];
            List<Node> rest = new(route);
            rest.RemoveAt(i);

            // Allowed insertion indices in 'rest' to respect the precedence rule.
            int lowest = 0;
            int highest = rest.Count;
            int partner = rest.FindIndex(x => x.Pkg == node.Pkg);
            if (node.IsPickup)
            {
                highest = partner;       // must stay before its delivery
            }
            else
            {
                lowest = partner + 1;    // must stay after its pickup
            }

            int bestCost = cost;
            int bestPosition = -1;
            for (int j = lowest; j <= highest; j++)
            {
                rest.Insert(j, node);
                int candidate = Cost(rest);
                bool better = candidate < bestCost && RespectsCapacity(rest);
                rest.RemoveAt(j);
                if (better)
                {
                    bestCost = candidate;
                    bestPosition = j;
                }
            }

            if (bestPosition >= 0)
            {
                rest.Insert(bestPosition, node);
                route = rest;
                cost = bestCost;
                improved = true;
            }
        }

        return improved;
    }

    // ------------------------------------------------------------------ helpers

    private List<Stop> ToStops(List<Node> route) =>
        route.Select(node => new Stop(
                _points[node.Loc],
                node.IsPickup ? StopKind.Pickup : StopKind.Delivery,
                _packages[node.Pkg].Id))
            .ToList();

    private static void Shuffle(int[] items, Random random)
    {
        for (int i = items.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
