namespace Backend;

/// <summary>Thrown when an input file or argument is invalid. Carries every problem found.</summary>
public class DataValidationException : Exception
{
    public DataValidationException(IReadOnlyList<string> errors)
        : base(string.Join(Environment.NewLine, errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
