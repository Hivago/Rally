using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Riders.Queries.GetProfile;

internal sealed class GetRiderProfileQueryHandler
    : IRequestHandler<GetRiderProfileQuery, Result<RiderProfileResponse>>
{
    private readonly IRiderRepository _riderRepository;

    public GetRiderProfileQueryHandler(IRiderRepository riderRepository)
    {
        _riderRepository = riderRepository;
    }

    public async Task<Result<RiderProfileResponse>> Handle(
        GetRiderProfileQuery request,
        CancellationToken cancellationToken)
    {
        var rider = await _riderRepository.GetByIdAsync(request.RiderId, cancellationToken);
        if (rider is null)
            return Result.Failure<RiderProfileResponse>(Error.NotFound("Rider", request.RiderId));

        var response = new RiderProfileResponse(
            rider.Id,
            rider.Phone.GetFormatted(),
            rider.Name,
            rider.Email?.Value,
            rider.VehicleType.ToString(),
            rider.VehicleNumber,
            rider.KycStatus.ToString(),
            rider.IsActive,
            rider.IsOnline,
            rider.CurrentLatitude,
            rider.CurrentLongitude,
            Mask(rider.BankAccountNumber),
            rider.BankIfscCode,
            rider.BankAccountName);

        return Result.Success(response);
    }

    private static string? Mask(string? accountNumber)
    {
        if (string.IsNullOrEmpty(accountNumber) || accountNumber.Length < 4)
            return accountNumber;
        return new string('*', accountNumber.Length - 4) + accountNumber[^4..];
    }
}