namespace Gateway.Configuration;

public sealed class OptimisticConcurrencyException : Exception
{
    public OptimisticConcurrencyException(string sectionName)
        : base($"Configuration section '{sectionName}' has been modified by another client.")
    {
    }
}
