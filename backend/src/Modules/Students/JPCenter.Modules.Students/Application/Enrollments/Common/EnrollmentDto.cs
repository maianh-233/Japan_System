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
namespace JPCenter.Modules.Students.Application.Enrollments.Common;

/// <summary>Ghi danh — khớp <c>Enrollment</c> ở <c>frontend/src/features/student/types.ts</c> (+ mã lớp, chi nhánh lớp)</summary>
public sealed record EnrollmentDto(
    Guid Id,
    Guid StudentId,
    Guid ClassId,
    string? ClassCode,
    Guid BranchId,
    EnrollmentStatus Status,
    DateOnly EnrolledAt,
    DateOnly? EndedAt,
    string? Note,
    DateOnly? ReservedUntil,
    Guid? TransferredTo,
    Guid? TransferredFrom,
    CertificateDto? Certificate);

/// <summary>Chứng nhận hoàn thành — <c>sentBy</c> là tên người gửi</summary>
public sealed record CertificateDto(string Code, DateTimeOffset IssuedAt, string SentTo, string SentBy, int SendCount, string? Exception);

/// <summary>Kết quả thao tác ghi danh kèm cảnh báo không chặn</summary>
public sealed record EnrollmentResult(IReadOnlyList<EnrollmentDto> Enrollments, IReadOnlyList<string> Warnings);
