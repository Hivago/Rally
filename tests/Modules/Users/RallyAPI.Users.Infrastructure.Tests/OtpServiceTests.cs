using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using StackExchange.Redis;
using System.Security.Cryptography;
using Xunit;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Infrastructure.Services;

namespace RallyAPI.Users.Infrastructure.Tests;

public sealed class OtpServiceTests
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly ISmsService _smsService;
    private readonly ILogger<OtpService> _logger;
    private readonly OtpService _service;

    private const string Phone = "+919876543210";

    public OtpServiceTests()
    {
        _redis = Substitute.For<IConnectionMultiplexer>();
        _db = Substitute.For<IDatabase>();
        _smsService = Substitute.For<ISmsService>();
        _logger = Substitute.For<ILogger<OtpService>>();

        _redis.GetDatabase().Returns(_db);
        _service = new OtpService(_redis, _smsService, _logger);
    }

    // --- GenerateAndSendOtpAsync ---

    [Fact]
    public async Task GenerateAndSendOtp_WhenPhoneIsLockedOut_ShouldThrowInvalidOperationException()
    {
        _db.KeyExistsAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(true));

        var act = async () => await _service.GenerateAndSendOtpAsync(Phone);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Too many failed*");
    }

    [Fact]
    public async Task GenerateAndSendOtp_WhenRateLimitExceeded_ShouldThrowInvalidOperationException()
    {
        _db.KeyExistsAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(false));
        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult((RedisValue)3)); // at the 3-per-10min limit

        var act = async () => await _service.GenerateAndSendOtpAsync(Phone);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Too many OTP requests*");
    }

    [Fact]
    public async Task GenerateAndSendOtp_WhenUnderRateLimit_ShouldSendSmsAndReturnOtp()
    {
        SetupSuccessfulGenerateDefaults();
        _smsService.SendAsync(Phone, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var otp = await _service.GenerateAndSendOtpAsync(Phone);

        otp.Should().Be("123456"); // DEBUG mode always returns 123456
        await _smsService.Received(1).SendAsync(
            Phone,
            Arg.Is<string>(m => m.Contains("123456")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAndSendOtp_WhenFirstRequestInWindow_ShouldSetRateLimitKeyExpiry()
    {
        SetupSuccessfulGenerateDefaults();
        _smsService.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        await _service.GenerateAndSendOtpAsync(Phone);

        // KeyExpireAsync is only called for the rate-limit window on the first request
        await _db.Received(1).KeyExpireAsync(
            Arg.Any<RedisKey>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task GenerateAndSendOtp_WhenSmsFails_ShouldStillReturnOtp()
    {
        SetupSuccessfulGenerateDefaults();
        _smsService.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false)); // SMS provider failure

        var otp = await _service.GenerateAndSendOtpAsync(Phone);

        // OTP is still stored in Redis; operation should not fail
        otp.Should().Be("123456");
    }

    // --- VerifyOtpAsync ---

    [Fact]
    public async Task VerifyOtp_WhenPhoneIsLockedOut_ShouldReturnFalse()
    {
        _db.KeyExistsAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(true));

        var result = await _service.VerifyOtpAsync(Phone, "123456");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyOtp_WhenNoOtpStored_ShouldReturnFalse()
    {
        _db.KeyExistsAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(false));
        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(RedisValue.Null));

        var result = await _service.VerifyOtpAsync(Phone, "123456");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyOtp_WhenCorrectOtp_ShouldReturnTrueAndCleanupRedisKeys()
    {
        var storedHash = (RedisValue)ComputeHash("123456");
        _db.KeyExistsAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(false));
        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(storedHash));
        _db.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(1L)); // first attempt, well under MaxAttempts=3

        var result = await _service.VerifyOtpAsync(Phone, "123456");

        result.Should().BeTrue();
        await _db.Received(2).KeyDeleteAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()); // otp:code + otp:attempts
    }

    [Fact]
    public async Task VerifyOtp_WhenWrongOtp_ShouldReturnFalseWithoutCleanup()
    {
        var storedHash = (RedisValue)ComputeHash("123456");
        _db.KeyExistsAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(false));
        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(storedHash));
        _db.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(1L));

        var result = await _service.VerifyOtpAsync(Phone, "999999");

        result.Should().BeFalse();
        await _db.DidNotReceive().KeyDeleteAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task VerifyOtp_WhenExceedsMaxAttempts_ShouldLockPhoneAndReturnFalse()
    {
        var storedHash = (RedisValue)ComputeHash("123456");
        _db.KeyExistsAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(false));
        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(storedHash));
        _db.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(4L)); // 4 > MaxAttempts (3) → triggers lockout

        var result = await _service.VerifyOtpAsync(Phone, "123456");

        result.Should().BeFalse();
        // Should set the lock key for 15 minutes
        await _db.Received(1).StringSetAsync(
            Arg.Is<RedisKey>(k => k.ToString().StartsWith("otp:lock:")),
            Arg.Any<RedisValue>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>());
        // Should cleanup otp:code and otp:attempts keys
        await _db.Received(2).KeyDeleteAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>());
    }

    #region Helpers

    /// <summary>
    /// Sets up Redis mock defaults for a successful GenerateAndSendOtpAsync call
    /// (no lockout, under rate limit, all writes succeed).
    /// </summary>
    private void SetupSuccessfulGenerateDefaults()
    {
        _db.KeyExistsAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(false));
        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(RedisValue.Null)); // first request in window
        _db.StringSetAsync(
                Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(true));
        _db.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(1L));
        _db.KeyExpireAsync(Arg.Any<RedisKey>(), Arg.Any<TimeSpan?>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(true));
    }

    /// <summary>
    /// Reproduces the SHA256 hash used internally by OtpService.
    /// </summary>
    private static string ComputeHash(string otp)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(otp);
        return Convert.ToBase64String(SHA256.HashData(bytes));
    }

    #endregion
}
