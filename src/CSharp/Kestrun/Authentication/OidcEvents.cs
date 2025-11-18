
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Kestrun.Authentication;
/// <summary>
/// OpenID Connect events to handle client assertion injection.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="OidcEvents"/> class.
/// </remarks>
/// <param name="assertionService">The assertion service used to create client assertions. </param>
public class OidcEvents(AssertionService assertionService) : OpenIdConnectEvents
{
    private readonly AssertionService _assertionService = assertionService;

    /// <summary>
    /// Handles the AuthorizationCodeReceived event to inject client assertions.
    /// </summary>
    /// <param name="context">The context for the AuthorizationCodeReceived event.</param>
    /// <returns>A completed task.</returns>
    public override Task AuthorizationCodeReceived(AuthorizationCodeReceivedContext context)
    {
        var tokenEndpoint =
            context.Options.Configuration?.TokenEndpoint
            ?? (context.Options.Authority?.TrimEnd('/') + "/connect/token");
        if (context.TokenEndpointRequest is not null)
        {
            context.TokenEndpointRequest.ClientAssertionType =
                "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";

            context.TokenEndpointRequest.ClientAssertion =
                _assertionService.CreateClientAssertion(tokenEndpoint);
        }
        return Task.CompletedTask;
    }
}
