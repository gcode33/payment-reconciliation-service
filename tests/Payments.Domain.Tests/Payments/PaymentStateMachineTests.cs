using Payments.Domain.Payments;

namespace Payments.Domain.Tests.Payments;

// Invariant I6: the table below is the spec (SPEC §7.2). Every other pair must be refused.
public sealed class PaymentStateMachineTests
{
    private static readonly HashSet<(PaymentStatus From, PaymentStatus To)> AllowedPaymentEdges =
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
        // A refund that fails after succeeding moves the payment back (DECISION 0004).
        (PaymentStatus.PartiallyRefunded, PaymentStatus.Captured),
        (PaymentStatus.Refunded, PaymentStatus.PartiallyRefunded),
        (PaymentStatus.Refunded, PaymentStatus.Captured),
    ];

    private static readonly HashSet<(OperationStatus From, OperationStatus To)> AllowedOperationEdges =
    [
        (OperationStatus.Pending, OperationStatus.Succeeded),
        (OperationStatus.Pending, OperationStatus.Failed),
        (OperationStatus.Pending, OperationStatus.Unknown),
        (OperationStatus.Unknown, OperationStatus.Succeeded),
        (OperationStatus.Unknown, OperationStatus.Failed),
    ];

    public static TheoryData<PaymentStatus, PaymentStatus> AllPaymentPairs()
    {
        var data = new TheoryData<PaymentStatus, PaymentStatus>();
        foreach (var from in Enum.GetValues<PaymentStatus>())
        {
            foreach (var to in Enum.GetValues<PaymentStatus>())
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    public static TheoryData<OperationStatus, OperationStatus> AllOperationPairs()
    {
        var data = new TheoryData<OperationStatus, OperationStatus>();
        foreach (var from in Enum.GetValues<OperationStatus>())
        {
            foreach (var to in Enum.GetValues<OperationStatus>())
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllPaymentPairs))]
    public void Payment_edges_match_the_spec_exactly(PaymentStatus from, PaymentStatus to) =>
        Assert.Equal(AllowedPaymentEdges.Contains((from, to)), PaymentStateMachine.IsAllowed(from, to));

    [Theory]
    [MemberData(nameof(AllOperationPairs))]
    public void Capture_edges_match_the_spec_exactly(OperationStatus from, OperationStatus to) =>
        Assert.Equal(AllowedOperationEdges.Contains((from, to)), PaymentStateMachine.IsAllowed(from, to, allowLateFailure: false));

    [Theory]
    [MemberData(nameof(AllOperationPairs))]
    public void Refund_edges_add_only_late_failure(OperationStatus from, OperationStatus to)
    {
        var expected = AllowedOperationEdges.Contains((from, to))
            || (from, to) == (OperationStatus.Succeeded, OperationStatus.Failed);
        Assert.Equal(expected, PaymentStateMachine.IsAllowed(from, to, allowLateFailure: true));
    }
}
