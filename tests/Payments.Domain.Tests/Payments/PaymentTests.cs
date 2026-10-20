using Payments.Domain.Payments;

namespace Payments.Domain.Tests.Payments;

public sealed class PaymentTests
{
    private static Money Cad(long cents) => Money.Of(cents, Currency.CAD);

    private static Payment Authorized(long cents = 1000)
    {
        var payment = Payment.StartAuthorization(Guid.NewGuid(), Cad(cents));
        payment.MarkAuthorized();
        return payment;
    }

    private static Payment Captured(long cents = 1000)
    {
        var payment = Authorized(cents);
        payment.StartCapture(Guid.NewGuid(), Cad(cents));
        payment.RecordCaptureOutcome(OperationStatus.Succeeded);
        return payment;
    }

    [Fact]
    public void Starting_an_authorization_records_the_first_transition()
    {
        var payment = Payment.StartAuthorization(Guid.NewGuid(), Cad(1000));

        Assert.Equal(PaymentStatus.Authorizing, payment.Status);
        Assert.Equal([new PaymentTransition(null, PaymentStatus.Authorizing)], payment.Transitions);
        Assert.Null(payment.Capture);
        Assert.Empty(payment.Refunds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void An_authorization_must_be_for_a_positive_amount(long cents) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Payment.StartAuthorization(Guid.NewGuid(), Cad(cents)));

    [Fact]
    public void Authorizing_can_be_declined()
    {
        var payment = Payment.StartAuthorization(Guid.NewGuid(), Cad(1000));
        payment.MarkDeclined();
        Assert.Equal(PaymentStatus.Declined, payment.Status);
    }

    [Theory]
    [InlineData(true, PaymentStatus.Authorized)]
    [InlineData(false, PaymentStatus.Declined)]
    public void An_unknown_outcome_resolves_to_authorized_or_declined(bool approved, PaymentStatus expected)
    {
        var payment = Payment.StartAuthorization(Guid.NewGuid(), Cad(1000));
        payment.MarkUnknown();

        if (approved)
        {
            payment.MarkAuthorized();
        }
        else
        {
            payment.MarkDeclined();
        }

        Assert.Equal(expected, payment.Status);
        Assert.Equal(
            [PaymentStatus.Authorizing, PaymentStatus.Unknown, expected],
            payment.Transitions.Select(t => t.To));
    }

    [Fact]
    public void A_payment_cannot_be_authorized_twice()
    {
        var payment = Authorized();
        var ex = Assert.Throws<InvalidTransitionException>(payment.MarkAuthorized);
        Assert.Equal(PaymentStatus.Authorized, ex.From);
    }

    [Fact]
    public void An_authorized_payment_can_be_voided()
    {
        var payment = Authorized();
        payment.Void();
        Assert.Equal(PaymentStatus.Voided, payment.Status);
    }

    [Fact]
    public void A_captured_payment_cannot_be_voided() =>
        Assert.Throws<InvalidTransitionException>(Captured().Void);

    [Fact]
    public void A_payment_with_a_capture_in_flight_cannot_be_voided()
    {
        var payment = Authorized();
        payment.StartCapture(Guid.NewGuid(), Cad(1000));
        Assert.ThrowsAny<DomainException>(payment.Void);
    }

    [Fact]
    public void A_full_capture_moves_the_payment_to_captured_only_when_it_succeeds()
    {
        var payment = Authorized(1000);

        var capture = payment.StartCapture(Guid.NewGuid(), Cad(1000));
        Assert.Equal(OperationStatus.Pending, capture.Status);
        Assert.Equal(PaymentStatus.Authorized, payment.Status);

        payment.RecordCaptureOutcome(OperationStatus.Succeeded);
        Assert.Equal(PaymentStatus.Captured, payment.Status);
        Assert.Equal(Cad(1000), payment.CapturedAmount);
    }

    [Fact]
    public void A_partial_capture_captures_only_that_amount()
    {
        var payment = Authorized(1000);
        payment.StartCapture(Guid.NewGuid(), Cad(600));
        payment.RecordCaptureOutcome(OperationStatus.Succeeded);

        Assert.Equal(Cad(600), payment.CapturedAmount);
        Assert.Equal(Cad(600), payment.RefundableAmount);
    }

    [Fact]
    public void Capturing_more_than_was_authorized_is_refused() =>
        Assert.ThrowsAny<DomainException>(() => Authorized(1000).StartCapture(Guid.NewGuid(), Cad(1001)));

    [Fact]
    public void Capturing_in_another_currency_is_refused() =>
        Assert.Throws<CurrencyMismatchException>(
            () => Authorized(1000).StartCapture(Guid.NewGuid(), Money.Of(500, Currency.USD)));

    [Fact]
    public void Capturing_before_authorization_is_refused() =>
        Assert.ThrowsAny<DomainException>(
            () => Payment.StartAuthorization(Guid.NewGuid(), Cad(1000)).StartCapture(Guid.NewGuid(), Cad(1000)));

    [Fact]
    public void A_second_capture_is_refused_while_the_first_is_in_flight()
    {
        var payment = Authorized();
        payment.StartCapture(Guid.NewGuid(), Cad(500));
        Assert.ThrowsAny<DomainException>(() => payment.StartCapture(Guid.NewGuid(), Cad(500)));
    }

    [Fact]
    public void A_failed_capture_can_be_retried()
    {
        var payment = Authorized();
        payment.StartCapture(Guid.NewGuid(), Cad(1000));
        payment.RecordCaptureOutcome(OperationStatus.Failed);
        Assert.Equal(PaymentStatus.Authorized, payment.Status);

        payment.StartCapture(Guid.NewGuid(), Cad(1000));
        payment.RecordCaptureOutcome(OperationStatus.Succeeded);
        Assert.Equal(PaymentStatus.Captured, payment.Status);
    }

    [Fact]
    public void An_unknown_capture_can_still_resolve_to_succeeded()
    {
        var payment = Authorized();
        payment.StartCapture(Guid.NewGuid(), Cad(1000));
        payment.RecordCaptureOutcome(OperationStatus.Unknown);
        Assert.Equal(PaymentStatus.Authorized, payment.Status);

        payment.RecordCaptureOutcome(OperationStatus.Succeeded);
        Assert.Equal(PaymentStatus.Captured, payment.Status);
    }

    [Fact]
    public void A_failed_capture_cannot_later_succeed()
    {
        var payment = Authorized();
        payment.StartCapture(Guid.NewGuid(), Cad(1000));
        payment.RecordCaptureOutcome(OperationStatus.Failed);
        Assert.Throws<InvalidTransitionException>(() => payment.RecordCaptureOutcome(OperationStatus.Succeeded));
    }

    [Fact]
    public void Recording_a_capture_outcome_without_a_capture_is_refused() =>
        Assert.ThrowsAny<DomainException>(() => Authorized().RecordCaptureOutcome(OperationStatus.Succeeded));

    [Fact]
    public void Partial_then_full_refunds_move_through_partially_refunded_to_refunded()
    {
        var payment = Captured(1000);

        var first = payment.StartRefund(Guid.NewGuid(), Cad(400));
        Assert.Equal(PaymentStatus.Captured, payment.Status);
        payment.RecordRefundOutcome(first.Id, OperationStatus.Succeeded);
        Assert.Equal(PaymentStatus.PartiallyRefunded, payment.Status);

        var second = payment.StartRefund(Guid.NewGuid(), Cad(600));
        payment.RecordRefundOutcome(second.Id, OperationStatus.Succeeded);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(Cad(1000), payment.RefundedAmount);
        Assert.Equal(Cad(0), payment.RefundableAmount);
    }

    [Fact]
    public void Refunding_more_than_was_captured_is_refused() =>
        Assert.ThrowsAny<DomainException>(() => Captured(1000).StartRefund(Guid.NewGuid(), Cad(1001)));

    [Fact]
    public void Refunds_in_flight_count_against_the_refundable_amount()
    {
        var payment = Captured(1000);
        payment.StartRefund(Guid.NewGuid(), Cad(600));
        Assert.Equal(Cad(400), payment.RefundableAmount);
        Assert.ThrowsAny<DomainException>(() => payment.StartRefund(Guid.NewGuid(), Cad(500)));
    }

    [Fact]
    public void A_failed_refund_frees_its_amount_and_leaves_the_status_alone()
    {
        var payment = Captured(1000);
        var refund = payment.StartRefund(Guid.NewGuid(), Cad(600));
        payment.RecordRefundOutcome(refund.Id, OperationStatus.Failed);

        Assert.Equal(PaymentStatus.Captured, payment.Status);
        Assert.Equal(Cad(1000), payment.RefundableAmount);
    }

    [Fact]
    public void A_refund_that_fails_after_succeeding_moves_the_payment_back()
    {
        // Stripe's pm_card_refundFail test card does exactly this (SPEC §7.2).
        var payment = Captured(1000);
        var refund = payment.StartRefund(Guid.NewGuid(), Cad(1000));
        payment.RecordRefundOutcome(refund.Id, OperationStatus.Succeeded);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);

        payment.RecordRefundOutcome(refund.Id, OperationStatus.Failed);

        Assert.Equal(PaymentStatus.Captured, payment.Status);
        Assert.Equal(Cad(0), payment.RefundedAmount);
        Assert.Equal(new PaymentTransition(PaymentStatus.Refunded, PaymentStatus.Captured), payment.Transitions[^1]);
    }

    [Fact]
    public void Refunding_before_capture_is_refused() =>
        Assert.ThrowsAny<DomainException>(() => Authorized().StartRefund(Guid.NewGuid(), Cad(100)));

    [Fact]
    public void Recording_an_outcome_for_an_unknown_refund_is_refused() =>
        Assert.ThrowsAny<DomainException>(() => Captured().RecordRefundOutcome(Guid.NewGuid(), OperationStatus.Succeeded));

    [Fact]
    public void A_refund_id_cannot_be_reused()
    {
        var payment = Captured(1000);
        var id = Guid.NewGuid();
        payment.StartRefund(id, Cad(100));
        Assert.ThrowsAny<DomainException>(() => payment.StartRefund(id, Cad(100)));
    }
}
