using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace JPCenter.Infrastructure.Common.Jobs;

/// <summary>
/// Bật Quartz scheduler cho job định kỳ (1 instance, lưu lịch trong bộ nhớ). Module tự khai báo job bằng
/// <c>services.AddQuartz(q =&gt; q.ScheduleJob&lt;TJob&gt;(...))</c> khi <see cref="IsEnabled"/>.
/// Tắt bằng <c>Jobs:Enabled=false</c> (test tích hợp tắt để nhiều host trong 1 tiến trình không tranh scheduler).
/// </summary>
public static class JobServiceCollectionExtensions
{
    public static bool IsEnabled(IConfiguration configuration) => configuration.GetValue("Jobs:Enabled", true);

    public static IServiceCollection AddScheduledJobs(this IServiceCollection services, IConfiguration configuration)
    {
        if (!IsEnabled(configuration)) return services;
        services.AddQuartz(_ => { });
        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);
        return services;
    }
}
