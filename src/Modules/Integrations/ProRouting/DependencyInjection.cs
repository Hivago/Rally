//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Logging;
//using Polly;
//using Polly.Extensions.Http;
//using RallyAPI.SharedKernel.Abstractions.Delivery;

//namespace RallyAPI.Integrations.ProRouting;

///// <summary>
///// Extension methods for registering ProRouting integration services.
///// </summary>
//public static class DependencyInjection
//{
//    /// <summary>
//    /// Adds ProRouting integration services to the DI container.
//    /// Includes Polly retry policies for resilience.
//    /// </summary>
//    public static IServiceCollection AddProRoutingIntegration(
//        this IServiceCollection services,
//        IConfiguration configuration)
//    {
//        // Bind options
//        services.Configure<ProRoutingOptions>(
//            configuration.GetSection(ProRoutingOptions.SectionName));

//        var options = configuration
//            .GetSection(ProRoutingOptions.SectionName)
//            .Get<ProRoutingOptions>() ?? new ProRoutingOptions();

//        // Register HttpClient with Polly retry policy
//        services.AddHttpClient<IDeliveryQuoteProvider, ProRoutingClient>((sp, client) =>
//        {
//            client.BaseAddress = new Uri(options.BaseUrl);
//            client.DefaultRequestHeaders.Add("x-pro-api-key", options.ApiKey);
//            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
//            client.DefaultRequestHeaders.Add("Accept", "application/json");
//        })
//            .AddPolicyHandler((sp, _) => GetRetryPolicy(sp, options.RetryCount))
//            .AddPolicyHandler(GetTimeoutPolicy(options.TimeoutSeconds));

//        return services;
//    }

//    /// <summary>
//    /// Adds mock delivery quote provider for testing.
//    /// Use this instead of AddProRoutingIntegration in test environments.
//    /// </summary>
//    public static IServiceCollection AddMockDeliveryQuoteProvider(
//        this IServiceCollection services,
//        Action<MockQuoteOptions>? configure = null)
//    {
//        var options = new MockQuoteOptions();
//        configure?.Invoke(options);

//        services.AddSingleton(options);
//        services.AddScoped<IDeliveryQuoteProvider, MockDeliveryQuoteProvider>();

//        return services;
//    }

//    /// <summary>
//    /// Creates a retry policy with exponential backoff for transient HTTP errors.
//    /// </summary>
//    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(
//        IServiceProvider serviceProvider,
//        int retryCount)
//    {
//        return HttpPolicyExtensions
//            .HandleTransientHttpError() // 5xx, 408, network errors
//            .Or<TaskCanceledException>() // Timeouts
//            .WaitAndRetryAsync(
//                retryCount: retryCount,
//                sleepDurationProvider: retryAttempt =>
//                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // 2s, 4s, 8s
//                onRetry: (outcome, timespan, retryAttempt, _) =>
//                {
//                    var logger = serviceProvider.GetService<ILogger<ProRoutingClient>>();
//                    logger?.LogWarning(
//                        "ProRouting API retry {RetryAttempt} after {Delay}s. Reason: {Reason}",
//                        retryAttempt,
//                        timespan.TotalSeconds,
//                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
//                });
//    }

//    /// <summary>
//    /// Creates a timeout policy as a backup to HttpClient timeout.
//    /// </summary>
//    private static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(int timeoutSeconds)
//    {
//        return Policy.TimeoutAsync<HttpResponseMessage>(
//            TimeSpan.FromSeconds(timeoutSeconds));
//    }
//}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using RallyAPI.SharedKernel.Abstractions.Delivery;

namespace RallyAPI.Integrations.ProRouting;

public static class DependencyInjection
{
    public static IServiceCollection AddProRoutingIntegration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<ProRoutingOptions>(
            configuration.GetSection(ProRoutingOptions.SectionName));

        var options = configuration
            .GetSection(ProRoutingOptions.SectionName)
            .Get<ProRoutingOptions>() ?? new ProRoutingOptions();

        var clientBuilder = services.AddHttpClient<IDeliveryQuoteProvider, ProRoutingClient>((sp, client) =>
        {
            client.BaseAddress = new Uri(options.BaseUrl);
            client.DefaultRequestHeaders.Add("x-pro-api-key", options.ApiKey);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        // Bypass SSL validation only in Development. In Production we use the default
        // OS trust store so bad certs fail the request.
        if (environment.IsDevelopment())
        {
            clientBuilder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        }

        clientBuilder
            .AddPolicyHandler((sp, _) => GetRetryPolicy(sp, options.RetryCount))
            .AddPolicyHandler(GetTimeoutPolicy(options.TimeoutSeconds));

        services.AddHttpClient<IThirdPartyDeliveryProvider, ProRoutingTaskService>(client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl);
                client.DefaultRequestHeaders.Add("x-pro-api-key", options.ApiKey);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            })
            .AddPolicyHandler((sp, _) => GetRetryPolicy(sp, options.RetryCount))
            .AddPolicyHandler(GetTimeoutPolicy(options.TimeoutSeconds));

        // ProRoutingTaskService also implements IIgmProvider — forward the
        // resolution so both interfaces share the same HttpClient + auth.
        services.AddScoped<IIgmProvider>(sp =>
            (IIgmProvider)sp.GetRequiredService<IThirdPartyDeliveryProvider>());

        return services;
    }

    public static IServiceCollection AddMockDeliveryQuoteProvider(
        this IServiceCollection services,
        Action<MockQuoteOptions>? configure = null)
    {
        var options = new MockQuoteOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddScoped<IDeliveryQuoteProvider, MockDeliveryQuoteProvider>();

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(
        IServiceProvider serviceProvider,
        int retryCount)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: retryCount,
                sleepDurationProvider: retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryAttempt, _) =>
                {
                    var logger = serviceProvider.GetService<ILogger<ProRoutingClient>>();
                    logger?.LogWarning(
                        "ProRouting API retry {RetryAttempt} after {Delay}s. Reason: {Reason}",
                        retryAttempt,
                        timespan.TotalSeconds,
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                });
    }



    private static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(int timeoutSeconds)
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(
            TimeSpan.FromSeconds(timeoutSeconds));
    }
}