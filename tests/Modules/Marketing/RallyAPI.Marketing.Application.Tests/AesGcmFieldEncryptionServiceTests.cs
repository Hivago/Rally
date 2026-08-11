using FluentAssertions;
using Microsoft.Extensions.Options;
using RallyAPI.SharedKernel.Security;
using Xunit;

namespace RallyAPI.Marketing.Application.Tests;

public class AesGcmFieldEncryptionServiceTests
{
    private sealed class TestOptions : IOptions<EncryptionOptions>
    {
        public EncryptionOptions Value { get; }
        public TestOptions(EncryptionOptions value) => Value = value;
    }

    private static AesGcmFieldEncryptionService CreateService(string? keyBase64 = null)
    {
        var key = keyBase64 ?? Convert.ToBase64String(new byte[32]); // deterministic test key
        return new AesGcmFieldEncryptionService(new TestOptions(new EncryptionOptions { Key = key }));
    }

    [Fact]
    public void EncryptThenDecrypt_ReturnsOriginalPlaintext()
    {
        var service = CreateService();
        var plaintext = "1234567890123456"; // looks like a bank account number

        var ciphertext = service.Encrypt(plaintext);
        var decrypted = service.Decrypt(ciphertext);

        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public void Encrypt_SamePlaintextTwice_ProducesDifferentCiphertext()
    {
        var service = CreateService();
        var plaintext = "ABCDE1234F";

        var first = service.Encrypt(plaintext);
        var second = service.Encrypt(plaintext);

        first.Should().NotBe(second); // fresh random nonce per call
        service.Decrypt(first).Should().Be(plaintext);
        service.Decrypt(second).Should().Be(plaintext);
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_Throws()
    {
        var service = CreateService();
        var ciphertext = service.Encrypt("sensitive-value");

        var bytes = Convert.FromBase64String(ciphertext);
        bytes[^1] ^= 0xFF; // flip the last byte (inside the auth tag)
        var tampered = Convert.ToBase64String(bytes);

        var act = () => service.Decrypt(tampered);

        act.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }

    [Fact]
    public void Constructor_MissingKey_Throws()
    {
        var act = () => CreateService(keyBase64: "");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Encryption:Key*");
    }

    [Fact]
    public void Constructor_WrongKeyLength_Throws()
    {
        var shortKey = Convert.ToBase64String(new byte[16]); // AES-128 length, not AES-256

        var act = () => CreateService(shortKey);

        act.Should().Throw<InvalidOperationException>().WithMessage("*32 bytes*");
    }
}
