namespace NovaWallet.Api.Common;

public static class Pagination
{
    public static bool Valid(int page, int pageSize) => page >= 1 && pageSize is >= 1 and <= 100 &&
        (long)(page - 1) * pageSize <= int.MaxValue;
    public static int Offset(int page, int pageSize) => checked((page - 1) * pageSize);
}
