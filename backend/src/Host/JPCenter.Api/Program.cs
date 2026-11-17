using JPCenter.Api;
using JPCenter.Api.Common;
using JPCenter.Api.Common.Modules;
using JPCenter.Application.Common.Messaging;
using JPCenter.Infrastructure.Common.Persistence;
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

builder.Services
    .AddApiCommon()
    .AddMessaging()
    .AddInfrastructureCommon()
    .AddModules(builder.Configuration /* các module đăng ký ở giai đoạn 3 */);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapModules();

if (app.Environment.IsDevelopment()) await app.Services.InitializeDatabaseAsync();

app.Run();

/// <summary>Cho WebApplicationFactory trong integration test.</summary>
public partial class Program;
