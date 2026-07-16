using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RallyAPI.Orders.Infrastructure.Services.PayU;
using Xunit;

namespace RallyAPI.Orders.Infrastructure.Tests.PayU;

public class PayUServiceTests
{
    private const string Key = "TESTKEY";
    private const string Salt = "TESTSALT";

    private static PayUService CreateService() =>
        new(
            Options.Create(new PayUOptions
            {
                MerchantKey = Key,
                MerchantSalt = Salt,
                BaseUrl = "https://secure.payu.in",
                SuccessUrl = "https://rally.app/success",
                FailureUrl = "https://rally.app/failure"
            }),
            new HttpClient(),
            NullLogger<PayUService>.Instance);

    private static string Sha512(string input)
    {
        var bytes = SHA512.HashData(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }

    // PayU only offers UPI when `phone` is a bare 10-digit Indian mobile. The stored value is
    // E.164 ("+919876543210"); anything longer suppresses UPI while cards/netbanking still show.
    [Theory]
    [InlineData("+919876543210", "9876543210")]
    [InlineData("919876543210", "9876543210")]
    [InlineData("9876543210", "9876543210")]
    [InlineData("+91 98765 43210", "9876543210")]
    [InlineData("098765-43210", "9876543210")]
    public void GenerateCheckoutParams_NormalizesPhoneToTenDigits(string input, string expected)
    {
        var result = CreateService().GenerateCheckoutParams(
            "TXN1", 499.00m, "Rally Order 123", "Yash", "y@rally.app", input);

        result.Phone.Should().Be(expected);
        result.Phone.Should().HaveLength(10);
    }

    [Fact]
    public void GenerateCheckoutParams_WithEmptyPhone_ReturnsEmpty()
    {
        var result = CreateService().GenerateCheckoutParams(
            "TXN1", 499.00m, "Rally Order 123", "Yash", "y@rally.app", "");

        result.Phone.Should().BeEmpty();
    }

    // The hash MUST be computed over the exact productinfo that is posted, or PayU rejects the
    // whole transaction. Guards against re-introducing a trim-in-hash-only mismatch.
    [Fact]
    public void GenerateCheckoutParams_HashMatchesPostedProductInfo()
    {
        var result = CreateService().GenerateCheckoutParams(
            "TXN1", 499.00m, "  Rally Order 123  ", "Yash", "y@rally.app", "+919876543210");

        result.ProductInfo.Should().Be("Rally Order 123");

        var expectedHash = Sha512(
            $"{Key}|TXN1|499.00|{result.ProductInfo}|Yash|y@rally.app|||||||||||{Salt}");
        result.Hash.Should().Be(expectedHash);
    }

    [Fact]
    public void GenerateCheckoutParams_BuildsPaymentUrlAndAmountFormat()
    {
        var result = CreateService().GenerateCheckoutParams(
            "TXN1", 499m, "Rally Order 123", "Yash", "y@rally.app", "+919876543210");

        result.Amount.Should().Be("499.00");
        result.PayUBaseUrl.Should().Be("https://secure.payu.in/_payment");
        result.Key.Should().Be(Key);
    }
}
