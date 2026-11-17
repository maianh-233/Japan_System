using System.Collections.Concurrent;
using JPCenter.Application.Common.Files;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.IntegrationTests.Infrastructure;

/// <summary>
/// Kho file giả trong bộ nhớ cho test API — kiểm tra giống bản thật (<see cref="FilePolicy"/>: tên, dung lượng, đuôi, nội dung),
/// tên lưu ngẫu nhiên theo thư mục. Bản S3 thật được test riêng với SeaweedFS (BuildingBlocks/FileStorageTests.cs).
/// </summary>
public sealed class InMemoryFileStorage : IFileStorage
{
    public ConcurrentDictionary<string, byte[]> Files { get; } = new();

    public async Task<Result<StoredFile>> UploadAsync(Stream content, string fileName, string folder, FilePolicy policy, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var check = policy.Check(fileName, bytes.Length, bytes.AsSpan(0, Math.Min(bytes.Length, FileTypes.HeaderLength)));
        if (check.IsFailure) return check.Error;
        var key = $"{folder}/{Guid.NewGuid():N}.{check.Value.Extension}";
        Files[key] = bytes;
        return new StoredFile(key, Path.GetFileName(fileName), check.Value.ContentType, bytes.Length);
    }

    public Task<Uri> GetDownloadUrlAsync(string key, FileLinkOptions? options, CancellationToken ct) =>
        Task.FromResult(new Uri($"https://files.test/{key}?name={Uri.EscapeDataString(options?.DownloadName ?? "")}&inline={options?.Inline ?? true}"));

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct) =>
        Task.FromResult<Stream?>(Files.TryGetValue(key, out var b) ? new MemoryStream(b) : null);

    public Task<bool> ExistsAsync(string key, CancellationToken ct) => Task.FromResult(Files.ContainsKey(key));

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        Files.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
