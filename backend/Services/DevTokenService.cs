using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace backend.Services;

/// <summary>
/// Issues short-lived signed JWTs for local development only, so the API's
/// client-credentials bearer auth can be exercised without a real external
/// identity provider (e.g. Azure AD). Never enabled outside Development.
/// </summary>
public class DevTokenService
{
    private readonly SymmetricSecurityKey _signingKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly string _clientId;
    private readonly string _clientSecret;

    public DevTokenService(IConfiguration configuration)
    {
        var signingKeyValue = configuration["Authentication:DevSigningKey"];
        if (string.IsNullOrWhiteSpace(signingKeyValue))
        {
            signingKeyValue = "dev-only-signing-key-do-not-use-in-production-1234567890";
        }
        _signingKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(signingKeyValue));

        _issuer = configuration["Authentication:DevIssuer"];
        if (string.IsNullOrWhiteSpace(_issuer))
        {
            _issuer = "https://localhost:7011/";
        }

        _audience = configuration["Authentication:Audience"];
        if (string.IsNullOrWhiteSpace(_audience))
        {
            _audience = "network-monitoring-api";
        }

        _clientId = configuration["Authentication:DevClientId"];
        if (string.IsNullOrWhiteSpace(_clientId))
        {
            _clientId = "dev-client";
        }

        _clientSecret = configuration["Authentication:DevClientSecret"];
        if (string.IsNullOrWhiteSpace(_clientSecret))
        {
            _clientSecret = "dev-secret";
        }
    }

    public bool ValidateClientCredentials(string? clientId, string? clientSecret, string? grantType)
    {
        return grantType == "client_credentials"
            && clientId == _clientId
            && clientSecret == _clientSecret;
    }

    public (string AccessToken, int ExpiresInSeconds) IssueToken()
    {
        const int expiresInSeconds = 3600;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, _clientId),
            new Claim(JwtRegisteredClaimNames.Aud, _audience),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddSeconds(expiresInSeconds),
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        return (accessToken, expiresInSeconds);
    }

    public SymmetricSecurityKey SigningKey => _signingKey;

    public string Issuer => _issuer;
}
