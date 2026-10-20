namespace Payments.Domain.Payments;

/// <summary>The allowed edges of the payment and operation state machines (SPEC §7.2).</summary>
public static class PaymentStateMachine
{
    public static bool IsAllowed(PaymentStatus from, PaymentStatus to) => throw new NotImplementedException();

    /// <param name="allowLateFailure">Refunds may fail after succeeding; captures may not.</param>
    public static bool IsAllowed(OperationStatus from, OperationStatus to, bool allowLateFailure) =>
        throw new NotImplementedException();
}
