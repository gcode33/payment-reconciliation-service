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

    internal void MoveTo(OperationStatus to) => throw new NotImplementedException();
}
