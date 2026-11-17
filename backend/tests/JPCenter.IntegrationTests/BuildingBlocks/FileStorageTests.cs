using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using JPCenter.Application.Common.Files;
using JPCenter.Infrastructure.Common.Files;
using Microsoft.Extensions.Logging.Abstractions;

namespace JPCenter.IntegrationTests.BuildingBlocks;

/// <summary>Quy định tải lên (đuôi, dung lượng, nội dung thật) — không cần Docker</summary>
public sealed class FilePolicyTests
{
    private static readonly byte[] PdfHeader = "%PDF-1.7\n"u8.ToArray();

    [Fact]
    public void Nhan_pdf_dung_noi_dung()
    {
        var r = FilePolicy.TeachingMaterials.Check("Bài 1 - Từ vựng.PDF", 1000, PdfHeader);
        r.IsSuccess.ShouldBeTrue();
        r.Value.ShouldBe(FileTypes.Pdf);
    }

    [Fact]
    public void Doi_duoi_exe_thanh_pdf_bi_chan()
    {
        var exe = new byte[] { 0x4D, 0x5A, 0x90, 0x00 }; // "MZ" — file chạy Windows
        FilePolicy.TeachingMaterials.Check("baitap.pdf", 1000, exe).Error.Code.ShouldBe("file.content_mismatch");
    }

    [Fact]
    public void Duoi_khong_cho_phep()
    {
        var r = FilePolicy.TeachingMaterials.Check("video.mp4", 1000, new byte[16]);
        r.Error.Code.ShouldBe("file.type_not_allowed");
        r.Error.Message.ShouldContain("pdf");
    }

    [Fact]
    public void Qua_dung_luong()
    {
        FilePolicy.Images.Check("anh.png", FilePolicy.Images.MaxBytes + 1, new byte[16]).Error.Code.ShouldBe("file.too_large");
    }

    [Fact]
    public void Tep_rong_va_thieu_ten()
    {
        FilePolicy.Images.Check("anh.png", 0, []).Error.Code.ShouldBe("file.empty");
        FilePolicy.Images.Check(" ", 10, []).Error.Code.ShouldBe("file.name_required");
    }

    [Theory]
    [InlineData("docx", new byte[] { 0x50, 0x4B, 0x03, 0x04 })]
    [InlineData("ppt", new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 })]
    [InlineData("mp3", new byte[] { 0x49, 0x44, 0x33, 0x04 })]
    [InlineData("mp3", new byte[] { 0xFF, 0xFB, 0x90, 0x64 })]
    [InlineData("m4a", new byte[] { 0, 0, 0, 0x20, 0x66, 0x74, 0x79, 0x70 })]
    public void Nhan_dien_magic_bytes(string extension, byte[] header)
    {
        FilePolicy.TeachingMaterials.Check($"tai-lieu.{extension}", 100, header).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Ten_hien_thi_bo_duong_dan_va_ky_tu_dieu_khien()
    {
        S3FileStorage.SafeFileName("..\\..\\windows/系統\u0001\"bài.pdf").ShouldBe("系統bài.pdf");
    }
}

/// <summary>Kho S3-compatible thật (SeaweedFS trong Docker) — cùng image với docker-compose</summary>
public sealed class SeaweedFixture : IAsyncLifetime
{
    private const int S3Port = 8333;

    private readonly IContainer _container = new ContainerBuilder("chrislusf/seaweedfs:4.51")
        .WithCommand("server", "-dir=/data", "-ip.bind=0.0.0.0", "-s3", $"-s3.port={S3Port}")
        .WithPortBinding(S3Port, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(S3Port).ForPath("/").ForStatusCodeMatching(_ => true)))
        .Build();

    public S3FileStorage Storage { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        var url = $"http://{_container.Hostname}:{_container.GetMappedPublicPort(S3Port)}";
        var options = new FileStorageOptions(url, "auto", "test-bucket", "local", "local", CreateBucket: true, LinkMinutes: 5);
        Storage = new S3FileStorage(options, TimeProvider.System, NullLogger<S3FileStorage>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        Storage?.Dispose();
        await _container.DisposeAsync();
    }
}

public sealed class S3FileStorageTests(SeaweedFixture fixture) : IClassFixture<SeaweedFixture>
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n% tai lieu thu\n%%EOF");
    private IFileStorage Storage => fixture.Storage;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Tai_len_lay_link_tai_ve_roi_xoa()
    {
        var result = await Storage.UploadAsync(new MemoryStream(Pdf), "Bài 1 - 文法.pdf", "academic/materials", FilePolicy.TeachingMaterials, Ct);

        result.IsSuccess.ShouldBeTrue();
        var file = result.Value;
        file.Key.ShouldStartWith("academic/materials/");
        file.Key.ShouldEndWith(".pdf");
        file.Key.ShouldNotContain("Bài"); // tên lưu là ngẫu nhiên, không dùng tên người dùng
        file.FileName.ShouldBe("Bài 1 - 文法.pdf");
        file.ContentType.ShouldBe("application/pdf");
        file.Size.ShouldBe(Pdf.Length);
        (await Storage.ExistsAsync(file.Key, Ct)).ShouldBeTrue();

        var url = await Storage.GetDownloadUrlAsync(file.Key, new FileLinkOptions(DownloadName: file.FileName, Inline: false), Ct);
        using var http = new HttpClient();
        using var response = await http.GetAsync(url, Ct);
        response.IsSuccessStatusCode.ShouldBeTrue();
        (await response.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(Pdf);

        await using (var read = await Storage.OpenReadAsync(file.Key, Ct))
        {
            read.ShouldNotBeNull();
        }

        await Storage.DeleteAsync(file.Key, Ct);
        (await Storage.ExistsAsync(file.Key, Ct)).ShouldBeFalse();
        (await Storage.OpenReadAsync(file.Key, Ct)).ShouldBeNull();
        await Storage.DeleteAsync(file.Key, Ct); // xóa lại không lỗi
    }

    [Fact]
    public async Task Luong_khong_tua_duoc_van_luu_duoc()
    {
        var result = await Storage.UploadAsync(new ForwardOnlyStream(Pdf), "de.pdf", "academic/materials", FilePolicy.TeachingMaterials, Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Size.ShouldBe(Pdf.Length);
    }

    [Fact]
    public async Task File_gia_mao_khong_duoc_luu()
    {
        var exe = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03 };
        var result = await Storage.UploadAsync(new MemoryStream(exe), "de.pdf", "academic/materials", FilePolicy.TeachingMaterials, Ct);

        result.Error.Code.ShouldBe("file.content_mismatch");
    }

    [Theory]
    [InlineData("../etc")]
    [InlineData("Academic/Materials")]
    [InlineData("")]
    public async Task Thu_muc_khong_hop_le(string folder)
    {
        var result = await Storage.UploadAsync(new MemoryStream(Pdf), "de.pdf", folder, FilePolicy.TeachingMaterials, Ct);

        result.Error.Code.ShouldBe("file.invalid_folder");
    }

    /// <summary>Giống luồng upload mạng: chỉ đọc tới, không biết độ dài</summary>
    private sealed class ForwardOnlyStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    }
}
