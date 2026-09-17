using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NovaWallet.Api.Common.Money;

[JsonConverter(typeof(NairaAmountJsonConverter))]
public readonly record struct NairaAmount(long Kobo);

public sealed class NairaAmountJsonConverter : JsonConverter<NairaAmount>
{
    public override NairaAmount Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Number)
            throw new JsonException("Amount must be a numeric naira amount.");
        using var document = JsonDocument.ParseValue(ref reader);
        if (!MoneyConversion.TryNairaToKobo(document.RootElement.GetRawText(), out var kobo))
            throw new JsonException("Supply a positive fixed-point naira amount with at most two decimal places within the kobo range.");
        return new NairaAmount(kobo);
    }

    public override void Write(Utf8JsonWriter writer, NairaAmount value, JsonSerializerOptions options)
    {
        if (value.Kobo <= 0) throw new JsonException("Amount must be positive.");
        var whole = (value.Kobo / 100).ToString(CultureInfo.InvariantCulture);
        var fraction = (value.Kobo % 100).ToString("D2", CultureInfo.InvariantCulture);
        writer.WriteRawValue(whole + "." + fraction);
    }
}
