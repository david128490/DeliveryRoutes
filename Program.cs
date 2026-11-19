using Backend;

// Usage: dotnet run -- city.txt packages.txt D
string cityPath;
string packagesPath;
string start;

if (args.Length >= 3)
{
    cityPath = args[0];
    packagesPath = args[1];
    start = args[2].Trim();
}
else
{
    Console.Write("Ruta del archivo del mapa (city.txt): ");
    cityPath = (Console.ReadLine() ?? "").Trim();
    Console.Write("Ruta del archivo de paquetes (packages.txt): ");
    packagesPath = (Console.ReadLine() ?? "").Trim();
    Console.Write("Posición inicial del domiciliario: ");
    start = (Console.ReadLine() ?? "").Trim();
}

try
{
    CityData city = CityFile.Load(cityPath);
    List<Package> packages = PackageFile.Load(packagesPath, city.Graph);

    if (!city.Graph.HasVertex(start))
    {
        throw new DataValidationException(new[] { $"La posición inicial «{start}» no existe en el mapa." });
    }

    var planner = new RoutePlanner(city.Graph, packages, start);
    List<Stop> route = planner.Plan();

    Console.Write(RoutePrinter.Build(
        planner, route, start, packages.Count, city.Graph.VertexCount, city.StreetCount, city.OneWayCount));
}
catch (DataValidationException ex)
{
    Console.Error.WriteLine("Se encontraron errores en los datos:");
    foreach (string error in ex.Errors)
    {
        Console.Error.WriteLine($"  - {error}");
    }

    return 1;
}

return 0;
