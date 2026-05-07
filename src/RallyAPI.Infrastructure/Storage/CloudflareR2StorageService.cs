using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RallyAPI.SharedKernel.Storage;

namespace RallyAPI.Infrastructure.Storage;

/// <summary>
/// Cloudflare R2 implementation of IStorageService.
/// R2 is S3-compatible, so we use the standard AWSSDK.S3 — no Cloudflare SDK needed.
///
/// HOW PRESIGNED URLS WORK WITH R2:
/// 1. We generate a presigned PUT URL using the R2 S3-compatible endpoint.
/// 2. The client PUTs the file directly to that URL — our server never receives the bytes.
/// 3. After upload, the client sends back the fileKey.
/// 4. We call BuildPublicUrl(fileKey) to get the permanent CDN URL and save it to DB.
/// </summary>
public sealed class CloudflareR2StorageService : IStorageService
{
    private readonly AmazonS3Client _s3;
    private readonly R2Options _options;
    private readonly ILogger<CloudflareR2StorageService> _logger;

    public CloudflareR2StorageService(
        IOptions<R2Options> options,
        ILogger<CloudflareR2StorageService> logger)
    {
        _options = options.Value;
        _logger = logger;

        // R2's S3-compatible endpoint format:
        // https://<accountId>.r2.cloudflarestorage.com
        var r2Endpoint = $"https://{_options.AccountId}.r2.cloudflarestorage.com";

        var config = new AmazonS3Config
        {
            ServiceURL = r2Endpoint,
            // R2 uses path-style URLs (bucket in path, not subdomain)
            ForcePathStyle = true,
            // R2 is always "auto" region — use us-east-1 as placeholder (SDK requires a value)
            AuthenticationRegion = "auto",
        };

        var credentials = new BasicAWSCredentials(
            _options.AccessKeyId,
            _options.SecretAccessKey);

        _s3 = new AmazonS3Client(credentials, config);
    }

    /// <inheritdoc />
    public async Task<PresignedUrlResult> GenerateUploadUrlAsync(
        string key,
        string contentType,
        int expiryMinutes = 10,
        CancellationToken ct = default)
    {
        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _options.BucketName,
                Key = key,
                Verb = HttpVerb.PUT,
                ContentType = contentType,
                Expires = DateTime.UtcNow.AddMinutes(expiryMinutes),
                // Enforce content type — client must set this header on the PUT
                Headers = { ContentType = contentType }
            };

            // GetPreSignedURL is synchronous in the SDK — wrap in Task.Run to keep
            // the async call site clean and avoid blocking the thread pool.
            var url = await Task.Run(() => _s3.GetPreSignedURL(request), ct);

            return new PresignedUrlResult(
                UploadUrl: url,
                FileKey: key,
                ExpiresAt: DateTime.UtcNow.AddMinutes(expiryMinutes));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate presigned URL for key {Key}", key);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> UploadAsync(
        Stream content,
        string key,
        string contentType,
        CancellationToken ct = default)
    {
        try
        {
            var request = new PutObjectRequest
            {
                BucketName = _options.BucketName,
                Key = key,
                InputStream = content,
                ContentType = contentType,
                AutoCloseStream = false,
                DisablePayloadSigning = true
            };

            await _s3.PutObjectAsync(request, ct);
            return BuildPublicUrl(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload object {Key} to R2", key);
            throw;
        }
    }

    /// <inheritdoc />
    public string BuildPublicUrl(string key)
    {
        // Trims trailing slash from base URL defensively
        var baseUrl = _options.PublicUrlBase.TrimEnd('/');
        return $"{baseUrl}/{key}";
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var request = new DeleteObjectRequest
            {
                BucketName = _options.BucketName,
                Key = key
            };

            await _s3.DeleteObjectAsync(request, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete object {Key} from R2", key);
            return false;
        }
    }
}