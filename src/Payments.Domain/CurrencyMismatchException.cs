namespace Payments.Domain;

public sealed class CurrencyMismatchException : InvalidOperationException
{
    public CurrencyMismatchException(Currency left, Currency right)
        : base($"Cannot combine {left} and {right} amounts.")
    {
        Left = left;
        Right = right;
    }

    public Currency Left { get; }

    public Currency Right { get; }
}
