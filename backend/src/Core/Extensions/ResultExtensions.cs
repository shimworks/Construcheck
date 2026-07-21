using Microsoft.AspNetCore.Mvc;
using Construcheck.SharedKernel;

namespace Construcheck.Core.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(
        this Result<T> result,
        ControllerBase controller)
    {
        if (result.IsSuccess)
            return controller.Ok(result.Value);

        return result.ErrorType switch
        {
            ResultErrorType.NotFound => controller.NotFound(new { error = result.Error }),
            ResultErrorType.Validation => controller.BadRequest(new { error = result.Error }),
            ResultErrorType.Conflict => controller.Conflict(new { error = result.Error }),
            ResultErrorType.Unauthorized => controller.Unauthorized(new { error = result.Error }),
            _ => controller.StatusCode(500, new { error = result.Error })
        };
    }
}