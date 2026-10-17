using System.Globalization;
using EventPlanner.Desktop.Localization;

namespace EventPlanner.Tests.Unit.Localization;

public sealed class MoneyTextTests
{
    [Theory]
    [InlineData("1250,50", "EUR", 125050L)]
    [InlineData("1 250,5", "EUR", 125050L)]
    [InlineData("1250.50", "EUR", 125050L)]
    [InlineData("0", "EUR", 0L)]
    [InlineData("1500", "JPY", 1500L)]
    [InlineData("1,250", "KWD", 1250L)]
    public void Parses_amounts_exactly_into_minor_units(string text, string currency, long expected)
    {
        using var culture = new CultureScope("sk-SK");

        Assert.True(MoneyText.TryParse(text, currency, out var amount));
        Assert.Equal(expected, amount);
    }

    [Theory]
    [InlineData("12,345", "EUR")] // more decimals than the currency has: rejected, not rounded
    [InlineData("-5", "EUR")]
    [InlineData("abc", "EUR")]
    [InlineData("1,5", "JPY")]
    public void Rejects_invalid_or_lossy_amounts(string text, string currency)
    {
        using var culture = new CultureScope("sk-SK");

        Assert.False(MoneyText.TryParse(text, currency, out _));
    }

    [Fact]
    public void Empty_text_is_unknown_not_zero()
    {
        Assert.True(MoneyText.TryParse("  ", "EUR", out var amount));
        Assert.Null(amount);
        Assert.Equal(Strings.Money_Unknown, MoneyText.Format((long?)null, "EUR"));
    }

    [Fact]
    public void Formats_with_currency_decimals()
    {
        using var culture = new CultureScope("sk-SK");

        Assert.Equal("1 250,50 EUR", MoneyText.Format(125050, "EUR").Replace(' ', ' '));
        Assert.Equal("1 500 JPY", MoneyText.Format(1500, "JPY").Replace(' ', ' '));
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

        public CultureScope(string name) => CultureInfo.CurrentCulture = new CultureInfo(name);

        public void Dispose() => CultureInfo.CurrentCulture = _previous;
    }
}
