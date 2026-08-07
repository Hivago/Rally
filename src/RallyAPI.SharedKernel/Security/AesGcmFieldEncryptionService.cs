using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace RallyAPI.SharedKernel.Security;

/// <summary>
/// AES-256-GCM field-level encryption. Output is a single base64 string: a random 12-byte
/// nonce, the ciphertext, then the 16-byte auth tag — so a tampered or corrupted value fails
/// to decrypt (throws) rather than silently returning garbage. A fresh random nonce is
/// generated per call, so encrypting the same plaintext twice produces different ciphertext —
/// never compare encrypted values for equality, decrypt and compare instead.
/// </summary>
public sealed class AesGcmFieldEncryptionService : IFieldEncryptionService
{
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    private readonly byte[] _key;

    public AesGcmFieldEncryptionService(IOptions<EncryptionOptions> options)
    {
        var keyBase64 = options.Value.Key;
        if (string.IsNullOrWhiteSpace(keyBase64))
            throw new InvalidOperationException(
                "Encryption:Key is not configured. Generate one with `openssl rand -base64 32` " +
                "and set it via the Encryption__Key environment variable — required before any " +
                "endpoint that encrypts sensitive fields (e.g. restaurant onboarding) can start.");

        try
        {
            _key = Convert.FromBase64String(keyBase64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Encryption:Key is not valid base64.", ex);
        }

        if (_key.Length != 32)
            throw new InvalidOperationException(
                $"Encryption:Key must decode to exactly 32 bytes (AES-256) — got {_key.Length}.");
    }

    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSizeBytes];

        using var aesGcm = new AesGcm(_key, TagSizeBytes);
        aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var combined = new byte[NonceSizeBytes + ciphertext.Length + TagSizeBytes];
        Buffer.BlockCopy(nonce, 0, combined, 0, NonceSizeBytes);
        Buffer.BlockCopy(ciphertext, 0, combined, NonceSizeBytes, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, combined, NonceSizeBytes + ciphertext.Length, TagSizeBytes);

        return Convert.ToBase64String(combined);
    }

    public string Decrypt(string ciphertextBase64)
    {
        ArgumentNullException.ThrowIfNull(ciphertextBase64);

        var combined = Convert.FromBase64String(ciphertextBase64);
        if (combined.Length < NonceSizeBytes + TagSizeBytes)
            throw new CryptographicException("Ciphertext is too short to contain a valid nonce and auth tag.");

        var nonce = combined[..NonceSizeBytes];
        var tag = combined[^TagSizeBytes..];
        var ciphertext = combined[NonceSizeBytes..^TagSizeBytes];
        var plaintextBytes = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(_key, TagSizeBytes);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintextBytes);

        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
