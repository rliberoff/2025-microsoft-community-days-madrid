using System.Net.Http.Headers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

namespace Demo;

internal sealed class BearerAuthenticationProviderWithCancellationToken
{
    private readonly IPublicClientApplication client;
    private readonly ILogger<BearerAuthenticationProviderWithCancellationToken> logger;

    public BearerAuthenticationProviderWithCancellationToken(IConfiguration configuration, ILogger<BearerAuthenticationProviderWithCancellationToken> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var clientId = configuration[@"MSGraph:ClientId"];
        var tenantId = configuration[@"MSGraph:TenantId"];

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(tenantId))
        {
            throw new InvalidOperationException(@"Please provide valid MSGraph configuration!");
        }

        this.client = PublicClientApplicationBuilder.Create(clientId)
                                                    .WithAuthority($"https://login.microsoftonline.com/{tenantId}")
                                                    .WithDefaultRedirectUri()
                                                    .Build();
    }

    public async Task AuthenticateRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue(@"Bearer", token);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var scopes = new string[] { @"https://graph.microsoft.com/.default" };

        logger.LogInformation(@"Attempting to acquire token silently.");

        try
        {
            var authResult = await client.AcquireTokenSilent(scopes, (await client.GetAccountsAsync().ConfigureAwait(false)).FirstOrDefault())
                                         .ExecuteAsync(cancellationToken)
                                         .ConfigureAwait(false);

            logger.LogInformation(@"Token acquired silently.");

            return authResult.AccessToken;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, @"Silent token acquisition failed: {Message}. Attempting device code flow.", exception.Message);

            var authResult = await client.AcquireTokenWithDeviceCode(scopes, deviceCodeResult =>
            {
                Console.WriteLine(deviceCodeResult.Message);
                return Task.CompletedTask;
            }).ExecuteAsync(cancellationToken)
              .ConfigureAwait(false);

            logger.LogInformation(@"Token acquired via device code flow.");

            return authResult.AccessToken;
        }
    }
}
