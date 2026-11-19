using Backend;

try
{
    CityData city = CityFile.Load("city.txt");
    List<Package> packages = PackageFile.Load("packages.txt", city.Graph);

    Console.WriteLine($"Ubicaciones: {city.Graph.VertexCount}");
    Console.WriteLine($"Calles: {city.StreetCount} ({city.OneWayCount} de un solo sentido)");
    Console.WriteLine($"Paquetes: {packages.Count}");
    Console.WriteLine($"Primer paquete: {packages[0]}");
}
catch (DataValidationException ex)
{
    foreach (string error in ex.Errors)
    {
        Console.Error.WriteLine($"  - {error}");
    }
}