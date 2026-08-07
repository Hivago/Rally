namespace RallyAPI.SharedKernel.Security;

/// <summary>
/// Field-level encryption for sensitive data stored at rest — bank account numbers, PAN, GST
/// numbers, and similar. Use for individual column values, not whole documents/files.
/// </summary>
public interface IFieldEncryptionService
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}
