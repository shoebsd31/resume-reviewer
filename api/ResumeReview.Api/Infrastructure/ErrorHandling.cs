using System.Diagnostics;
using System.Text.Json;

namespace ResumeReview.Api.Infrastructure;

public static class ErrorHandling
{
    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder app)
    {
        app.Use(async (ctx, next) =>
        {
            try
            {
                await next();
            }
            catch (Exception ex)
            {
                var traceId = Activity.Current?.Id ?? ctx.TraceIdentifier;
                ctx.Response.StatusCode = 500;
                ctx.Response.ContentType = "application/json";
                var body = JsonSerializer.Serialize(new
                {
                    error = "internal_error",
                    message = ex.Message,
                    traceId
                });
                await ctx.Response.WriteAsync(body);
            }
        });
        return app;
    }
}
