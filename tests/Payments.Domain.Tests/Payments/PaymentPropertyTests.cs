using CsCheck;
using Payments.Domain.Payments;

namespace Payments.Domain.Tests.Payments;

// Invariants I4 and I6 under random sequences of captures, refunds and provider outcomes.
public sealed class PaymentPropertyTests
{
    private enum Step
    {
        StartCapture,
        CaptureOutcome,
        StartRefund,
        RefundOutcome,
        Void,
    }

    private static readonly Gen<OperationStatus> Outcomes =
        Gen.OneOfConst(OperationStatus.Succeeded, OperationStatus.Failed, OperationStatus.Unknown);

    private static readonly Gen<(Step Step, long Cents, OperationStatus Outcome, int Pick)> Steps =
        Gen.Select(Gen.Enum<Step>(), Gen.Long[1, 12_000], Outcomes, Gen.Int[0, 20]);

    [Fact]
    public void Random_operations_never_break_the_money_limits_or_the_transition_table() =>
        Gen.Select(Gen.Long[1, 10_000], Steps.Array[0, 40]).Sample((authorized, steps) =>
        {
            var payment = Payment.StartAuthorization(Guid.NewGuid(), Money.Of(authorized, Currency.CAD));
            payment.MarkAuthorized();

            foreach (var (step, cents, outcome, pick) in steps)
            {
                try
                {
                    Apply(payment, step, Money.Of(cents, Currency.CAD), outcome, pick);
                }
                catch (DomainException)
                {
                    // Refused operations are fine; breaking an invariant is not.
                }

                AssertInvariants(payment);
            }
        });

    private static void Apply(Payment payment, Step step, Money amount, OperationStatus outcome, int pick)
    {
        switch (step)
        {
            case Step.StartCapture:
                payment.StartCapture(Guid.NewGuid(), amount);
                break;
            case Step.CaptureOutcome:
                payment.RecordCaptureOutcome(outcome);
                break;
            case Step.StartRefund:
                payment.StartRefund(Guid.NewGuid(), amount);
                break;
            case Step.RefundOutcome when payment.Refunds.Count > 0:
                payment.RecordRefundOutcome(payment.Refunds[pick % payment.Refunds.Count].Id, outcome);
                break;
            case Step.Void:
                payment.Void();
                break;
        }
    }

    private static void AssertInvariants(Payment payment)
    {
        var zero = Money.Zero(Currency.CAD);

        // I4: captures never exceed the authorization; refunds never exceed the capture.
        Assert.True(payment.CapturedAmount <= payment.AuthorizedAmount);
        Assert.True(payment.RefundedAmount <= payment.CapturedAmount);
        Assert.True(payment.RefundableAmount >= zero);

        // The status always agrees with the money.
        var expected = payment.CapturedAmount.IsZero
            ? payment.Status
            : payment.RefundedAmount.IsZero
                ? PaymentStatus.Captured
                : payment.RefundedAmount < payment.CapturedAmount
                    ? PaymentStatus.PartiallyRefunded
                    : PaymentStatus.Refunded;
        Assert.Equal(expected, payment.Status);

        // I6: every recorded change is an allowed edge.
        foreach (var transition in payment.Transitions.Skip(1))
        {
            Assert.True(PaymentStateMachine.IsAllowed(transition.From!.Value, transition.To));
        }
    }
}
