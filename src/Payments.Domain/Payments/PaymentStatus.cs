namespace Payments.Domain.Payments;

// Starts at 1 so that default(PaymentStatus) is not a valid status.
public enum PaymentStatus
{
    Authorizing = 1,
    Authorized,
    Declined,
    Unknown,
    Voided,
    Captured,
    PartiallyRefunded,
    Refunded,
}
