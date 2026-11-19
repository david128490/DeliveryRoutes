using System.Globalization;

namespace Backend;

/// <summary>Command-line arguments: up to four positional values plus optional flags.</summary>
public sealed class CommandLineOptions
{
    public string? CityPath { get; private set; }
    public string? PackagesPath { get; private set; }
    public string? Start { get; private set; }

    /// <summary>Fourth positional argument: maximum packages carried at once.</summary>
    public int Capacity { get; private set; } = int.MaxValue;

    public bool ReturnToStart { get; private set; }
    public bool Exact { get; private set; }
    public bool Compare { get; private set; }
    public bool Export { get; private set; }

    public int Restarts { get; private set; } = RoutePlanner.DefaultRestarts;
    public double Seconds { get; private set; } = RoutePlanner.DefaultMaxSeconds;
    public int Seed { get; private set; } = RoutePlanner.DefaultSeed;

    /// <summary>Parses the arguments; throws DataValidationException listing every problem.</summary>
    public static CommandLineOptions Parse(string[] args)
    {
        var options = new CommandLineOptions();
        var errors = new List<string>();
        var positional = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg.ToLowerInvariant())
            {
                case "--return":
                    options.ReturnToStart = true;
                    break;
                case "--exact":
                    options.Exact = true;
                    break;
                case "--compare":
                    options.Compare = true;
                    break;
                case "--export":
                    options.Export = true;
                    break;
                case "--restarts":
                    options.Restarts = ReadInt(args, ref i, arg, 1, errors, options.Restarts);
                    break;
                case "--seconds":
                    options.Seconds = ReadInt(args, ref i, arg, 1, errors, (int)options.Seconds);
                    break;
                case "--seed":
                    options.Seed = ReadInt(args, ref i, arg, 0, errors, options.Seed);
                    break;
                default:
                    if (arg.StartsWith("--"))
                    {
                        errors.Add($"Opción desconocida: {arg}");
                    }
                    else
                    {
                        positional.Add(arg);
                    }

                    break;
            }
        }

        if (positional.Count is 1 or 2)
        {
            errors.Add("Faltan argumentos. Uso: dotnet run -- city.txt packages.txt D [capacidad] [opciones]");
        }
        else if (positional.Count > 4)
        {
            errors.Add("Hay demasiados argumentos. Uso: dotnet run -- city.txt packages.txt D [capacidad] [opciones]");
        }
        else if (positional.Count >= 3)
        {
            options.CityPath = positional[0];
            options.PackagesPath = positional[1];
            options.Start = positional[2].Trim();

            if (positional.Count == 4)
            {
                if (int.TryParse(positional[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int capacity) && capacity > 0)
                {
                    options.Capacity = capacity;
                }
                else
                {
                    errors.Add($"La capacidad «{positional[3]}» no es un entero mayor que 0.");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new DataValidationException(errors);
        }

        return options;
    }

    /// <summary>Reads the integer that follows an option (and consumes it).</summary>
    private static int ReadInt(string[] args, ref int i, string name, int minimum, List<string> errors, int fallback)
    {
        if (i + 1 < args.Length
            && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            && value >= minimum)
        {
            i++;
            return value;
        }

        errors.Add($"La opción {name} necesita un número entero mayor o igual a {minimum}.");
        return fallback;
    }
}