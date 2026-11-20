using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Application.Enrollments.Common;
using JPCenter.Modules.Students.Application.Students.Common;
using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Ev = JPCenter.Modules.Students.Contracts.IntegrationEvents;
namespace JPCenter.Modules.Students.Application.Students.Common;

/// <summary>
/// Hồ sơ học viên — khớp <c>Student</c> ở <c>frontend/src/features/crm/types.ts</c> (+ trạng thái học suy từ ghi danh, lớp đang học, version).
/// <paramref name="StudyStatus"/>: studying › reserved › finished › dropped › none (quy tắc vàng 3 — không lưu cột riêng).
/// </summary>
public sealed record StudentDto(
    Guid Id,
    string Code,
    string FullName,
    Gender? Gender,
    DateOnly? Dob,
    string? Phone,
    string? Email,
    string? Address,
    string? Note,
    Guid BranchId,
    DateOnly JoinedAt,
    bool Hidden,
    DateTimeOffset? HiddenAt,
    string StudyStatus,
    IReadOnlyList<Guid> ActiveClassIds,
    uint RowVersion);

/// <summary>Kết quả thao tác hàng loạt: làm được / bỏ qua kèm lý do</summary>
public sealed record SkippedStudentDto(Guid Id, string Code, string Reason);
