using Microsoft.Extensions.Logging;

using Microsoft.SemanticKernel;

namespace Demo;

internal sealed class ExpectedSchemaFunctionFilter(ILogger logger) : IAutoFunctionInvocationFilter
{
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    public async Task OnAutoFunctionInvocationAsync(AutoFunctionInvocationContext context, Func<AutoFunctionInvocationContext, Task> next)
    {
        try
        {
            await next(context).ConfigureAwait(false);

            if (context.Result.ValueType == typeof(RestApiOperationResponse))
            {
                var openApiResponse = context.Result.GetValue<RestApiOperationResponse>();
                if (openApiResponse?.ExpectedSchema is not null)
                {
                    openApiResponse.ExpectedSchema = null;
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, @"There was an error during a function invocation. Error was: {ErrorMessage}", exception.Message);
        }
    }
}
