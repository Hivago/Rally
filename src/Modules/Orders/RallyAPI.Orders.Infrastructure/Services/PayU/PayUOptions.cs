// File: src/Modules/Orders/RallyAPI.Orders.Infrastructure/Services/PayU/PayUOptions.cs

namespace RallyAPI.Orders.Infrastructure.Services.PayU;

public class PayUOptions
{
    public const string SectionName = "PayU";

    /// <summary>Merchant Key from PayU dashboard</summary>
    public string MerchantKey { get; set; } = string.Empty;

    /// <summary>Merchant Salt (v2) — NEVER expose to client. Store in env vars for production.</summary>
    public string MerchantSalt { get; set; } = string.Empty;

    /// <summary>Test: https://test.payu.in  |  Production: https://secure.payu.in</summary>
    public string BaseUrl { get; set; } = "https://test.payu.in";

    /// <summary>
    /// Where PayU redirects the browser on successful payment. This must point at the
    /// BACKEND return endpoint (…/api/payments/return/success), not the SPA directly:
    /// PayU POSTs the result form here, and a static SPA cannot read a POST body. The
    /// backend reads it and 302-redirects to <see cref="FrontendSuccessUrl"/>.
    /// </summary>
    public string SuccessUrl { get; set; } = string.Empty;

    /// <summary>Where PayU redirects the browser on failed payment (backend return endpoint).</summary>
    public string FailureUrl { get; set; } = string.Empty;

    /// <summary>
    /// SPA success page the backend return endpoint redirects to (GET), with the resolved
    /// order id appended as ?orderId=…  e.g. https://hivago.vercel.app/payment-success
    /// </summary>
    public string FrontendSuccessUrl { get; set; } = string.Empty;

    /// <summary>SPA failure page the backend return endpoint redirects to (GET), with ?orderId=… when known.</summary>
    public string FrontendFailureUrl { get; set; } = string.Empty;
}