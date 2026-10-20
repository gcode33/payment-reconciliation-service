namespace Payments.Domain.Payments;

public sealed class Refund
{
    internal Refund(Guid id, Money amount)
    {
        Id = id;
        Amount = amount;
        Status = OperationStatus.Pending;
    }

    public Guid Id { get; }

    public Money Amount { get; }

    public OperationStatus Status { get; private set; }

    /// <summary>True while the provider may still move money for this refund.</summary>
    public bool IsInFlightOrSucceeded => Status is OperationStatus.Pending or OperationStatus.Unknown or OperationStatus.Succeeded;

    internal void MoveTo(OperationStatus to)
    {
        if (!PaymentStateMachine.IsAllowed(Status, to, allowLateFailure: true))
        {
            throw new InvalidTransitionException(nameof(Refund), Status, to);
        }

        Status = to;
    }
}
