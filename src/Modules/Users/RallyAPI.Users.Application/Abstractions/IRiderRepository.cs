using RallyAPI.Users.Domain.Entities;
using RallyAPI.Users.Domain.Enums;
using RallyAPI.Users.Domain.ValueObjects;

namespace RallyAPI.Users.Application.Abstractions;

public interface IRiderRepository
{
    Task<Rider?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Rider?> GetByPhoneAsync(PhoneNumber phone, CancellationToken cancellationToken = default);
    Task<bool> ExistsByPhoneAsync(PhoneNumber phone, CancellationToken cancellationToken = default);
    Task<List<Rider>> GetOnlineRidersAsync(CancellationToken cancellationToken = default);
    Task<Rider?> GetByIdWithKycAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> CountAsync(bool? isOnline = null, CancellationToken cancellationToken = default);
    Task<int> CountPendingKycAsync(CancellationToken cancellationToken = default);
    Task<(List<Rider> Items, int TotalCount)> GetPagedAsync(bool? isOnline, KycStatus? kycStatus, int page, int pageSize, CancellationToken cancellationToken = default);
    Task AddAsync(Rider rider, CancellationToken cancellationToken = default);
    void Update(Rider rider, CancellationToken cancellationToken = default);

    // KYC documents are written directly as their own rows so we never issue a
    // concurrency-checked UPDATE against the Rider aggregate (which carries a
    // Version token). See ConfirmRiderKycDocumentCommandHandler.
    void AddKycDocument(RiderKycDocument document);
    void RemoveKycDocument(RiderKycDocument document);

    /// <summary>
    /// For each given rider id, returns current bank details, read live at call time (not a
    /// stored snapshot). A rider missing from the result — or present with a null field — has
    /// no usable bank details and must be excluded from an ICICI export.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, RiderBankDetails>> GetBankDetailsByIdsAsync(
        IReadOnlyCollection<Guid> riderIds, CancellationToken cancellationToken = default);
}

public sealed record RiderBankDetails(
    Guid RiderId,
    string? BankAccountNumber,
    string? BankIfscCode,
    string? BankAccountName);