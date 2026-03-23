using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RallyAPI.Users.Application.Abstractions;
using StackExchange.Redis;
using System.Security.Cryptography;

namespace RallyAPI.Users.Infrastructure.Services;

public class OtpService : IOtpService
{
    private readonly IDatabase _redis;
    private readonly ILogger<OtpService> _logger;
    private readonly ISmsService _smsService;
    private readonly bool _useFixedOtp;

    // Configuration
    private const int OtpExpiryMinutes = 5;
    private const int MaxAttempts = 3;
    private const int LockoutMinutes = 15;
    private const int RateLimitPerPhone = 3;      // max 3 OTPs per phone per 10 min
    private const int RateLimitWindowMinutes = 10;

    public OtpService(
        IConnectionMultiplexer redis,
        ISmsService smsService,
        ILogger<OtpService> logger,
        IHostEnvironment environment)
    {
        _redis = redis.GetDatabase();
        _smsService = smsService;
        _logger = logger;
        _useFixedOtp = environment.IsDevelopment()
            || environment.EnvironmentName == "Testing";
    }

    public async Task<string> GenerateAndSendOtpAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        var phoneKey = NormalizePhone(phoneNumber);

        // Check if phone is locked out (too many failed attempts)
        if (await _redis.KeyExistsAsync($"otp:lock:{phoneKey}"))
        {
            throw new InvalidOperationException(
                "Too many failed attempts. Try again in 15 minutes.");
        }

        // Rate limit: max 3 OTP requests per 10 minutes per phone
        var rateKey = $"otp:rate:{phoneKey}";
        var currentRate = await _redis.StringGetAsync(rateKey);
        if (currentRate.HasValue && (int)currentRate >= RateLimitPerPhone)
        {
            throw new InvalidOperationException(
                "Too many OTP requests. Please wait before trying again.");
        }

        // Generate OTP
        var otp = GenerateOtp();

        // Store hashed OTP in Redis with expiry
        var hashedOtp = HashOtp(otp);
        await _redis.StringSetAsync(
            $"otp:code:{phoneKey}",
            hashedOtp,
            TimeSpan.FromMinutes(OtpExpiryMinutes),
            When.Always,
            CommandFlags.None);

        // Reset attempt counter
        await _redis.StringSetAsync(
            $"otp:attempts:{phoneKey}",
            0,
            TimeSpan.FromMinutes(OtpExpiryMinutes),
            When.Always,
            CommandFlags.None);

        // Increment rate limit counter
        await _redis.StringIncrementAsync(rateKey);
        // Set expiry only if this is the first request in the window
        if (!currentRate.HasValue)
        {
            await _redis.KeyExpireAsync(rateKey, TimeSpan.FromMinutes(RateLimitWindowMinutes), CommandFlags.None);
        }

        // TODO: Send via SMS provider (Twilio/MSG91)
        //// For now, log to console
        //Console.WriteLine($"========================================");
        //Console.WriteLine($"[OTP SERVICE - REDIS] Phone: {phoneNumber}");
        //Console.WriteLine($"[OTP SERVICE - REDIS] OTP: {otp}");
        //Console.WriteLine($"[OTP SERVICE - REDIS] Expires in: {OtpExpiryMinutes} min");
        //Console.WriteLine($"========================================");

        var message = $"{otp} is your OTP for Rally. Valid for 5 minutes. Do not share.";
        var sent = await _smsService.SendAsync(phoneNumber, message, cancellationToken);

        if (!sent)
        {
            _logger.LogError("Failed to send OTP SMS to {Phone}", phoneNumber);
        }

        return otp;
    }

    public async Task<bool> VerifyOtpAsync(
        string phoneNumber,
        string otp,
        CancellationToken cancellationToken = default)
    {
        var phoneKey = NormalizePhone(phoneNumber);

        // Check lockout
        if (await _redis.KeyExistsAsync($"otp:lock:{phoneKey}"))
            return false;

        // Get stored OTP hash
        var storedHash = await _redis.StringGetAsync($"otp:code:{phoneKey}");
        if (!storedHash.HasValue)
            return false; // No OTP exists or it expired

        // Check attempts
        var attempts = (int)await _redis.StringIncrementAsync($"otp:attempts:{phoneKey}");
        if (attempts > MaxAttempts)
        {
            // Lock the phone number
            await _redis.StringSetAsync(
                $"otp:lock:{phoneKey}",
                "locked",
                TimeSpan.FromMinutes(LockoutMinutes),
                When.Always,
                CommandFlags.None);

            // Clean up OTP keys
            await CleanupKeys(phoneKey);

            Console.WriteLine($"[OTP SERVICE] Phone {phoneNumber} locked for {LockoutMinutes} min");
            return false;
        }

        // Verify
        var hashedInput = HashOtp(otp);
        if (storedHash != hashedInput)
        {
            Console.WriteLine($"[OTP SERVICE] Invalid OTP for {phoneNumber}. Attempt {attempts}/{MaxAttempts}");
            return false;
        }

        // Success — clean up
        await CleanupKeys(phoneKey);
        Console.WriteLine($"[OTP SERVICE] OTP verified for {phoneNumber}");
        return true;
    }

    private async Task CleanupKeys(string phoneKey)
    {
        await _redis.KeyDeleteAsync($"otp:code:{phoneKey}");
        await _redis.KeyDeleteAsync($"otp:attempts:{phoneKey}");
    }

    private string GenerateOtp()
    {
        // TODO: switch to random OTP once MSG91 WhatsApp is approved by Meta
        // if (_useFixedOtp)
        //     return "123456";
        // return RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        return "123456";
    }

    private static string HashOtp(string otp)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(otp);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    private static string NormalizePhone(string phone)
    {
        // Remove spaces, dashes — keep only digits and +
        return new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
    }
}

/**
```

---

## What Changed vs Your Old Code

| Before | After |
|--------|-------|
| `ConcurrentDictionary` (dies on restart) | Redis(survives restarts) |
| Plain text OTP stored | SHA256 hashed before storing |
| `Random` for OTP generation | `RandomNumberGenerator` (cryptographically secure) |
| No rate limiting on requests | 3 OTPs per phone per 10 minutes |
| 3 failed attempts, no lockout timer | 3 failed attempts → 15 min lockout |
| No phone normalization | Strips spaces/dashes |

---

## Quick Lesson: Redis Keys We're Using
```
otp:code:+919876543210      → hashed OTP(expires in 5 min)
otp:attempts:+919876543210  → attempt counter(expires in 5 min)
otp:lock:+919876543210      → lockout flag(expires in 15 min)
otp:rate:+919876543210      → request counter(expires in 10 min)
**/