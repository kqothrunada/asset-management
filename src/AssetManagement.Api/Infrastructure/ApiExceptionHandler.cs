using AssetManagement.Application.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Infrastructure
{
    public class ApiExceptionHandler : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
        {
            ProblemDetails problem;

            switch (ex)
            {
                case ValidationException ve:
                    var errors = ve.Errors.GroupBy(e => e.PropertyName)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                    problem = new ValidationProblemDetails(errors) { Status = 400, Title = "Failed validation" };
                    break;
                case NotFoundException:
                    problem = new ProblemDetails { Status = 404, Title = "Not found", Detail = ex.Message };
                    break;
                case ForbiddenException:
                    problem = new ProblemDetails { Status = 403, Title = "Access denied", Detail = ex.Message };
                    break;
                case BusinessRuleException:
                    problem = new ProblemDetails { Status = 409, Title = "Violating business rules", Detail = ex.Message };
                    break;
                default:
                    if (!http.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
                        return false;
                    http.Response.StatusCode = 500;
                    await http.Response.WriteAsJsonAsync(new ProblemDetails
                    {
                        Status = 500,
                        Title = "Unexpected Error",
                        Detail = ex.ToString()
                    }, ct);
                    return true;
            }

            http.Response.StatusCode = problem.Status!.Value;
            await http.Response.WriteAsJsonAsync(problem, ct);
            return true;
        }
    }
}
