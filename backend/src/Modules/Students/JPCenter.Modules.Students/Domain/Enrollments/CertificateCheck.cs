namespace JPCenter.Modules.Students.Domain.Enrollments;

/// <summary>1 dòng checklist điều kiện cấp chứng nhận (HV-15 bước 1) — <paramref name="Key"/>: completed · attendance · final_exam · tuition</summary>
public sealed record CertificateCheck(string Key, bool Ok, string Label);
