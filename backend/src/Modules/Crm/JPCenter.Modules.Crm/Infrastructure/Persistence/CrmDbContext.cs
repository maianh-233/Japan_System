using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Domain.Leads;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Infrastructure.Persistence;

/// <summary>
/// DbContext module Crm — schema <c>crm</c>. Giai đoạn 15 mới lưu lead + lịch sử tư vấn + liên kết lead ↔ học viên
/// (cổng PH thấy con mình); lịch hẹn, chiến dịch, quy tắc chia lead, test xếp lớp, tin nhắn thêm ở giai đoạn 18.
/// </summary>
public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options) : ModuleDbContextBase(options), ICrmDbContext
{
    public const string SchemaName = "crm";

    protected override string Schema => SchemaName;

    public DbSet<Lead> Leads => Set<Lead>();
}
