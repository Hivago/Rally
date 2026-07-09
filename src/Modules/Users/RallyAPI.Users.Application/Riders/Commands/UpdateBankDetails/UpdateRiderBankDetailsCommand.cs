using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Riders.Commands.UpdateBankDetails;

/// <summary>
/// Rider updates their payout bank details. All three fields are required together.
/// </summary>
public sealed record UpdateRiderBankDetailsCommand(
    Guid RiderId,
    string BankAccountNumber,
    string BankIfscCode,
    string BankAccountName) : IRequest<Result>;
