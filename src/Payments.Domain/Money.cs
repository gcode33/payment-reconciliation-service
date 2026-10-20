using System.Globalization;

namespace Payments.Domain;

/// <summary>
/// An exact amount of money: a whole number of minor units (cents) in one currency (SPEC §7.1).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>No <c>double</c> or <c>decimal</c> anywhere in this type.</item>
/// <item>Arithmetic is <c>checked</c>: overflow throws <see cref="OverflowException"/>.</item>
/// <item>Combining or comparing two currencies throws <see cref="CurrencyMismatchException"/>.</item>
/// <item>Only defined <see cref="Domain.Currency"/> values are accepted.</item>
/// </list>
/// </remarks>
public readonly record struct Money : IComparable<Money>
{
    private Money(long minorUnits, Currency currency)
    {
        MinorUnits = minorUnits;
        Currency = currency;
    }

    public long MinorUnits { get; }

    public Currency Currency { get; }

    public bool IsZero => MinorUnits == 0;

    public bool IsPositive => MinorUnits > 0;

    public bool IsNegative => MinorUnits < 0;

    /// <summary>Any amount, including zero and negatives (ledger balances).</summary>
    public static Money Of(long minorUnits, Currency currency)
    {
        if (!Enum.IsDefined(currency))
        {
            throw new ArgumentOutOfRangeException(nameof(currency), currency, "Unsupported currency.");
        }

        return new Money(minorUnits, currency);
    }

    public static Money Zero(Currency currency) => Of(0, currency);

    /// <summary>An amount that must be greater than zero (amounts coming in through the API).</summary>
    public static Money Positive(long minorUnits, Currency currency)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minorUnits);
        return Of(minorUnits, currency);
    }

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(checked(MinorUnits + other.MinorUnits), Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(checked(MinorUnits - other.MinorUnits), Currency);
    }

    public Money Negate() => new(checked(-MinorUnits), Currency);

    public int CompareTo(Money other)
    {
        EnsureSameCurrency(other);
        return MinorUnits.CompareTo(other.MinorUnits);
    }

    /// <summary>Formats as "CAD 10.05" or "USD -0.50".</summary>
    public override string ToString()
    {
        // Work with the magnitude as ulong: Math.Abs(long.MinValue) would overflow.
        var magnitude = MinorUnits < 0 ? (ulong)(-(MinorUnits + 1)) + 1 : (ulong)MinorUnits;
        var sign = MinorUnits < 0 ? "-" : "";
        return string.Create(CultureInfo.InvariantCulture, $"{Currency} {sign}{magnitude / 100}.{magnitude % 100:D2}");
    }

    public static Money operator +(Money left, Money right) => left.Add(right);

    public static Money operator -(Money left, Money right) => left.Subtract(right);

    public static Money operator -(Money value) => value.Negate();

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
        {
            throw new CurrencyMismatchException(Currency, other.Currency);
        }
    }
}
