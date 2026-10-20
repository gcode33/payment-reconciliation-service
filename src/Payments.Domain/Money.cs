namespace Payments.Domain;

/// <summary>
/// An exact amount of money: a whole number of minor units (cents) in one currency (SPEC §7.1).
/// </summary>
/// <remarks>
/// PLAN step 1.1: George implements the members below until every test in
/// <c>tests/Payments.Domain.Tests</c> passes. Rules:
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

    public bool IsZero => throw new NotImplementedException();

    public bool IsPositive => throw new NotImplementedException();

    public bool IsNegative => throw new NotImplementedException();

    /// <summary>Any amount, including zero and negatives (ledger balances).</summary>
    public static Money Of(long minorUnits, Currency currency) => throw new NotImplementedException();

    public static Money Zero(Currency currency) => throw new NotImplementedException();

    /// <summary>An amount that must be greater than zero (amounts coming in through the API).</summary>
    public static Money Positive(long minorUnits, Currency currency) => throw new NotImplementedException();

    public Money Add(Money other) => throw new NotImplementedException();

    public Money Subtract(Money other) => throw new NotImplementedException();

    public Money Negate() => throw new NotImplementedException();

    public int CompareTo(Money other) => throw new NotImplementedException();

    /// <summary>Formats as "CAD 10.05" or "USD -0.50".</summary>
    public override string ToString() => throw new NotImplementedException();

    public static Money operator +(Money left, Money right) => left.Add(right);

    public static Money operator -(Money left, Money right) => left.Subtract(right);

    public static Money operator -(Money value) => value.Negate();

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;
}
