using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Riders.Queries.GetKycStatus;

public sealed record GetRiderKycStatusQuery(Guid RiderId)
    : IRequest<Result<RiderKycStatusResponse>>;

/// <summary>
/// The rider's own KYC verification state, for the app's KYC screen.
/// Lets the rider see their status and which documents they've uploaded /
/// which are verified — without needing the admin documents endpoint.
/// </summary>
public sealed record RiderKycStatusResponse(
    string KycStatus,
    bool CanGoOnline,
    DateTime? LastSubmittedAt,
    IReadOnlyList<RiderKycDocumentDto> Documents);

public sealed record RiderKycDocumentDto(
    string DocumentType,
    string PublicUrl,
    bool IsVerified,
    DateTime UploadedAt,
    DateTime? VerifiedAt);
