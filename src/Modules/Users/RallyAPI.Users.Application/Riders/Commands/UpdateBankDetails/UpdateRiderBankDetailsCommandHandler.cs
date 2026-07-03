using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Abstractions;

namespace RallyAPI.Users.Application.Riders.Commands.UpdateBankDetails;

internal sealed class UpdateRiderBankDetailsCommandHandler
    : IRequestHandler<UpdateRiderBankDetailsCommand, Result>
{
    private readonly IRiderRepository _riderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateRiderBankDetailsCommandHandler(
        IRiderRepository riderRepository,
        IUnitOfWork unitOfWork)
    {
        _riderRepository = riderRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        UpdateRiderBankDetailsCommand request,
        CancellationToken cancellationToken)
    {
        var rider = await _riderRepository.GetByIdAsync(request.RiderId, cancellationToken);
        if (rider is null)
            return Result.Failure(Error.NotFound("Rider", request.RiderId));

        var result = rider.UpdateBankDetails(
            request.BankAccountNumber,
            request.BankIfscCode,
            request.BankAccountName);

        if (result.IsFailure)
            return result;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
