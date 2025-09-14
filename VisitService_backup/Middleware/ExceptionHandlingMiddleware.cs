using System.Net;

namespace VisitService.Middleware
{
    public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                
                logger.LogInformation("Visitt API request {Method} {Path} ", 
                    context.Request.Method, context.Request.Path);
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var response = exception switch
            {
                ArgumentException _ => new {Status = HttpStatusCode.BadRequest.ToString(), ErrorMessage = exception.Message, ErrorNumber = (int)HttpStatusCode.BadRequest},
                KeyNotFoundException _ => new {Status = HttpStatusCode.NotFound.ToString(), ErrorMessage = 
                        string.IsNullOrEmpty(exception.Message)? "The request key not found." : exception.Message
                , ErrorNumber = (int)HttpStatusCode.NotFound},
                UnauthorizedAccessException _ => new {Status = HttpStatusCode.Forbidden.ToString(), ErrorMessage = exception.Message, ErrorNumber = (int)HttpStatusCode.Forbidden},
                _ => new {Status = HttpStatusCode.InternalServerError.ToString(), ErrorMessage = $"Internal server error. Please retry later. Message: " + exception.Message, ErrorNumber = (int)HttpStatusCode.InternalServerError}
            };

            var start = DateTime.UtcNow;

            var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;

            logger.LogError(exception, "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms. Error: {ErrorMessage}",
            context.Request.Method, context.Request.Path, response.ErrorNumber, elapsed, exception.Message);
            
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)response.ErrorNumber;
            await context.Response.WriteAsJsonAsync(response);
        }
    }
}
