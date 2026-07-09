using MediatR;
using RallyAPI.SharedKernel.Results;

namespace RallyAPI.Users.Application.Owners.Commands.UpdateBankDetails;

/// <summary>
/// Update a restaurant owner's payout bank details. Used by the owner (self, via JWT sub)
/// and by an admin (targeting an arbitrary owner). All three fields are required together.
/// </summary>
public sealed record UpdateOwnerBankDetailsCommand(
    Guid OwnerId,
    string BankAccountNumber,
    string BankIfscCode,
    string BankAccountName) : IRequest<Result>;
