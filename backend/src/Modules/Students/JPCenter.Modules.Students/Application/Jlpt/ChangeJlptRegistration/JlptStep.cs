namespace JPCenter.Modules.Students.Application.Jlpt.ChangeJlptRegistration;

/// <summary>Bước tiến độ đăng ký thi / chứng chỉ gốc — đường dẫn <c>jlpt-registrations/{id}/{action}</c></summary>
public enum JlptStep
{
    Pay,
    Submit,
    Admit,
    Result,
    Absent,
    Cancel,
    CertReceived,
    CertHanded,
}
