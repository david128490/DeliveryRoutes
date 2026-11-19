using System.Globalization;

namespace Backend;

/// <summary>The loaded city map plus some statistics for the report.</summary>
public sealed class CityData
{
    public CityData(Graph<string> graph, int streetCount, int oneWayCount)
    {
        Graph = graph;
        StreetCount = streetCount;
        OneWayCount = oneWayCount;
    }

    public Graph<string> Graph { get; }
    public int StreetCount { get; }
    public int OneWayCount { get; }
}

/// <summary>Loads and validates city.txt: from,to,distance_in_meters,direction.</summary>
public static class CityFile
{
    public static CityData Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new DataValidationException(new[] { $"No se encontró el archivo del mapa: {path}" });
        }

        var errors = new List<string>();
        var graph = new Graph<string>(directed: true);
        var seenStreets = new Dictionary<(string, string), int>();
        int streets = 0;
        int oneWay = 0;

        string[] lines = File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++)
        {
            int lineNumber = i + 1;
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            string[] fields = line.Split(',').Select(f => f.Trim()).ToArray();
            if (fields.Length != 4 || fields.Any(f => f.Length == 0))
            {
                errors.Add($"{path}, línea {lineNumber}: se esperaban 4 campos (from,to,distancia,sentido) y se encontró «{line}».");
                continue;
            }

            string from = fields[0];
            string to = fields[1];

            if (!int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int meters) || meters <= 0)
            {
                errors.Add($"{path}, línea {lineNumber}: la distancia «{fields[2]}» no es un número entero positivo.");
                continue;
            }

            bool isTwoWay;
            if (fields[3].Equals("TwoWay", StringComparison.OrdinalIgnoreCase))
            {
                isTwoWay = true;
            }
            else if (fields[3].Equals("OneWay", StringComparison.OrdinalIgnoreCase))
            {
                isTwoWay = false;
            }
            else
            {
                errors.Add($"{path}, línea {lineNumber}: el sentido «{fields[3]}» no es TwoWay ni OneWay.");
                continue;
            }

            if (from == to)
            {
                errors.Add($"{path}, línea {lineNumber}: la calle une la ubicación {from} consigo misma.");
                continue;
            }

            // A,B and B,A are the same street.
            var key = string.CompareOrdinal(from, to) <= 0 ? (from, to) : (to, from);
            if (seenStreets.TryGetValue(key, out int firstLine))
            {
                errors.Add($"{path}, línea {lineNumber}: la calle entre {from} y {to} ya apareció en la línea {firstLine}.");
                continue;
            }

            seenStreets[key] = lineNumber;
            graph.AddEdge(from, to, meters);
            if (isTwoWay)
            {
                graph.AddEdge(to, from, meters);
            }
            else
            {
                oneWay++;
            }

            streets++;
        }

        if (errors.Count == 0 && streets == 0)
        {
            errors.Add($"{path}: el archivo no contiene ninguna calle.");
        }

        if (errors.Count > 0)
        {
            throw new DataValidationException(errors);
        }

        return new CityData(graph, streets, oneWay);
    }
}