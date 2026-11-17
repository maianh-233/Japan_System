using JPCenter.Api;
using Serilog;

// Đọc .env TRƯỚC khi tạo builder để cấu hình nhận được các biến
DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, log) =>
{
    log.ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console();
    if (ctx.Configuration["Seq:Url"] is { Length: > 0 } seq) log.WriteTo.Seq(seq);
});

var app = builder.Build();

app.UseSerilogRequestLogging();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

/// <summary>Cho WebApplicationFactory trong integration test.</summary>
public partial class Program;
