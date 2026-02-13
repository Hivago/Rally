using MediatR;
using RallyAPI.SharedKernel.Results;
using RallyAPI.Users.Application.Customers.Commands.VerifyOtp;

public sealed class VerifyCustomerOtpCommandHandler
    : IRequestHandler<
        VerifyCustomerOtpCommand,
        Result<VerifyCustomerOtpResponse>>
{
    public async Task<Result<VerifyCustomerOtpResponse>> Handle(
        VerifyCustomerOtpCommand request,
        CancellationToken cancellationToken)
    {
        // temporary test
        var response = new VerifyCustomerOtpResponse(
            Guid.NewGuid(),
            "dummy-token",
            false
        );

        return Result.Success(response);
    }
}
