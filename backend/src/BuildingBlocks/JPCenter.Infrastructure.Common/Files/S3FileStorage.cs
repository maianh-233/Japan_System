using System.Net;
using System.Text.RegularExpressions;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using JPCenter.Application.Common.Files;
using JPCenter.SharedKernel.Primitives;
using Microsoft.Extensions.Logging;

namespace JPCenter.Infrastructure.Common.Files;

/// <summary>
/// Hiện thực <see cref="IFileStorage"/> trên mọi kho S3-compatible (SeaweedFS local, Cloudflare R2, Backblaze B2, AWS S3).
/// Bucket riêng tư; tải xuống bằng link ký sẵn có hạn — trình duyệt lấy file thẳng từ kho, không đi qua API.
/// Singleton: <see cref="AmazonS3Client"/> dùng chung an toàn giữa các luồng.
/// </summary>
public sealed partial class S3FileStorage : IFileStorage, IDisposable
{
    private readonly FileStorageOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<S3FileStorage> _logger;
    private readonly AmazonS3Client? _client;
    private readonly SemaphoreSlim _bucketLock = new(1, 1);
    private bool _bucketReady;

    public S3FileStorage(FileStorageOptions options, TimeProvider time, ILogger<S3FileStorage> logger)
    {
        _options = options;
        _time = time;
        _logger = logger;
        if (!options.IsConfigured) return;

        var config = new AmazonS3Config
        {
            ServiceURL = options.ServiceUrl,
            AuthenticationRegion = options.Region,
            // Kho ngoài AWS dùng đường dẫn dạng {host}/{bucket}/{key}
            ForcePathStyle = true,
            // SDK v4 mặc định gửi checksum CRC — R2 / SeaweedFS chưa hỗ trợ đầy đủ → chỉ gửi khi API bắt buộc
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };
        _client = new AmazonS3Client(new BasicAWSCredentials(options.AccessKey ?? "local", options.SecretKey ?? "local"), config);
    }

    public async Task<Result<StoredFile>> UploadAsync(Stream content, string fileName, string folder, FilePolicy policy, CancellationToken ct)
    {
        if (!FolderPattern().IsMatch(folder)) return FileErrors.InvalidFolder;

        // Cần biết dung lượng + đọc lại từ đầu → luồng không tua được thì đệm ra file tạm (tự xóa khi đóng)
        await using var buffered = content.CanSeek ? null : await BufferAsync(content, policy.MaxBytes, ct);
        var stream = buffered ?? content;
        var start = stream.Position;
        var length = stream.Length - start;

        var header = new byte[FileTypes.HeaderLength];
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        stream.Position = start;

        var check = policy.Check(fileName, length, header.AsSpan(0, read));
        if (check.IsFailure) return check.Error;
        var type = check.Value;

        var client = Client();
        await EnsureBucketAsync(client, ct);

        var now = _time.GetUtcNow();
        var key = $"{folder}/{now:yyyy}/{now:MM}/{Guid.CreateVersion7():N}.{type.Extension}";
        var displayName = SafeFileName(fileName);

        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            InputStream = stream,
            AutoCloseStream = false,
            ContentType = type.ContentType,
            // Gửi 1 lần (không chia khúc aws-chunked) — R2 / SeaweedFS nhận ổn định nhất; HTTPS thì bỏ ký nội dung cho nhanh
            UseChunkEncoding = false,
            DisablePayloadSigning = _options.UsesHttps,
            DisableDefaultChecksumValidation = true,
            Metadata = { ["original-name"] = Uri.EscapeDataString(displayName) },
        }, ct);

        _logger.LogInformation("Đã lưu file {Key} ({Size} byte, {Policy})", key, length, policy.Name);
        return new StoredFile(key, displayName, type.ContentType, length);
    }

    public async Task<Uri> GetDownloadUrlAsync(string key, FileLinkOptions? options, CancellationToken ct)
    {
        options ??= new FileLinkOptions();
        var validFor = options.ValidFor ?? TimeSpan.FromMinutes(_options.LinkMinutes);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Protocol = _options.UsesHttps ? Protocol.HTTPS : Protocol.HTTP,
            Expires = _time.GetUtcNow().Add(validFor).UtcDateTime,
        };
        if (!string.IsNullOrWhiteSpace(options.DownloadName))
        {
            var name = SafeFileName(options.DownloadName);
            // filename* (RFC 5987) giữ được tên tiếng Việt / tiếng Nhật; filename= bản ASCII cho trình duyệt cũ
            request.ResponseHeaderOverrides.ContentDisposition =
                $"{(options.Inline ? "inline" : "attachment")}; filename=\"{AsciiName(name)}\"; filename*=UTF-8''{Uri.EscapeDataString(name)}";
        }

        return new Uri(await Client().GetPreSignedURLAsync(request));
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken ct)
    {
        try
        {
            var response = await Client().GetObjectAsync(_options.Bucket, key, ct);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct)
    {
        try
        {
            await Client().GetObjectMetadataAsync(_options.Bucket, key, ct);
            return true;
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        // S3 xóa key không tồn tại vẫn trả 204 → gọi lại an toàn
        await Client().DeleteObjectAsync(_options.Bucket, key, ct);
        _logger.LogInformation("Đã xóa file {Key}", key);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _bucketLock.Dispose();
    }

    private AmazonS3Client Client() => _client ?? throw new InvalidOperationException(
        "Chưa cấu hình nơi lưu file (Storage:ServiceUrl). Local: chạy docker compose + điền Storage__* trong .env (xem .env.example).");

    private async Task EnsureBucketAsync(AmazonS3Client client, CancellationToken ct)
    {
        if (_bucketReady || !_options.CreateBucket) return;
        await _bucketLock.WaitAsync(ct);
        try
        {
            if (_bucketReady) return;
            if (!await AmazonS3Util.DoesS3BucketExistV2Async(client, _options.Bucket))
            {
                await client.PutBucketAsync(_options.Bucket, ct);
                _logger.LogInformation("Đã tạo bucket {Bucket}", _options.Bucket);
            }
            _bucketReady = true;
        }
        finally
        {
            _bucketLock.Release();
        }
    }

    /// <summary>Chép luồng không tua được ra file tạm; quá dung lượng cho phép thì dừng sớm (Check sẽ báo quá cỡ)</summary>
    private static async Task<FileStream> BufferAsync(Stream source, long maxBytes, CancellationToken ct)
    {
        var temp = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        var buffer = new byte[81920];
        int n;
        while ((n = await source.ReadAsync(buffer, ct)) > 0)
        {
            await temp.WriteAsync(buffer.AsMemory(0, n), ct);
            if (temp.Length > maxBytes) break;
        }
        temp.Position = 0;
        return temp;
    }

    /// <summary>Tên hiển thị: bỏ đường dẫn + ký tự điều khiển, tối đa 200 ký tự (giữ đuôi)</summary>
    public static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/').Split('/')[^1]);
        name = new string(name.Where(c => !char.IsControl(c) && c != '"').ToArray()).Trim();
        if (name.Length <= 200) return name.Length == 0 ? "tep" : name;
        var ext = Path.GetExtension(name);
        return name[..(200 - ext.Length)] + ext;
    }

    private static string AsciiName(string name) =>
        new(name.Normalize(System.Text.NormalizationForm.FormD).Where(c => c is >= ' ' and <= '~').ToArray());

    /// <summary>Thư mục: chữ thường, số, gạch ngang, ngăn bằng "/" — vd <c>academic/materials</c></summary>
    [GeneratedRegex("^[a-z0-9-]+(/[a-z0-9-]+)*$")]
    private static partial Regex FolderPattern();
}
