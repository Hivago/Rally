using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Enums;

namespace RallyAPI.Users.Application.Riders.Queries.GetKycStatus;

internal sealed class GetRiderKycStatusQueryHandler
    : IRequestHandler<GetRiderKycStatusQuery, Result<RiderKycStatusResponse>>
{
    private readonly IRiderRepository _riderRepository;

    public GetRiderKycStatusQueryHandler(IRiderRepository riderRepository)
    {
        _riderRepository = riderRepository;
    }

    public async Task<Result<RiderKycStatusResponse>> Handle(
        GetRiderKycStatusQuery request,
        CancellationToken cancellationToken)
    {
        var rider = await _riderRepository.GetByIdWithKycAsync(request.RiderId, cancellationToken);
        if (rider is null)
            return Result.Failure<RiderKycStatusResponse>(Error.NotFound("Rider", request.RiderId));

        var documents = rider.KycDocuments
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new RiderKycDocumentDto(
                d.DocumentType.ToString(),
                d.PublicUrl,
                d.IsVerified,
                d.UploadedAt,
                d.VerifiedAt))
            .ToList();

        DateTime? lastSubmittedAt = rider.KycDocuments.Count > 0
            ? rider.KycDocuments.Max(d => d.UploadedAt)
            : null;

        var response = new RiderKycStatusResponse(
            KycStatus: rider.KycStatus.ToString(),
            CanGoOnline: rider.KycStatus == KycStatus.Verified,
            LastSubmittedAt: lastSubmittedAt,
            Documents: documents);

        return Result.Success(response);
    }
}
