using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Http;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace JPCenter.Api.Common.Results;

/// <summary>Đổi <see cref="Result"/> thành HTTP — endpoint KHÔNG tự chọn mã HTTP.</summary>
public static class ResultExtensions
{
    /// <summary>Thành công → 204 No Content.</summary>
    public static IResult ToHttpResult(this Result result) =>
        result.IsSuccess ? HttpResults.NoContent() : result.Error.ToProblem();

    /// <summary>Thành công → 200 kèm dữ liệu.</summary>
    public static IResult ToHttpResult<T>(this Result<T> result) =>
        result.IsSuccess ? HttpResults.Ok(result.Value) : result.Error.ToProblem();

    /// <summary>Thành công → 201 Created, header Location do <paramref name="location"/> dựng từ giá trị.</summary>
    public static IResult ToCreatedResult<T>(this Result<T> result, Func<T, string> location) =>
        result.IsSuccess ? HttpResults.Created(location(result.Value), result.Value) : result.Error.ToProblem();

    public static IResult ToProblem(this Error error) => new ErrorHttpResult(error);
}
