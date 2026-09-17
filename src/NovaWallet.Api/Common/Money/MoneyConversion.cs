using System.Globalization;

namespace NovaWallet.Api.Common.Money;

public static class MoneyConversion
{
    // Decimal is used only for exact response presentation; ledger arithmetic stays in long kobo.
    public static decimal KoboToNaira(long kobo) => kobo / 100m;

    // Parse the exact decimal text at the API boundary, using integer arithmetic only.
    // Reject excess precision rather than silently round or truncate customer money.
    public static bool TryNairaToKobo(string naira, out long kobo)
    {
        kobo = 0;
        if (string.IsNullOrEmpty(naira) || naira.Length > 20) return false;
        var parts = naira.Split('.');
        if (parts.Length > 2 || parts[0].Length == 0 ||
            parts[0].Any(c => c is < '0' or > '9') ||
            (parts[0].Length > 1 && parts[0][0] == '0')) return false;
        if (!long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var whole)) return false;
        var minor = 0;
        if (parts.Length == 2)
        {
            var fraction = parts[1];
            if (fraction.Length is < 1 or > 2 || fraction.Any(c => c is < '0' or > '9')) return false;
            minor = (fraction[0] - '0') * 10;
            if (fraction.Length == 2) minor += fraction[1] - '0';
        }
        if (whole > long.MaxValue / 100 ||
            whole == long.MaxValue / 100 && minor > long.MaxValue % 100) return false;
        kobo = checked(whole * 100 + minor);
        return kobo > 0;
    }
}
