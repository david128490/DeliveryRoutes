namespace Backend;

/// <summary>Weighted graph stored as an adjacency list.</summary>
public class Graph<T> where T : notnull
{
    private readonly Dictionary<T, List<Edge<T>>> _adjacency = new();

    public Graph(bool directed)
    {
        Directed = directed;
    }

    public bool Directed { get; }

    public int VertexCount => _adjacency.Count;

    public IEnumerable<T> Vertices => _adjacency.Keys;

    public bool HasVertex(T vertex) => _adjacency.ContainsKey(vertex);

    public void AddVertex(T vertex)
    {
        if (!_adjacency.ContainsKey(vertex))
        {
            _adjacency[vertex] = new List<Edge<T>>();
        }
    }

    /// <summary>Adds an edge. In an undirected graph the reverse edge is added too.</summary>
    public void AddEdge(T from, T to, int weight)
    {
        AddVertex(from);
        AddVertex(to);
        _adjacency[from].Add(new Edge<T>(from, to, weight));
        if (!Directed)
        {
            _adjacency[to].Add(new Edge<T>(to, from, weight));
        }
    }

    public IReadOnlyList<Edge<T>> Neighbors(T vertex) => _adjacency[vertex];
}
