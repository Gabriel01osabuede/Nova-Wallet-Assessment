using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace NovaWallet.Api.Infrastructure.Authentication;

public static class DemoTokens
{
    public static string Mint(IConfiguration config, Guid subject, string role, DateTimeOffset now)
    {
        if (subject == Guid.Empty || role is not ("Customer" or "FundingSystem" or "Auditor" or "Admin"))
            throw new ArgumentException("Supply a non-empty actor GUID and a supported demo role.");
        var claims = new List<Claim> { new("sub", subject.ToString("D")), new("role", role) };
        if (role == "Customer") claims.Add(new Claim("customer_id", subject.ToString("D")));
        var token = new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"], claims,
            now.UtcDateTime, now.AddHours(1).UtcDateTime,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:SigningKey"]!)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
