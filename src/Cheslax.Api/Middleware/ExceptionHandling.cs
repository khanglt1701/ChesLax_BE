using Cheslax.Application.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace Cheslax.Api.Middleware;

public static class ExceptionHandling
{
    public static IApplicationBuilder UseCheslaxExceptionHandler(this IApplicationBuilder app)
    {
        app.UseExceptionHandler(handler => handler.Run(async context =>
        {
            var feature = context.Features.Get<IExceptionHandlerFeature>();
            var exception = feature?.Error;
            var env = context.RequestServices.GetService<IHostEnvironment>();
            var (status, error) = Map(exception, env?.IsDevelopment() == true);
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error.Code, error.Message, error.Fields });
        }));

        return app;
    }

    private static (int Status, ApiError Error) Map(Exception? exception, bool detailed) => exception switch
    {
        CheslaxException ex => (ex.StatusCode, new ApiError(ex.Code, ex.Message)),
        UnauthorizedAccessException => (401, new ApiError("UNAUTHORIZED", "Cần đăng nhập.")),
        ValidationException ex => (400, new ApiError(
            "VALIDATION",
            "Dữ liệu không hợp lệ.",
            ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))),
        ArgumentException ex => (400, new ApiError("INVALID", ex.Message)),
        _ => (500, new ApiError("INTERNAL", detailed && exception is not null ? exception.ToString() : "Đã xảy ra lỗi. Thử lại sau.")),
    };
}
