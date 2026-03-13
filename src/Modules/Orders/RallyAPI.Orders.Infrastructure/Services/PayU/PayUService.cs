// File: src/Modules/Orders/RallyAPI.Orders.Infrastructure/Services/PayU/PayUService.cs

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RallyAPI.Orders.Application.Abstractions;

namespace RallyAPI.Orders.Infrastructure.Services.PayU;

public class PayUService : IPayUService
{
    private readonly PayUOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<PayUService> _logger;

    public PayUService(
        IOptions<PayUOptions> options,
        HttpClient httpClient,
        ILogger<PayUService> logger)
    {
        _options = options.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    // ========== Hash Generation ==========

    public PayUCheckoutParams GenerateCheckoutParams(
        string txnId, decimal amount, string productInfo,
        string firstName, string email, string phone)
    {
        var amountStr = amount.ToString("F2");

        // PayU payment hash: key|txnid|amount|productinfo|firstname|email|||||||||SALT
       // var hashString = $"{_options.MerchantKey}|{txnId}|{amountStr}|{productInfo}|{firstName}|{email}|||||||||||{_options.MerchantSalt}";
        var hashString = $"{_options.MerchantKey}|{txnId}|{amountStr}|{productInfo.Trim()}|{firstName}|{email}|||||||||||{_options.MerchantSalt}";
        var hash = ComputeSha512(hashString);

        return new PayUCheckoutParams
        {
            Key = _options.MerchantKey,
            TxnId = txnId,
            Amount = amountStr,
            ProductInfo = productInfo,
            FirstName = firstName,
            Email = email,
            Phone = phone,
            Surl = _options.SuccessUrl,
            Furl = _options.FailureUrl,
            Hash = hash,
            PayUBaseUrl = $"{_options.BaseUrl}/_payment"
        };
    }

    public bool VerifyWebhookHash(Dictionary<string, string> formData)
    {
        try
        {
            var receivedHash = formData.GetValueOrDefault("hash", "");
            if (string.IsNullOrWhiteSpace(receivedHash))
            {
                _logger.LogWarning("PayU webhook: no hash in callback");
                return false;
            }

            var key = formData.GetValueOrDefault("key", "");
            var txnId = formData.GetValueOrDefault("txnid", "");
            var amount = formData.GetValueOrDefault("amount", "");
            var productInfo = formData.GetValueOrDefault("productinfo", "");
            var firstName = formData.GetValueOrDefault("firstname", "");
            var email = formData.GetValueOrDefault("email", "");
            var status = formData.GetValueOrDefault("status", "");
            var udf1 = formData.GetValueOrDefault("udf1", "");
            var udf2 = formData.GetValueOrDefault("udf2", "");
            var udf3 = formData.GetValueOrDefault("udf3", "");
            var udf4 = formData.GetValueOrDefault("udf4", "");
            var udf5 = formData.GetValueOrDefault("udf5", "");

            // Reverse hash: SALT|status||||||udf5|udf4|udf3|udf2|udf1|email|firstname|productinfo|amount|txnid|key
            var reverseHashString = $"{_options.MerchantSalt}|{status}||||||{udf5}|{udf4}|{udf3}|{udf2}|{udf1}|{email}|{firstName}|{productInfo}|{amount}|{txnId}|{key}";
            var expectedHash = ComputeSha512(reverseHashString);

            var isValid = string.Equals(expectedHash, receivedHash, StringComparison.OrdinalIgnoreCase);

            if (!isValid)
            {
                _logger.LogWarning(
                    "PayU webhook hash mismatch for txnId {TxnId}. Expected: {Expected}, Received: {Received}",
                    txnId, expectedHash[..16] + "...", receivedHash[..16] + "...");
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying PayU webhook hash");
            return false;
        }
    }

    // ========== API Calls ==========

    public async Task<PayUVerifyResult?> VerifyPaymentAsync(string txnId)
    {
        try
        {
            var command = "verify_payment";
            var apiHash = ComputeApiHash(command, txnId);

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["key"] = _options.MerchantKey,
                ["command"] = command,
                ["var1"] = txnId,
                ["hash"] = apiHash
            });

            var response = await _httpClient.PostAsync(
                $"{_options.BaseUrl}/merchant/postservice?form=2", content);

            var json = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PayU verify_payment response for {TxnId}: {Response}", txnId, json);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var status = root.GetProperty("status").GetInt32();
            if (status != 1) return null;

            var txnDetails = root.GetProperty("transaction_details").GetProperty(txnId);

            return new PayUVerifyResult
            {
                Status = txnDetails.GetProperty("status").GetString() ?? "",
                PayuId = txnDetails.GetProperty("mihpayid").GetString() ?? "",
                Amount = txnDetails.GetProperty("amt").GetString() ?? "",
                Mode = txnDetails.GetProperty("mode").GetString(),
                BankRefNum = txnDetails.GetProperty("bank_ref_num").GetString(),
                ErrorMessage = txnDetails.TryGetProperty("error_Message", out var err) ? err.GetString() : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling PayU verify_payment for {TxnId}", txnId);
            return null;
        }
    }

    public async Task<PayURefundResult?> RefundTransactionAsync(string payuId, string uniqueToken, decimal amount)
    {
        try
        {
            var command = "cancel_refund_transaction";
            var apiHash = ComputeApiHash(command, payuId);

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["key"] = _options.MerchantKey,
                ["command"] = command,
                ["var1"] = payuId,
                ["var2"] = uniqueToken,
                ["var3"] = amount.ToString("F2"),
                ["hash"] = apiHash
            });

            var response = await _httpClient.PostAsync(
                $"{_options.BaseUrl}/merchant/postservice?form=2", content);

            var json = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PayU refund response for PayuId {PayuId}: {Response}", payuId, json);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new PayURefundResult
            {
                Status = root.GetProperty("status").GetInt32(),
                Message = root.GetProperty("msg").GetString() ?? "",
                RequestId = root.TryGetProperty("request_id", out var rid) ? rid.GetString() : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling PayU refund for PayuId {PayuId}", payuId);
            return null;
        }
    }

    // ========== Helpers ==========

    private string ComputeApiHash(string command, string var1)
    {
        // API hash: key|command|var1|salt
        var hashString = $"{_options.MerchantKey}|{command}|{var1}|{_options.MerchantSalt}";
        return ComputeSha512(hashString);
    }

    private static string ComputeSha512(string input)
    {
        var bytes = SHA512.HashData(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}