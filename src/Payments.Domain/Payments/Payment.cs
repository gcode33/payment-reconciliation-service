namespace Payments.Domain.Payments;

/// <summary>
/// A card payment and its capture and refunds. All status changes go through the transition
/// table in <see cref="PaymentStateMachine"/> and are recorded in <see cref="Transitions"/>.
/// </summary>
public sealed class Payment
{
    private Payment(Guid id, Money authorizedAmount)
    {
        Id = id;
        AuthorizedAmount = authorizedAmount;
    }

    public Guid Id { get; }

    public Money AuthorizedAmount { get; }

    public PaymentStatus Status { get; private set; }

    /// <summary>The latest capture attempt, if any.</summary>
    public Capture? Capture => throw new NotImplementedException();

    public IReadOnlyList<Refund> Refunds => throw new NotImplementedException();

    public IReadOnlyList<PaymentTransition> Transitions => throw new NotImplementedException();

    /// <summary>The amount of the succeeded capture, or zero.</summary>
    public Money CapturedAmount => throw new NotImplementedException();

    /// <summary>The total of succeeded refunds.</summary>
    public Money RefundedAmount => throw new NotImplementedException();

    /// <summary>Captured minus every refund that has succeeded or might still succeed.</summary>
    public Money RefundableAmount => throw new NotImplementedException();

    public static Payment StartAuthorization(Guid id, Money amount) => throw new NotImplementedException();

    public void MarkAuthorized() => throw new NotImplementedException();

    public void MarkDeclined() => throw new NotImplementedException();

    public void MarkUnknown() => throw new NotImplementedException();

    public void Void() => throw new NotImplementedException();

    public Capture StartCapture(Guid captureId, Money amount) => throw new NotImplementedException();

    public void RecordCaptureOutcome(OperationStatus outcome) => throw new NotImplementedException();

    public Refund StartRefund(Guid refundId, Money amount) => throw new NotImplementedException();

    public void RecordRefundOutcome(Guid refundId, OperationStatus outcome) => throw new NotImplementedException();
}
