using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NovaWallet.Api.Infrastructure.Persistence;

public sealed class LedgerDesignTimeFactory : IDesignTimeDbContextFactory<LedgerDbContext>
{
    public LedgerDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<LedgerDbContext>()
        .UseSqlServer("Server=localhost;Database=NovaWallet;Integrated Security=true;TrustServerCertificate=true").Options);
}
