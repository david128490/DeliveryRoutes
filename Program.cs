using Backend;

try
{
    CityData city = CityFile.Load("city.txt");

    foreach (var (from, to) in new[] { ("AQ", "BS"), ("BS", "AQ"), ("D", "AR") })
    {
        var (distance, previous) = ShortestPaths.Dijkstra(city.Graph, from);
        var path = ShortestPaths.ReconstructPath(previous, from, to);
        Console.WriteLine($"{from} -> {to}: {distance[to]} m   Camino: {string.Join(" -> ", path)}");
    }
}
catch (DataValidationException ex)
{
    foreach (string error in ex.Errors)
    {
        Console.Error.WriteLine($"  - {error}");
    }
}