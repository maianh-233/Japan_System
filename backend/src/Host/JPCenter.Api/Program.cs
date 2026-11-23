using JPCenter.Api;
using JPCenter.Api.Common;
using JPCenter.Api.Common.Modules;
using JPCenter.Api.Common.Realtime;
using JPCenter.Application.Common.Messaging;
using JPCenter.Infrastructure.Common.Jobs;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Academic;
using JPCenter.Modules.Crm;
using JPCenter.Modules.Finance;
using JPCenter.Modules.Hr;
using JPCenter.Modules.Library;
using JPCenter.Modules.Notifications;
using JPCenter.Modules.Students;
using JPCenter.Modules.System;
using JPCenter.Modules.Work;
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
    .AddRequestPipeline(builder.Configuration)
    .AddMessaging()
    .AddInfrastructureCommon()
    .AddScheduledJobs(builder.Configuration)
    .AddModules(builder.Configuration,
        new AcademicModule(),
        new CrmModule(),
        new FinanceModule(),
        new HrModule(),
        new LibraryModule(),
        new NotificationsModule(),
        new StudentsModule(),
        new SystemModule(),
        new WorkModule());

var app = builder.Build();

app.UseRequestPipeline();
app.UseSerilogRequestLogging();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapModules();
app.MapRealtimeHub();

if (app.Environment.IsDevelopment()) await app.Services.InitializeDatabaseAsync();

app.Run();

/// <summary>Cho WebApplicationFactory trong integration test.</summary>
public partial class Program;
