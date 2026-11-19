using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TradeDesk.Core.Domain;

namespace TradeDesk.Api;

/// <summary>Maps domain exceptions to RFC 7807 problem responses.</summary>
public class TradeExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        ProblemDetails? details = ex switch
        {
            TradeValidationException v => new ValidationProblemDetails(v.Errors.ToDictionary(e => e.Key, e => e.Value))
            {
                Status = StatusCodes.Status400BadRequest, Title = v.Message,
            },
            TradeNotFoundException => new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = ex.Message },
            TradeStateException => new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = ex.Message },
            _ => null,
        };
        if (details is null) return false;

        http.Response.StatusCode = details.Status!.Value;
        return await problems.TryWriteAsync(new() { HttpContext = http, ProblemDetails = details, Exception = ex });
    }
}
