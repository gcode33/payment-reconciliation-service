namespace Payments.Domain.Payments;

/// <summary>Status of one capture or refund sent to the provider (SPEC §7.2).</summary>
public enum OperationStatus
{
    Pending = 1,
    Succeeded,
    Failed,
    Unknown,
}
