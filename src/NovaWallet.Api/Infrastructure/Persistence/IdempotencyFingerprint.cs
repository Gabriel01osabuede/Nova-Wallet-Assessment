using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NovaWallet.Api.Infrastructure.Persistence;

public static class IdempotencyFingerprint
{
    public static bool ValidKey(string? key) => key is { Length: >= 1 and <= 128 } &&
        key.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or ':' or '-');
    public static byte[] Transfer(Guid source, Guid destination, long amount) =>
        Hash($"transfer|v1|{source:D}|{destination:D}|{amount.ToString(CultureInfo.InvariantCulture)}");
    public static byte[] Credit(Guid wallet, long amount) =>
        Hash($"credit|v1|{wallet:D}|{amount.ToString(CultureInfo.InvariantCulture)}");
    public static string Resource(string scope, string key) =>
        "novawallet:idem:" + Convert.ToHexString(Hash(scope + "|" + key)).ToLowerInvariant();
    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
