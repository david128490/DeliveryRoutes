namespace Backend;

/// <summary>Dijkstra (single source, non-negative weights) and path reconstruction.</summary>
public static class ShortestPaths
{
    /// <summary>
    /// Returns the minimum distance from source to every reachable vertex and the
    /// predecessor of each vertex on its shortest path. Unreachable vertices are absent.
    /// </summary>
    public static (Dictionary<T, int> Distance, Dictionary<T, T> Previous) Dijkstra<T>(Graph<T> graph, T source)
        where T : notnull
    {
        var distance = new Dictionary<T, int> { [source] = 0 };
        var previous = new Dictionary<T, T>();
        var queue = new PriorityQueue<T, int>();
        queue.Enqueue(source, 0);

        while (queue.TryDequeue(out T? current, out int currentDistance))
        {
            // Stale queue entry: a shorter path to this vertex was already processed.
            if (currentDistance > distance[current])
            {
                continue;
            }

            foreach (Edge<T> edge in graph.Neighbors(current))
            {
                int candidate = currentDistance + edge.Weight;
                if (!distance.TryGetValue(edge.To, out int known) || candidate < known)
                {
                    distance[edge.To] = candidate;
                    previous[edge.To] = current;
                    queue.Enqueue(edge.To, candidate);
                }
            }
        }

        return (distance, previous);
    }

    /// <summary>Rebuilds source -> ... -> target from Dijkstra's predecessors (empty if unreachable).</summary>
    public static List<T> ReconstructPath<T>(Dictionary<T, T> previous, T source, T target)
        where T : notnull
    {
        var path = new List<T> { target };
        T current = target;
        while (!EqualityComparer<T>.Default.Equals(current, source))
        {
            if (!previous.TryGetValue(current, out T? parent))
            {
                return new List<T>();
            }

            path.Add(parent);
            current = parent;
        }

        path.Reverse();
        return path;
    }
}
