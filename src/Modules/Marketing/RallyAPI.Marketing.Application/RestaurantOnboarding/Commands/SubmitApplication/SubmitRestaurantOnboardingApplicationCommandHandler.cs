using MediatR;
using Microsoft.Extensions.Logging;
using RallyAPI.Marketing.Application.Abstractions;
using RallyAPI.Marketing.Domain.Entities;
using RallyAPI.SharedKernel.Results;
using RallyAPI.SharedKernel.Security;

namespace RallyAPI.Marketing.Application.RestaurantOnboarding.Commands.SubmitApplication;

public sealed class SubmitRestaurantOnboardingApplicationCommandHandler
    : IRequestHandler<SubmitRestaurantOnboardingApplicationCommand, Result<SubmitRestaurantOnboardingApplicationResponse>>
{
    private readonly IRestaurantOnboardingApplicationRepository _repository;
    private readonly IFieldEncryptionService _encryption;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOnboardingNotificationService _notificationService;
    private readonly ILogger<SubmitRestaurantOnboardingApplicationCommandHandler> _logger;

    public SubmitRestaurantOnboardingApplicationCommandHandler(
        IRestaurantOnboardingApplicationRepository repository,
        IFieldEncryptionService encryption,
        IUnitOfWork unitOfWork,
        IOnboardingNotificationService notificationService,
        ILogger<SubmitRestaurantOnboardingApplicationCommandHandler> logger)
    {
        _repository = repository;
        _encryption = encryption;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<Result<SubmitRestaurantOnboardingApplicationResponse>> Handle(
        SubmitRestaurantOnboardingApplicationCommand request,
        CancellationToken cancellationToken)
    {
        // Block a duplicate submission while a prior one for the same phone/email is still
        // awaiting review — cheap spam/double-submit guard, not a hard uniqueness constraint
        // (the same restaurant can reapply after a Rejected or once Approved-and-onboarded).
        if (await _repository.HasPendingApplicationAsync(request.Phone, request.Email, cancellationToken))
        {
            return Result.Failure<SubmitRestaurantOnboardingApplicationResponse>(
                Error.Conflict("An application for this phone number or email is already pending review."));
        }

        // Encrypt sensitive fields here — never write plaintext PAN/GST/bank account number
        // to the database. Everything else stays plaintext (non-sensitive, and needed for
        // search/display in the admin review list).
        var bankAccountNumberEncrypted = _encryption.Encrypt(request.BankAccountNumber);
        var bankAccountLast4 = Last4(request.BankAccountNumber);

        var panNumberEncrypted = _encryption.Encrypt(request.PanNumber);
        var panLast4 = Last4(request.PanNumber);

        string? gstNumberEncrypted = null;
        string? gstLast4 = null;
        if (!string.IsNullOrWhiteSpace(request.GstNumber))
        {
            gstNumberEncrypted = _encryption.Encrypt(request.GstNumber);
            gstLast4 = Last4(request.GstNumber);
        }

        var createResult = RestaurantOnboardingApplication.Create(
            request.RestaurantName,
            request.OwnerName,
            request.Phone,
            request.Email,
            request.City,
            request.AddressLine,
            request.CuisineType,
            request.FssaiNumber,
            bankAccountNumberEncrypted,
            bankAccountLast4,
            request.BankIfscCode,
            request.BankAccountName,
            panNumberEncrypted,
            panLast4,
            gstNumberEncrypted,
            gstLast4,
            request.Source,
            request.IpAddress);

        if (createResult.IsFailure)
            return Result.Failure<SubmitRestaurantOnboardingApplicationResponse>(createResult.Error);

        var application = createResult.Value;
        await _repository.AddAsync(application, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "New restaurant onboarding application {ApplicationId} submitted for '{RestaurantName}' in {City}",
            application.Id, application.RestaurantName, application.City);

        // Best-effort alert — the service swallows its own failures so a Discord outage never
        // blocks or fails the applicant's submission.
        await _notificationService.NotifyNewApplicationAsync(application, cancellationToken);

        return Result.Success(new SubmitRestaurantOnboardingApplicationResponse(application.Id));
    }

    private static string Last4(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= 4 ? trimmed : trimmed[^4..];
    }
}
