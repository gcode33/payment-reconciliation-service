namespace Payments.Domain;

// Starts at 1 so that default(Currency) is not a valid currency (SPEC §7.1).
public enum Currency
{
    CAD = 1,
    USD = 2,
}
