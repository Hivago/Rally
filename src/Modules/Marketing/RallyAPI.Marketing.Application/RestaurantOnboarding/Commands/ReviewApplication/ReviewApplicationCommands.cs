using MediatR;
using RallyAPI.Marketing.Application.Abstractions;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Marketing.Application.RestaurantOnboarding.Commands.ReviewApplication;

// Approving/rejecting an application never creates a live owner/restaurant account and never
// touches money — it just records a review decision. Creating the real account with real
// login credentials is a deliberate separate manual step (see docs handoff note). That's why
// these are gated at the generic "Admin" policy, not Super Admin — unlike the payout reconcile
// flow, nothing here can move funds or fabricate a paid transaction.

// ============ Approve ============

public sealed record ApproveRestaurantOnboardingApplicationCommand(
    Guid ApplicationId,
    Guid ReviewedByAdminId,
    string? Notes) : IRequest<Result>;

public sealed class ApproveRestaurantOnboardingApplicationCommandHandler
    : IRequestHandler<ApproveRestaurantOnboardingApplicationCommand, Result>
{
    private readonly IRestaurantOnboardingApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveRestaurantOnboardingApplicationCommandHandler(
        IRestaurantOnboardingApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ApproveRestaurantOnboardingApplicationCommand request, CancellationToken ct)
    {
        var app = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (app is null)
            return Result.Failure(Error.NotFound("RestaurantOnboardingApplication", request.ApplicationId));

        var result = app.Approve(request.ReviewedByAdminId, request.Notes);
        if (result.IsFailure)
            return result;

        _repository.Update(app);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ============ Reject ============

public sealed record RejectRestaurantOnboardingApplicationCommand(
    Guid ApplicationId,
    Guid ReviewedByAdminId,
    string Reason) : IRequest<Result>;

public sealed class RejectRestaurantOnboardingApplicationCommandHandler
    : IRequestHandler<RejectRestaurantOnboardingApplicationCommand, Result>
{
    private readonly IRestaurantOnboardingApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RejectRestaurantOnboardingApplicationCommandHandler(
        IRestaurantOnboardingApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(RejectRestaurantOnboardingApplicationCommand request, CancellationToken ct)
    {
        var app = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (app is null)
            return Result.Failure(Error.NotFound("RestaurantOnboardingApplication", request.ApplicationId));

        var result = app.Reject(request.ReviewedByAdminId, request.Reason);
        if (result.IsFailure)
            return result;

        _repository.Update(app);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
