namespace RallyAPI.SharedKernel.Security;

public sealed class EncryptionOptions
{
    public const string SectionName = "Encryption";

    /// <summary>
    /// Base64-encoded 32-byte (256-bit) AES key. Generate with: openssl rand -base64 32
    /// Set via the Encryption__Key environment variable — never commit a real key.
    /// </summary>
    public string Key { get; set; } = string.Empty;
}
