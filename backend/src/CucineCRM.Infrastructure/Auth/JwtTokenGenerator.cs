using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CucineCRM.Application.Interfaces;
using CucineCRM.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CucineCRM.Infrastructure.Auth;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _options;

    public JwtTokenGenerator(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public string GenerateToken(Utente utente)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, utente.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, utente.Email),
            new(ClaimTypes.Name, $"{utente.Nome} {utente.Cognome}"),
            // ClaimTypes.Role è quello che [Authorize(Roles = "...")] legge di default in ASP.NET Core
            new(ClaimTypes.Role, utente.Ruolo.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (utente.AgenteId.HasValue)
            claims.Add(new Claim("agenteId", utente.AgenteId.Value.ToString()));

        if (utente.ClienteId.HasValue)
            claims.Add(new Claim("clienteId", utente.ClienteId.Value.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_options.ExpirationHours),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
