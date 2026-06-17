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

        // 4. If replacing an existing document of the same type, delete its file
        //    and remove its row.
        var existingDoc = rider.KycDocuments
            .FirstOrDefault(d => d.DocumentType == command.DocumentType);
        if (existingDoc is not null)
        {
            await _storage.DeleteAsync(existingDoc.FileKey, ct);
            _riderRepository.RemoveKycDocument(existingDoc);
        }

        // 5. Insert the new document directly as its own row. We deliberately do
        //    NOT mutate or save the Rider aggregate here: Rider carries a `Version`
        //    optimistic-concurrency token, and persisting the graph made EF emit a
        //    concurrency-checked UPDATE on the rider row that affected 0 rows
        //    (DbUpdateConcurrencyException -> HTTP 500). Writing only the child
        //    table via an explicit INSERT sidesteps the concurrency check entirely.
        var publicUrl = _storage.BuildPublicUrl(command.FileKey);
        var document = RiderKycDocument.Create(
            command.RiderId,
            command.DocumentType,
            command.FileKey,
            publicUrl);
        _riderRepository.AddKycDocument(document);

        // 6. Save — INSERT (+ optional DELETE) only; no UPDATE on the rider row.
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new ConfirmRiderKycDocumentResponse(
            document.Id,
            publicUrl,
            document.DocumentType));
    }
}