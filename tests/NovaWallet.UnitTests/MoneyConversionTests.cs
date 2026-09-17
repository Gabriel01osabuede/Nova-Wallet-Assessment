using System.Text.Json;
using NovaWallet.Api.Common.Money;
using Xunit;

namespace NovaWallet.UnitTests;

public sealed class MoneyConversionTests
{
    [Theory]
    [InlineData(0L, "0")]
    [InlineData(1L, "0.01")]
    [InlineData(1_000_050L, "10000.50")]
    [InlineData(long.MaxValue, "92233720368547758.07")]
    public void PresentsKoboAsExactDecimalNaira(long kobo, string expected)
    {
        var naira = MoneyConversion.KoboToNaira(kobo);
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), naira);
        Assert.Equal(kobo, checked((long)(naira * 100m)));
    }

    [Theory]
    [InlineData("10000", 1_000_000L)]
    [InlineData("10000.50", 1_000_050L)]
    [InlineData("10000.5", 1_000_050L)]
    [InlineData("10000.00", 1_000_000L)]
    [InlineData("0.01", 1L)]
    [InlineData("0.1", 10L)]
    [InlineData("92233720368547758.07", long.MaxValue)]
    public void ConvertsNairaExactly(string input, long expected)
    {
        Assert.True(MoneyConversion.TryNairaToKobo(input, out var kobo));
        Assert.Equal(expected, kobo);
        Assert.Equal(expected, JsonSerializer.Deserialize<NairaAmount>(input).Kobo);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0.00")]
    [InlineData("-1")]
    [InlineData("0.001")]
    [InlineData("10000.001")]
    [InlineData("1.000")]
    [InlineData("92233720368547758.08")]
    [InlineData("92233720368547759")]
    [InlineData("1e4")]
    [InlineData("1e-100")]
    public void RejectsInvalidPrecisionOrCapacityWithoutRounding(string input)
    {
        Assert.False(MoneyConversion.TryNairaToKobo(input, out _));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NairaAmount>(input));
    }

    [Theory]
    [InlineData("\"10000\"")]
    [InlineData("null")]
    [InlineData("{}")]
    public void RequiresJsonNumber(string input) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NairaAmount>(input));

    [Fact]
    public void SerializationPreservesMaximumAndOneKobo()
    {
        Assert.Equal("92233720368547758.07", JsonSerializer.Serialize(new NairaAmount(long.MaxValue)));
        Assert.Equal("0.01", JsonSerializer.Serialize(new NairaAmount(1)));
    }
}
