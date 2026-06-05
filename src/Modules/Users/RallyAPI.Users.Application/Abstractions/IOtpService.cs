using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Abstractions;

public interface IOtpService
{
    /// <summary>
    /// Generates, stores and sends an OTP. Returns a failure Result (instead of throwing)
    /// when the phone is locked out or rate-limited, so callers surface 429 — not 500.
    /// </summary>
    Task<Result<string>> GenerateAndSendOtpAsync(string phoneNumber, CancellationToken cancellationToken = default);
    Task<bool> VerifyOtpAsync(string phoneNumber, string otp, CancellationToken cancellationToken = default);
}