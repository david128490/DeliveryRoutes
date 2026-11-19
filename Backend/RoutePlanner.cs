using System.Diagnostics;

namespace Backend;

/// <summary>
/// Plans the pickup-and-delivery route. Exact part: Dijkstra distances between the points that
/// matter. Heuristic part: cheapest insertion + relocation + single-stop moves, with random restarts.
/// </summary>
public class RoutePlanner
{
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
    public int Restarts { get; init; } = 200;

    /// <summary>Safety time limit (seconds) for the whole improvement phase.</summary>
    public double MaxSeconds { get; init; } = 10;

    /// <summary>Fixed seed so the result is reproducible.</summary>
    public int Seed { get; init; } = 12345;

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

        for (int step = 0; step < 2 * _packages.Count; step++)
        {
            Node? bestNode = null;
            int bestDistance = int.MaxValue;
            for (int pkg = 0; pkg < _packages.Count; pkg++)
            {
                if (!picked[pkg] && _dist[current, _pickupLoc[pkg]] < bestDistance)
                {
                    bestDistance = _dist[current, _pickupLoc[pkg]];
                    bestNode = new Node(_pickupLoc[pkg], true, pkg);
                }
                else if (picked[pkg] && !delivered[pkg] && _dist[current, _deliveryLoc[pkg]] < bestDistance)
                {
                    bestDistance = _dist[current, _deliveryLoc[pkg]];
                    bestNode = new Node(_deliveryLoc[pkg], false, pkg);
                }
            }

            Node chosen = bestNode!.Value;
            if (chosen.IsPickup) picked[chosen.Pkg] = true; else delivered[chosen.Pkg] = true;
            route.Add(chosen);
            current = chosen.Loc;
        }

        return ToStops(route);
    }

    /// <summary>Total metres of the route (start -> first stop -> ... -> last stop).</summary>
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

        return total;
    }

    /// <summary>Shortest distance in metres between two relevant points.</summary>
    public int Distance(string from, string to) => _dist[_index[from], _index[to]];

    /// <summary>
    /// True if every package is picked up exactly once and delivered exactly once,
    /// after its pickup, and each task happens at the package's own location.
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
            }
            else
            {
                if (stop.Location != package.Delivery || !picked.Contains(stop.PackageId) || !delivered.Add(stop.PackageId))
                {
                    return false;
                }
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
    /// </summary>
    private (int Delta, int A, int B) BestInsertion(List<Node> route, int pkg)
    {
        int n = route.Count;
        int p = _pickupLoc[pkg];
        int q = _deliveryLoc[pkg];

        // Sequence position k: 0 = start, k >= 1 = route[k - 1].
        int At(int k) => k == 0 ? StartIndex : route[k - 1].Loc;

        int bestDelta = int.MaxValue;
        int bestA = 0;
        int bestB = 0;

        for (int a = 0; a <= n; a++)
        {
            int u = At(a);
            bool hasNext = a < n;
            int v = hasNext ? At(a + 1) : -1;

            int pickupDelta = _dist[u, p] + (hasNext ? _dist[p, v] - _dist[u, v] : 0);
            int togetherDelta = _dist[u, p] + _dist[p, q] + (hasNext ? _dist[q, v] - _dist[u, v] : 0);

            for (int b = a; b <= n; b++)
            {
                int delta;
                if (b == a)
                {
                    delta = togetherDelta;
                }
                else
                {
                    int u2 = At(b);
                    bool hasNext2 = b < n;
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

            // Move single stops while keeping "pickup before delivery".
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
                rest.RemoveAt(j);
                if (candidate < bestCost)
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