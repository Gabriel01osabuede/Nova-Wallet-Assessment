namespace NovaWallet.Api.Infrastructure.Authentication;

public sealed class CurrentCaller(IHttpContextAccessor accessor)
{
    public Guid SubjectId => Guid.Parse(accessor.HttpContext!.User.FindFirst("sub")!.Value);
    public Guid CustomerId => Guid.Parse(accessor.HttpContext!.User.FindFirst("customer_id")!.Value);
}
