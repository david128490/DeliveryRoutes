using System.Globalization;

namespace Backend;

/// <summary>Loads and validates packages.txt: location,action,package.</summary>
public static class PackageFile
{
    private sealed class Draft
    {
        public string? Pickup;
        public int PickupLine;
        public string? Delivery;
        public int DeliveryLine;
    }

    public static List<Package> Load(string path, Graph<string> city)
    {
        if (!File.Exists(path))
        {
            throw new DataValidationException(new[] { $"No se encontró el archivo de paquetes: {path}" });
        }

        var errors = new List<string>();
        var drafts = new SortedDictionary<int, Draft>();

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
            if (fields.Length != 3 || fields.Any(f => f.Length == 0))
            {
                errors.Add($"{path}, línea {lineNumber}: se esperaban 3 campos (ubicación,acción,paquete) y se encontró «{line}».");
                continue;
            }

            string location = fields[0];
            bool lineOk = true;

            if (!city.HasVertex(location))
            {
                errors.Add($"{path}, línea {lineNumber}: la ubicación «{location}» no existe en el mapa.");
                lineOk = false;
            }

            bool isPickup = false;
            if (fields[1].Equals("Pickup", StringComparison.OrdinalIgnoreCase))
            {
                isPickup = true;
            }
            else if (!fields[1].Equals("Delivery", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{path}, línea {lineNumber}: la acción «{fields[1]}» no es Pickup ni Delivery.");
                lineOk = false;
            }

            if (!int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) || id <= 0)
            {
                errors.Add($"{path}, línea {lineNumber}: el número de paquete «{fields[2]}» no es un entero mayor que 0.");
                lineOk = false;
            }

            if (!lineOk)
            {
                continue;
            }

            if (!drafts.TryGetValue(id, out Draft? draft))
            {
                draft = new Draft();
                drafts[id] = draft;
            }

            if (isPickup)
            {
                if (draft.Pickup != null)
                {
                    errors.Add($"{path}, línea {lineNumber}: el paquete {id} ya tiene una recogida (línea {draft.PickupLine}).");
                    continue;
                }

                draft.Pickup = location;
                draft.PickupLine = lineNumber;
            }
            else
            {
                if (draft.Delivery != null)
                {
                    errors.Add($"{path}, línea {lineNumber}: el paquete {id} ya tiene una entrega (línea {draft.DeliveryLine}).");
                    continue;
                }

                draft.Delivery = location;
                draft.DeliveryLine = lineNumber;
            }
        }

        var packages = new List<Package>();
        foreach (var (id, draft) in drafts)
        {
            if (draft.Pickup == null)
            {
                errors.Add($"{path}: al paquete {id} le falta la recogida (Pickup).");
            }
            else if (draft.Delivery == null)
            {
                errors.Add($"{path}: al paquete {id} le falta la entrega (Delivery).");
            }
            else
            {
                packages.Add(new Package(id, draft.Pickup, draft.Delivery));
            }
        }

        if (errors.Count == 0 && packages.Count == 0)
        {
            errors.Add($"{path}: el archivo no contiene ningún paquete.");
        }

        if (errors.Count > 0)
        {
            throw new DataValidationException(errors);
        }

        return packages;
    }
}
