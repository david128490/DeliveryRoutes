namespace Backend;

/// <summary>Exact solver (recursion + backtracking) for small instances.</summary>
public partial class RoutePlanner
{
    /// <summary>Above this many packages the exact search takes too long.</summary>
    public const int MaxExactPackages = 8;

    private int _exactBestCost;
    private List<Node>? _exactBestRoute;

    /// <summary>
    /// Optimal route found by trying every valid order, pruning branches whose partial distance
    /// already reaches the best total known. The heuristic route gives the first upper bound.
    /// </summary>
    public List<Stop> PlanExact(List<Stop> heuristicRoute)
    {
        if (_packages.Count > MaxExactPackages)
        {
            throw new DataValidationException(new[]
            {
                $"La opción --exact solo funciona con {MaxExactPackages} paquetes o menos (el archivo tiene {_packages.Count}).",
            });
        }

        // +1 so that a route as good as the heuristic one is also found (and the search can prove it optimal).
        _exactBestCost = (int)TotalDistance(heuristicRoute) + 1;
        _exactBestRoute = null;

        int n = _packages.Count;
        Search(new List<Node>(), new bool[n], new bool[n], StartIndex, 0, 0);

        return _exactBestRoute == null ? heuristicRoute : ToStops(_exactBestRoute);
    }

    private void Search(List<Node> path, bool[] picked, bool[] delivered, int current, int cost, int load)
    {
        // Prune: this branch cannot beat the best total known.
        if (cost >= _exactBestCost)
        {
            return;
        }

        if (path.Count == 2 * _packages.Count)
        {
            int total = cost + (ReturnToStart ? _dist[current, StartIndex] : 0);
            if (total < _exactBestCost)
            {
                _exactBestCost = total;
                _exactBestRoute = new List<Node>(path);
            }

            return;
        }

        for (int pkg = 0; pkg < _packages.Count; pkg++)
        {
            if (delivered[pkg])
            {
                continue;
            }

            if (!picked[pkg])
            {
                if (load >= Capacity)
                {
                    continue;
                }

                picked[pkg] = true;
                path.Add(new Node(_pickupLoc[pkg], true, pkg));
                Search(path, picked, delivered, _pickupLoc[pkg], cost + _dist[current, _pickupLoc[pkg]], load + 1);
                path.RemoveAt(path.Count - 1);
                picked[pkg] = false;
            }
            else
            {
                delivered[pkg] = true;
                path.Add(new Node(_deliveryLoc[pkg], false, pkg));
                Search(path, picked, delivered, _deliveryLoc[pkg], cost + _dist[current, _deliveryLoc[pkg]], load - 1);
                path.RemoveAt(path.Count - 1);
                delivered[pkg] = false;
            }
        }
    }
}