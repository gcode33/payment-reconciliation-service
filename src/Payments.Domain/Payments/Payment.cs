namespace Payments.Domain.Payments;

/// <summary>
/// A card payment and its capture and refunds. All status changes go through the transition
/// table in <see cref="PaymentStateMachine"/> and are recorded in <see cref="Transitions"/>.
/// </summary>
public sealed class Payment
{
    private readonly List<PaymentTransition> _transitions = [];
    private readonly List<Refund> _refunds = [];

    private Payment(Guid id, Money authorizedAmount)
    {
        Id = id;
        AuthorizedAmount = authorizedAmount;
    }

    public Guid Id { get; }

    public Money AuthorizedAmount { get; }

    public PaymentStatus Status { get; private set; }

    /// <summary>The latest capture attempt, if any. One capture per payment (docs/decisions/0004).</summary>
    public Capture? Capture { get; private set; }

    public IReadOnlyList<Refund> Refunds => _refunds;

    public IReadOnlyList<PaymentTransition> Transitions => _transitions;

    /// <summary>The amount of the succeeded capture, or zero.</summary>
    public Money CapturedAmount =>
        Capture is { Status: OperationStatus.Succeeded } ? Capture.Amount : Money.Zero(AuthorizedAmount.Currency);

    /// <summary>The total of succeeded refunds.</summary>
    public Money RefundedAmount => Sum(_refunds.Where(r => r.Status == OperationStatus.Succeeded));

    /// <summary>Captured minus every refund that has succeeded or might still succeed.</summary>
    public Money RefundableAmount => CapturedAmount - Sum(_refunds.Where(r => r.IsInFlightOrSucceeded));

    public static Payment StartAuthorization(Guid id, Money amount)
    {
        if (!amount.IsPositive)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "An authorization must be for a positive amount.");
        }

        var payment = new Payment(id, amount) { Status = PaymentStatus.Authorizing };
        payment._transitions.Add(new PaymentTransition(null, PaymentStatus.Authorizing));
        return payment;
    }

    public void MarkAuthorized() => MoveTo(PaymentStatus.Authorized);

    public void MarkDeclined() => MoveTo(PaymentStatus.Declined);

    public void MarkUnknown() => MoveTo(PaymentStatus.Unknown);

    public void Void()
    {
        // A succeeded capture is refused by the transition table (Captured -> Voided is not an edge).
        if (Capture is { Status: OperationStatus.Pending or OperationStatus.Unknown })
        {
            throw new DomainException("A payment with a capture in flight cannot be voided.");
        }

        MoveTo(PaymentStatus.Voided);
    }

    public Capture StartCapture(Guid captureId, Money amount)
    {
        if (Status != PaymentStatus.Authorized)
        {
            throw new DomainException($"Only an authorized payment can be captured; this one is {Status}.");
        }

        if (Capture is { IsInFlightOrSucceeded: true })
        {
            throw new DomainException("This payment already has a capture in flight or succeeded.");
        }

        if (!amount.IsPositive || amount > AuthorizedAmount)
        {
            throw new DomainException($"A capture must be more than zero and at most {AuthorizedAmount}; got {amount}.");
        }

        Capture = new Capture(captureId, amount);
        return Capture;
    }

    public void RecordCaptureOutcome(OperationStatus outcome)
    {
        if (Capture is null)
        {
            throw new DomainException("There is no capture to record an outcome for.");
        }

        Capture.MoveTo(outcome);
        if (outcome == OperationStatus.Succeeded)
        {
            MoveTo(PaymentStatus.Captured);
        }
    }

    public Refund StartRefund(Guid refundId, Money amount)
    {
        if (Status is not (PaymentStatus.Captured or PaymentStatus.PartiallyRefunded))
        {
            throw new DomainException($"Only a captured payment can be refunded; this one is {Status}.");
        }

        if (_refunds.Exists(r => r.Id == refundId))
        {
            throw new DomainException($"Refund {refundId} already exists.");
        }

        if (!amount.IsPositive || amount > RefundableAmount)
        {
            throw new DomainException($"A refund must be more than zero and at most {RefundableAmount}; got {amount}.");
        }

        var refund = new Refund(refundId, amount);
        _refunds.Add(refund);
        return refund;
    }

    public void RecordRefundOutcome(Guid refundId, OperationStatus outcome)
    {
        var refund = _refunds.Find(r => r.Id == refundId)
            ?? throw new DomainException($"Refund {refundId} does not belong to this payment.");

        refund.MoveTo(outcome);

        var target = RefundedAmount.IsZero
            ? PaymentStatus.Captured
            : RefundedAmount < CapturedAmount ? PaymentStatus.PartiallyRefunded : PaymentStatus.Refunded;
        if (target != Status)
        {
            MoveTo(target);
        }
    }

    private void MoveTo(PaymentStatus to)
    {
        if (!PaymentStateMachine.IsAllowed(Status, to))
        {
            throw new InvalidTransitionException(nameof(Payment), Status, to);
        }

        _transitions.Add(new PaymentTransition(Status, to));
        Status = to;
    }

    private Money Sum(IEnumerable<Refund> refunds) =>
        refunds.Aggregate(Money.Zero(AuthorizedAmount.Currency), (total, refund) => total + refund.Amount);
}
