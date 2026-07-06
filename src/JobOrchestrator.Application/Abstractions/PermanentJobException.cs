namespace JobOrchestrator.Application.Abstractions;

/// <summary>Marks a job failure as unrecoverable — deserialization errors, unknown job types, and other errors no retry could fix. The resilient executor dead-letters immediately instead of consuming the retry budget.</summary>
public sealed class PermanentJobException : Exception
{
    public PermanentJobException(string message) : base(message)
    {
    }

    public PermanentJobException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
