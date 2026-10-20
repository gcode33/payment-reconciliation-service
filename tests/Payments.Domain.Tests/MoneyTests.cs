namespace Payments.Domain.Tests;

public sealed class MoneyTests
{
    [Fact]
    public void Of_keeps_the_minor_units_and_currency()
    {
        var money = Money.Of(1005, Currency.CAD);

        Assert.Equal(1005, money.MinorUnits);
        Assert.Equal(Currency.CAD, money.Currency);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void Of_rejects_an_undefined_currency(int currency) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Money.Of(100, (Currency)currency));

    [Fact]
    public void Zero_is_zero_and_neither_positive_nor_negative()
    {
        var zero = Money.Zero(Currency.USD);

        Assert.True(zero.IsZero);
        Assert.False(zero.IsPositive);
        Assert.False(zero.IsNegative);
        Assert.Equal(Currency.USD, zero.Currency);
    }

    [Fact]
    public void Positive_accepts_one_cent() =>
        Assert.True(Money.Positive(1, Currency.CAD).IsPositive);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void Positive_rejects_zero_and_negative_amounts(long minorUnits) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Money.Positive(minorUnits, Currency.CAD));

    [Fact]
    public void Positive_rejects_an_undefined_currency() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Money.Positive(100, (Currency)0));

    [Fact]
    public void Adding_the_same_currency_adds_the_minor_units() =>
        Assert.Equal(Money.Of(1500, Currency.CAD), Money.Of(1000, Currency.CAD) + Money.Of(500, Currency.CAD));

    [Fact]
    public void Subtracting_can_go_below_zero() =>
        Assert.Equal(Money.Of(-400, Currency.CAD), Money.Of(600, Currency.CAD) - Money.Of(1000, Currency.CAD));

    [Fact]
    public void Adding_different_currencies_throws_and_names_both()
    {
        var ex = Assert.Throws<CurrencyMismatchException>(
            () => Money.Of(1000, Currency.CAD) + Money.Of(1000, Currency.USD));

        Assert.Equal(Currency.CAD, ex.Left);
        Assert.Equal(Currency.USD, ex.Right);
    }

    [Fact]
    public void Subtracting_different_currencies_throws() =>
        Assert.Throws<CurrencyMismatchException>(() => Money.Of(1000, Currency.CAD) - Money.Of(1, Currency.USD));

    [Fact]
    public void Adding_past_the_largest_long_throws_instead_of_wrapping_around() =>
        Assert.Throws<OverflowException>(() => Money.Of(long.MaxValue, Currency.CAD) + Money.Of(1, Currency.CAD));

    [Fact]
    public void Subtracting_past_the_smallest_long_throws_instead_of_wrapping_around() =>
        Assert.Throws<OverflowException>(() => Money.Of(long.MinValue, Currency.CAD) - Money.Of(1, Currency.CAD));

    [Fact]
    public void Negate_flips_the_sign()
    {
        Assert.Equal(Money.Of(-250, Currency.USD), -Money.Of(250, Currency.USD));
        Assert.True(Money.Of(-1, Currency.USD).IsNegative);
    }

    [Fact]
    public void Negating_the_smallest_long_throws() =>
        Assert.Throws<OverflowException>(() => -Money.Of(long.MinValue, Currency.CAD));

    [Fact]
    public void Equal_amounts_in_different_currencies_are_not_equal() =>
        Assert.NotEqual(Money.Of(100, Currency.CAD), Money.Of(100, Currency.USD));

    [Fact]
    public void Comparison_works_within_one_currency()
    {
        var small = Money.Of(100, Currency.CAD);
        var large = Money.Of(200, Currency.CAD);

        Assert.True(small < large);
        Assert.True(large > small);
        Assert.True(small <= Money.Of(100, Currency.CAD));
        Assert.True(large >= small);
    }

    [Fact]
    public void Comparing_different_currencies_throws() =>
        Assert.Throws<CurrencyMismatchException>(() => Money.Of(100, Currency.CAD) < Money.Of(200, Currency.USD));

    [Theory]
    [InlineData(1005, Currency.CAD, "CAD 10.05")]
    [InlineData(-50, Currency.USD, "USD -0.50")]
    [InlineData(0, Currency.CAD, "CAD 0.00")]
    [InlineData(7, Currency.CAD, "CAD 0.07")]
    [InlineData(123456789, Currency.USD, "USD 1234567.89")]
    // Math.Abs(long.MinValue) overflows, so this case catches a common formatting bug.
    [InlineData(long.MinValue, Currency.CAD, "CAD -92233720368547758.08")]
    public void ToString_shows_the_currency_and_two_decimals(long minorUnits, Currency currency, string expected) =>
        Assert.Equal(expected, Money.Of(minorUnits, currency).ToString());
}
