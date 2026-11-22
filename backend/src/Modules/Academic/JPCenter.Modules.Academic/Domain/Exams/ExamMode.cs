namespace JPCenter.Modules.Academic.Domain.Exams;

/// <summary>Hình thức kỳ thi (ND-04)</summary>
public enum ExamMode
{
    /// <summary>HV làm trên Cổng trong khung giờ thi, tự chấm</summary>
    Online,
    /// <summary>In đề, thi tại lớp, GV / giáo vụ nhập điểm từng phần</summary>
    Paper,
}
