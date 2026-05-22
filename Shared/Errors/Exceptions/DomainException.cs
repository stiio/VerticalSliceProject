namespace VerticalSliceProject.Shared.Errors.Exceptions;

/// <summary>Base class for domain-invariant violations.</summary>
public class DomainException : Exception
{
    public DomainException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
