using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.SharedKernel.Storage;
using RallyAPI.Users.Application.Abstractions;
using RallyAPI.Users.Domain.Entities;

namespace RallyAPI.Users.Application.Riders.Commands.UploadKyc;

// ──────────────────────────────────────────────
// Command
// ──────────────────────────────────────────────

public sealed record ConfirmRiderKycDocumentCommand(
    Guid RiderId,
    Guid RequestingRiderId,
    bool IsAdmin,
    RiderKycDocumentType DocumentType,
    string FileKey
) : IRequest<Result<ConfirmRiderKycDocumentResponse>>;

public sealed record ConfirmRiderKycDocumentResponse(
    Guid DocumentId,
    string PublicUrl,
    RiderKycDocumentType DocumentType
);

// ──────────────────────────────────────────────
// Handler
// ──────────────────────────────────────────────

public sealed class ConfirmRiderKycDocumentCommandHandler
    : IRequestHandler<ConfirmRiderKycDocumentCommand, Result<ConfirmRiderKycDocumentResponse>>
{
    private readonly IRiderRepository _riderRepository;
    private readonly IStorageService _storage;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmRiderKycDocumentCommandHandler(
        IRiderRepository riderRepository,
        IStorageService storage,
        IUnitOfWork unitOfWork)
    {
        _riderRepository = riderRepository;
        _storage = storage;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ConfirmRiderKycDocumentResponse>> Handle(
        ConfirmRiderKycDocumentCommand command,
        CancellationToken ct)
    {
        // 1. Ownership check
        if (!command.IsAdmin && command.RiderId != command.RequestingRiderId)
            return Result.Failure<ConfirmRiderKycDocumentResponse>(
                Error.Forbidden("You can only upload your own KYC documents."));

        // 2. Load rider aggregate WITH its KYC documents. We need the collection
        //    tracked so the replace path below finds an existing doc of the same
        //    type (and so the new doc is tracked as Added, not Modified).
        var rider = await _riderRepository.GetByIdWithKycAsync(command.RiderId, ct);
        if (rider is null)
            return Result.Failure<ConfirmRiderKycDocumentResponse>(
                Error.NotFound("Rider", command.RiderId));

        // 3. Validate fileKey belongs to this rider + document type
        var docTypeName = command.DocumentType.ToString().ToLowerInvariant();
        var expectedPrefix = $"riders/{command.RiderId}/kyc/{docTypeName}/";
        if (!command.FileKey.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<ConfirmRiderKycDocumentResponse>(
                StorageErrors.InvalidFileKey);

        // 4. Delete old file from R2 if rider is replacing an existing document
        var existingDoc = rider.KycDocuments
            .FirstOrDefault(d => d.DocumentType == command.DocumentType);
        if (existingDoc is not null)
            await _storage.DeleteAsync(existingDoc.FileKey, ct);

        // 5. Build public URL and update Rider aggregate
        var publicUrl = _storage.BuildPublicUrl(command.FileKey);
        var document = rider.AddOrReplaceKycDocument(
            command.DocumentType,
            command.FileKey,
            publicUrl);

        // 6. Save. The rider was loaded tracked, so EF already knows the new
        //    document is Added (and any replaced one is Deleted) plus the scalar
        //    UpdatedAt change. Do NOT call repository.Update(rider): DbSet.Update
        //    marks the whole graph Modified, which makes EF emit an UPDATE for the
        //    brand-new document (it already has a non-default Guid key) instead of
        //    an INSERT -> 0 rows affected -> DbUpdateConcurrencyException (the 500).
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new ConfirmRiderKycDocumentResponse(
            document.Id,
            publicUrl,
            document.DocumentType));
    }
}