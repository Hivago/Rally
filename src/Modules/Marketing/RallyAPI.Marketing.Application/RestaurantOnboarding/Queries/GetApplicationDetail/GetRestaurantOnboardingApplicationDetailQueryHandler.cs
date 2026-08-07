using MediatR;
using RallyAPI.Marketing.Application.Abstractions;
using RallyAPI.SharedKernel.Results;
using RallyAPI.SharedKernel.Security;

namespace RallyAPI.Marketing.Application.RestaurantOnboarding.Queries.GetApplicationDetail;

public sealed class GetRestaurantOnboardingApplicationDetailQueryHandler
    : IRequestHandler<GetRestaurantOnboardingApplicationDetailQuery, Result<RestaurantOnboardingApplicationDetailDto?>>
{
    private readonly IRestaurantOnboardingApplicationRepository _repository;
    private readonly IFieldEncryptionService _encryption;

    public GetRestaurantOnboardingApplicationDetailQueryHandler(
        IRestaurantOnboardingApplicationRepository repository,
        IFieldEncryptionService encryption)
    {
        _repository = repository;
        _encryption = encryption;
    }

    public async Task<Result<RestaurantOnboardingApplicationDetailDto?>> Handle(
        GetRestaurantOnboardingApplicationDetailQuery request,
        CancellationToken cancellationToken)
    {
        var app = await _repository.GetByIdAsync(request.ApplicationId, cancellationToken);
        if (app is null)
            return Result.Success<RestaurantOnboardingApplicationDetailDto?>(null);

        string? bankAccountNumber = null;
        string? panNumber = null;
        string? gstNumber = null;

        if (request.IncludeSensitiveDetails)
        {
            bankAccountNumber = _encryption.Decrypt(app.BankAccountNumberEncrypted);
            panNumber = _encryption.Decrypt(app.PanNumberEncrypted);
            gstNumber = app.GstNumberEncrypted is null ? null : _encryption.Decrypt(app.GstNumberEncrypted);
        }

        var dto = new RestaurantOnboardingApplicationDetailDto(
            app.Id,
            app.RestaurantName,
            app.OwnerName,
            app.Phone,
            app.Email,
            app.City,
            app.AddressLine,
            app.CuisineType,
            app.FssaiNumber,
            Mask(app.BankAccountLast4),
            bankAccountNumber,
            app.BankIfscCode,
            app.BankAccountName,
            Mask(app.PanLast4),
            panNumber,
            app.GstLast4 is null ? null : Mask(app.GstLast4),
            gstNumber,
            app.Status,
            app.ReviewedByAdminId,
            app.ReviewedAtUtc,
            app.ReviewNotes,
            app.Source,
            app.CreatedAt);

        return Result.Success<RestaurantOnboardingApplicationDetailDto?>(dto);
    }

    private static string Mask(string last4) => $"•••• {last4}";
}
