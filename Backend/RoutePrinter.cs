using System.Globalization;
using System.Text;

namespace Backend;

/// <summary>Builds the step-by-step output described in section 5 of the workshop.</summary>
public static class RoutePrinter
{
    public static string Build(
        RoutePlanner planner,
        List<Stop> route,
        string start,
        int packageCount,
        int locationCount,
        int streetCount,
        int oneWayCount)
    {
        var text = new StringBuilder();
        text.AppendLine($"Posición inicial: {start}");
        text.AppendLine($"Paquetes: {packageCount} Ubicaciones: {locationCount} Calles: {streetCount} ({oneWayCount} de un solo sentido)");
        text.AppendLine();

        var carried = new HashSet<int>();
        string current = start;
        long totalMeters = 0;
        int stopNumber = 0;

        int i = 0;
        while (i < route.Count)
        {
            // Group consecutive tasks that happen at the same place into one stop.
            string location = route[i].Location;
            var group = new List<Stop>();
            while (i < route.Count && route[i].Location == location)
            {
                group.Add(route[i]);
                i++;
            }

            // Deliveries of packages already carried first, then pickups,
            // then deliveries of packages picked up in this very stop.
            var deliveriesFirst = group.Where(s => s.Kind == StopKind.Delivery && carried.Contains(s.PackageId)).Select(s => s.PackageId).ToList();
            var pickups = group.Where(s => s.Kind == StopKind.Pickup).Select(s => s.PackageId).ToList();
            var deliveriesLast = group.Where(s => s.Kind == StopKind.Delivery && !carried.Contains(s.PackageId)).Select(s => s.PackageId).ToList();

            var actions = new List<string>();
            if (deliveriesFirst.Count > 0) actions.Add("entregar " + Packages(deliveriesFirst));
            if (pickups.Count > 0) actions.Add("recoger " + Packages(pickups));
            if (deliveriesLast.Count > 0) actions.Add("entregar " + Packages(deliveriesLast));
            string actionText = string.Join(" y ", actions);

            foreach (int id in pickups) carried.Add(id);

            stopNumber++;
            if (location == current)
            {
                text.AppendLine($"{stopNumber}. En {location}: {actionText}.");
            }
            else
            {
                int meters = planner.Distance(current, location);
                totalMeters += meters;
                text.AppendLine($"{stopNumber}. Ir de {current} a {location} ({meters} m): {actionText}.");
                text.AppendLine($"   Camino: {string.Join(" -> ", planner.PathBetween(current, location))}");
            }

            current = location;
        }

        if (!planner.IsValid(route) || totalMeters != (long)planner.TotalDistance(route))
        {
            throw new InvalidOperationException("La ruta calculada no pasó la verificación final.");
        }

        text.AppendLine();
        text.AppendLine("Todos los paquetes fueron recogidos y entregados.");
        text.AppendLine($"Recorrido total: {(totalMeters / 1000.0).ToString("F2", CultureInfo.InvariantCulture)} km");
        return text.ToString();
    }

    /// <summary>"paquete 5", "paquetes 5 y 7", "paquetes 1, 2 y 3".</summary>
    private static string Packages(List<int> ids)
    {
        if (ids.Count == 1)
        {
            return $"paquete {ids[0]}";
        }

        string head = string.Join(", ", ids.Take(ids.Count - 1));
        return $"paquetes {head} y {ids[^1]}";
    }
}