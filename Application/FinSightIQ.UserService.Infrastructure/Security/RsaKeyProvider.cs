using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FinSightIQ.UserService.Infrastructure.Security;

/// <summary>
/// Manages the RSA keypair used for signing and validating RS256 JWT tokens.
/// </summary>
public interface IRsaKeyProvider
{
    RsaSecurityKey SigningKey { get; }
    RsaSecurityKey ValidationKey { get; }
    string KeyId { get; }
    string ExportPublicKeyPem();
}

public sealed class RsaKeyProvider : IRsaKeyProvider, IDisposable
{
    private readonly RSA _rsa;
    private readonly RsaSecurityKey _signingKey;
    private readonly RsaSecurityKey _validationKey;
    private readonly string _keyId;

    public RsaKeyProvider(
        IOptions<JwtOptions> options,
        IHostEnvironment environment,
        ILogger<RsaKeyProvider> logger)
    {
        JwtOptions jwtOptions = options.Value;
        _keyId = jwtOptions.KeyId;

        _rsa = RSA.Create(2048);

        if (!string.IsNullOrWhiteSpace(jwtOptions.PrivateKeyPem))
        {
            string pem = ResolvePem(jwtOptions.PrivateKeyPem);
            _rsa.ImportFromPem(pem);
            logger.LogInformation("Imported RSA private signing key from configuration. KeyId={KeyId}", _keyId);
        }
        else if (environment.IsDevelopment())
        {
            logger.LogWarning("No Jwt:PrivateKeyPem configured. Generated ephemeral in-memory RSA 2048 keypair for Development. KeyId={KeyId}", _keyId);
        }
        else
        {
            throw new InvalidOperationException("Jwt:PrivateKeyPem must be provided in production and staging environments.");
        }

        _signingKey = new RsaSecurityKey(_rsa) { KeyId = _keyId };

        // Validation key uses only public parameters
        RSA pubRsa = RSA.Create();
        pubRsa.ImportRSAPublicKey(_rsa.ExportRSAPublicKey(), out _);
        _validationKey = new RsaSecurityKey(pubRsa) { KeyId = _keyId };
    }

    public RsaSecurityKey SigningKey => _signingKey;
    public RsaSecurityKey ValidationKey => _validationKey;
    public string KeyId => _keyId;

    public string ExportPublicKeyPem()
    {
        return _rsa.ExportSubjectPublicKeyInfoPem();
    }

    private static string ResolvePem(string pemOrPath)
    {
        if (File.Exists(pemOrPath))
        {
            return File.ReadAllText(pemOrPath);
        }

        return pemOrPath;
    }

    public void Dispose()
    {
        _rsa.Dispose();
    }
}
