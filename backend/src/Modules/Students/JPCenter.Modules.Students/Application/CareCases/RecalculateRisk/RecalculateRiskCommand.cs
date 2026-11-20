using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Students.Application.CareCases.Common;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Application.CareCases.RecalculateRisk;

/// <summary>HV-14.E4 — tính lại nguy cơ bỏ học cho mọi HV đang học (job hằng đêm, hoặc nút "Tính lại" của nhân viên). Trả số HV × lớp có dấu hiệu.</summary>
public sealed record RecalculateRiskCommand : ICommand<int>;

public sealed class RecalculateRiskHandler(RiskCalculator calculator) : ICommandHandler<RecalculateRiskCommand, int>
{
    public async Task<Result<int>> Handle(RecalculateRiskCommand cmd, CancellationToken ct) => await calculator.RecalculateAsync(null, null, ct);
}
