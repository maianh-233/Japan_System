using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Application.Common.Messaging;

/// <summary>Lệnh thay đổi dữ liệu, không trả giá trị.</summary>
public interface ICommand : IRequest<Result>;

/// <summary>Lệnh thay đổi dữ liệu, trả giá trị (vd Id vừa tạo).</summary>
public interface ICommand<TValue> : IRequest<Result<TValue>>;
