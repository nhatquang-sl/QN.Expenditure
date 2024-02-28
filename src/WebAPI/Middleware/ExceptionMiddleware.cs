using Application.Common.Exceptions;
using Application.Common.Logging;
using System.Net;

namespace WebAPI.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;

        public ExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ILogTrace logTrace)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                var type = ex.GetType();
                context.Response.ContentType = "application/json";

                if (type == typeof(BadRequestException))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                }
                else if (type == typeof(NotFoundException))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                }
                else if (type == typeof(ConflictException))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                }

                await context.Response.WriteAsync(context.Response.StatusCode != (int)HttpStatusCode.InternalServerError ? ex.Message : "Internal Server Error");
            }
        }
    }

    public class BadRequest
    {
        public string Message { get; set; }
    }

    public class Conflict
    {
        public string Message { get; set; }
    }

    public class NotFound
    {
        public string Message { get; set; }
    }

    public static class ExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseExceptionMiddleware(
            this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ExceptionMiddleware>();
        }
    }
}
