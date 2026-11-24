using JPCenter.Modules.Students.Application.Jlpt.Common;

namespace JPCenter.Modules.Students.Application.Jlpt.GetMyJlpt;

/// <summary>Cổng HV / PH — kỳ thi + lượt đăng ký của HV (bỏ ghi chú nội bộ)</summary>
public sealed record MyJlptDto(IReadOnlyList<JlptSessionDto> Sessions, IReadOnlyList<JlptRegistrationDto> Registrations)
{
    public static JlptRegistrationDto Public(JlptRegistrationDto r) => r with { Note = null };
}
