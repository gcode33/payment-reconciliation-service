namespace Payments.Domain.Payments;

/// <summary>One recorded status change. <c>From</c> is null for the payment's creation.</summary>
public sealed record PaymentTransition(PaymentStatus? From, PaymentStatus To);
