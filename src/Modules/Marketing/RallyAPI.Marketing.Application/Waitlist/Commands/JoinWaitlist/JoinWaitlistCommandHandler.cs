using MediatR;
using RallyAPI.Marketing.Application.Abstractions;
using RallyAPI.Marketing.Domain.Entities;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Marketing.Application.Waitlist.Commands.JoinWaitlist;

internal sealed class JoinWaitlistCommandHandler
    : IRequestHandler<JoinWaitlistCommand, Result<JoinWaitlistResponse>>
{
    private readonly ICustomerWaitlistRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public JoinWaitlistCommandHandler(
        ICustomerWaitlistRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<JoinWaitlistResponse>> Handle(
        JoinWaitlistCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedPhone = request.Phone.Trim();

        if (await _repository.ExistsByEmailAsync(normalizedEmail, cancellationToken))
            return Result.Failure<JoinWaitlistResponse>(
                Error.Conflict("This email is already on the waitlist."));

        if (await _repository.ExistsByPhoneAsync(normalizedPhone, cancellationToken))
            return Result.Failure<JoinWaitlistResponse>(
                Error.Conflict("This phone number is already on the waitlist."));

        var entryResult = CustomerWaitlistEntry.Create(
            request.Name,
            normalizedEmail,
            normalizedPhone,
            request.Source,
            request.IpAddress);

        if (entryResult.IsFailure)
            return Result.Failure<JoinWaitlistResponse>(entryResult.Error);

        await _repository.AddAsync(entryResult.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new JoinWaitlistResponse(entryResult.Value.Id));
    }
}
