using Backend;

try
{
    CityData city = CityFile.Load("city.txt");
    Console.WriteLine($"Ubicaciones: {city.Graph.VertexCount}");
    Console.WriteLine($"Calles: {city.StreetCount} ({city.OneWayCount} de un solo sentido)");
}
catch (DataValidationException ex)
{
    foreach (string error in ex.Errors)
    {
        Console.Error.WriteLine($"  - {error}");
    }
}
