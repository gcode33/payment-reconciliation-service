namespace Payments.Domain;

/// <summary>A state change that is not an allowed edge of the state machine (invariant I6).</summary>
public sealed class InvalidTransitionException : DomainException
{
    public InvalidTransitionException(string entity, object from, object to)
        : base($"{entity} cannot move from {from} to {to}.")
    {
        From = from;
        To = to;
    }

    public object From { get; }

    public object To { get; }
}
