namespace Payments.Domain.Payments;

/// <summary>The allowed edges of the payment and operation state machines (SPEC §7.2).</summary>
public static class PaymentStateMachine
{
    private static readonly HashSet<(PaymentStatus From, PaymentStatus To)> PaymentEdges =
    [
        (PaymentStatus.Authorizing, PaymentStatus.Authorized),
        (PaymentStatus.Authorizing, PaymentStatus.Declined),
        (PaymentStatus.Authorizing, PaymentStatus.Unknown),
        (PaymentStatus.Unknown, PaymentStatus.Authorized),
        (PaymentStatus.Unknown, PaymentStatus.Declined),
        (PaymentStatus.Authorized, PaymentStatus.Voided),
        (PaymentStatus.Authorized, PaymentStatus.Captured),
        (PaymentStatus.Captured, PaymentStatus.PartiallyRefunded),
        (PaymentStatus.Captured, PaymentStatus.Refunded),
        (PaymentStatus.PartiallyRefunded, PaymentStatus.Refunded),
        // A refund that fails after succeeding moves the payment back (docs/decisions/0004).
        (PaymentStatus.PartiallyRefunded, PaymentStatus.Captured),
        (PaymentStatus.Refunded, PaymentStatus.PartiallyRefunded),
        (PaymentStatus.Refunded, PaymentStatus.Captured),
    ];

    private static readonly HashSet<(OperationStatus From, OperationStatus To)> OperationEdges =
    [
        (OperationStatus.Pending, OperationStatus.Succeeded),
        (OperationStatus.Pending, OperationStatus.Failed),
        (OperationStatus.Pending, OperationStatus.Unknown),
        (OperationStatus.Unknown, OperationStatus.Succeeded),
        (OperationStatus.Unknown, OperationStatus.Failed),
    ];

    public static bool IsAllowed(PaymentStatus from, PaymentStatus to) => PaymentEdges.Contains((from, to));

    /// <param name="allowLateFailure">Refunds may fail after succeeding; captures may not.</param>
    public static bool IsAllowed(OperationStatus from, OperationStatus to, bool allowLateFailure) =>
        OperationEdges.Contains((from, to))
        || (allowLateFailure && from == OperationStatus.Succeeded && to == OperationStatus.Failed);
}
