using System.Diagnostics;
using System.Globalization;
using Backend;

try
{
    Test("city_mini.txt", "packages_mini.txt", "A");
    Test("city.txt", "packages.txt", "D");
}
catch (DataValidationException ex)
{
    foreach (string error in ex.Errors)
    {
        Console.Error.WriteLine($"  - {error}");
    }
}

static string Km(double meters) => (meters / 1000).ToString("F2", CultureInfo.InvariantCulture);

static void Test(string cityPath, string packagesPath, string start)
{
    CityData city = CityFile.Load(cityPath);
    List<Package> packages = PackageFile.Load(packagesPath, city.Graph);
    var planner = new RoutePlanner(city.Graph, packages, start);

    List<Stop> greedy = planner.PlanGreedy();

    var stopwatch = Stopwatch.StartNew();
    List<Stop> best = planner.Plan();
    stopwatch.Stop();

    Console.WriteLine($"{cityPath} (inicio {start})");
    Console.WriteLine($"  Vecino más cercano: {Km(planner.TotalDistance(greedy))} km   válida: {planner.IsValid(greedy)}");
    Console.WriteLine($"  Ruta mejorada:      {Km(planner.TotalDistance(best))} km   válida: {planner.IsValid(best)}   ({stopwatch.Elapsed.TotalSeconds:F1} s)");
    Console.WriteLine();
}