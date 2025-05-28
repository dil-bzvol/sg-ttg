using System.Net;
using System.Text;
using Microsoft.AspNetCore.HeaderPropagation;
using Microsoft.Extensions.Primitives;

namespace TranslatedTemplateGenerator.Middlewares;

internal class ErrorHandlingMiddleware(
    RequestDelegate next,
    ILogger<ErrorHandlingMiddleware> logger,
    HeaderPropagationValues headerPropagationValues)
{
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await context.Response.WriteAsJsonAsync(GenerateErrorMessage(ex));
            logger.LogError(ex, "Unhandled exception has been occured");
        }
    }

    private DetailedErrorMessage GenerateErrorMessage(Exception exception)
    {
        var headers = headerPropagationValues.Headers ??=
            new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);

        const string correlationIdHeader = "X-Correlation-ID";
        if (!headers.TryGetValue(correlationIdHeader, out var correlationId))
            correlationId = Guid.NewGuid().ToString();

        return new DetailedErrorMessage
        {
            CorrelationId = correlationId,
            Error = GenerateExceptionMessageWithStackTrace(exception)
        };
    }

    private static string GenerateExceptionMessageWithStackTrace(Exception exception)
    {
        const int maxDepth = 5;
        var sb = new StringBuilder();

        sb.Append(exception.GetType());
        sb.Append(": ");
        sb.Append(exception.Message);

        var innerException = exception.InnerException;
        var i = maxDepth;
        while (innerException != null && i-- > 0)
        {
            sb.AppendLine();
            sb.Append(" ---> ");
            sb.Append(innerException.GetType());
            sb.Append(": ");
            sb.Append(innerException.Message);
            innerException = innerException.InnerException;
        }

        return sb.ToString();
    }

    private class DetailedErrorMessage : ErrorMessage
    {
        /// <summary>
        /// Stringified JSON blob with human friendly property names
        /// </summary>
        public string ErrorData { get; set; } = default!;
    }

    /// <summary>
    /// Models the data for the error page.
    /// </summary>
    private class ErrorMessage
    {
        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        /// <value>
        /// The error code.
        /// </value>
        public string Error { get; set; } = default!;

        /// <summary>
        /// Gets or sets the error description.
        /// </summary>
        /// <value>
        /// The error description.
        /// </value>
        public string? ErrorDescription { get; set; }

        /// <summary>
        /// The per-request identifier. This can be used to display to the end user and can be used in diagnostics.
        /// </summary>
        /// <value>
        /// The request identifier.
        /// </value>
        public string? CorrelationId { get; set; }
    }
}


public static class ErrorHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder applicationBuilder) => 
        applicationBuilder.UseMiddleware<ErrorHandlingMiddleware>();
}