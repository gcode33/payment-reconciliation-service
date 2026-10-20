namespace Payments.Domain.Payments;

public sealed class Capture
{
    internal Capture(Guid id, Money amount)
    {
        Id = id;
        Amount = amount;
        Status = OperationStatus.Pending;
    }

    public Guid Id { get; }

    public Money Amount { get; }

    public OperationStatus Status { get; private set; }

    /// <summary>True while the provider may still move money for this capture.</summary>
    public bool IsInFlightOrSucceeded => Status is OperationStatus.Pending or OperationStatus.Unknown or OperationStatus.Succeeded;

    internal void MoveTo(OperationStatus to)
    {
        if (!PaymentStateMachine.IsAllowed(Status, to, allowLateFailure: false))
        {
            throw new InvalidTransitionException(nameof(Capture), Status, to);
        }

        Status = to;
    }
}
