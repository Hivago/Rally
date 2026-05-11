using MediatR;
using RallyAPI.Marketing.Application.Abstractions;
using RallyAPI.Marketing.Domain.Entities;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Marketing.Application.RestaurantLeads.Commands.SubmitLead;

internal sealed class SubmitRestaurantLeadCommandHandler
    : IRequestHandler<SubmitRestaurantLeadCommand, Result<SubmitRestaurantLeadResponse>>
{
    private readonly IRestaurantLeadRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SubmitRestaurantLeadCommandHandler(
        IRestaurantLeadRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SubmitRestaurantLeadResponse>> Handle(
        SubmitRestaurantLeadCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedPhone = request.Phone.Trim();

        if (await _repository.ExistsByPhoneAsync(normalizedPhone, cancellationToken))
            return Result.Failure<SubmitRestaurantLeadResponse>(
                Error.Conflict("A lead with this phone number already exists."));

        var leadResult = RestaurantLead.Create(
            request.RestaurantName,
            request.OwnerName,
            normalizedPhone,
            request.City,
            request.DailyOrders,
            request.Source,
            request.IpAddress);

        if (leadResult.IsFailure)
            return Result.Failure<SubmitRestaurantLeadResponse>(leadResult.Error);

        await _repository.AddAsync(leadResult.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new SubmitRestaurantLeadResponse(leadResult.Value.Id));
    }
}
