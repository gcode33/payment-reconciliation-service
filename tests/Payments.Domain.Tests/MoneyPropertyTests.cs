using CsCheck;

namespace Payments.Domain.Tests;

// Property tests: CsCheck tries many random inputs and shrinks any failure to the smallest example.
public sealed class MoneyPropertyTests
{
    // Kept well inside the long range so additions in these properties cannot overflow.
    private static readonly Gen<long> Amounts = Gen.Long[-1_000_000_000_000_000, 1_000_000_000_000_000];

    private static readonly Gen<Currency> Currencies = Gen.OneOfConst(Currency.CAD, Currency.USD);

    [Fact]
    public void Adding_then_subtracting_gives_back_the_original() =>
        Gen.Select(Amounts, Amounts, Currencies).Sample((a, b, currency) =>
        {
            var start = Money.Of(a, currency);
            var other = Money.Of(b, currency);
            Assert.Equal(start, start + other - other);
        });

    [Fact]
    public void Addition_order_does_not_matter() =>
        Gen.Select(Amounts, Amounts, Currencies).Sample((a, b, currency) =>
            Assert.Equal(Money.Of(a, currency) + Money.Of(b, currency), Money.Of(b, currency) + Money.Of(a, currency)));

    [Fact]
    public void Zero_changes_nothing() =>
        Gen.Select(Amounts, Currencies).Sample((a, currency) =>
            Assert.Equal(Money.Of(a, currency), Money.Of(a, currency) + Money.Zero(currency)));

    [Fact]
    public void Mixed_currencies_always_throw() =>
        Gen.Select(Amounts, Amounts).Sample((a, b) =>
        {
            Assert.Throws<CurrencyMismatchException>(() => Money.Of(a, Currency.CAD) + Money.Of(b, Currency.USD));
            Assert.Throws<CurrencyMismatchException>(() => Money.Of(a, Currency.USD) - Money.Of(b, Currency.CAD));
        });

    [Fact]
    public void Exactly_one_of_zero_positive_negative_is_true() =>
        Gen.Select(Amounts, Currencies).Sample((a, currency) =>
        {
            var money = Money.Of(a, currency);
            Assert.Equal(1, (money.IsZero ? 1 : 0) + (money.IsPositive ? 1 : 0) + (money.IsNegative ? 1 : 0));
        });
}
