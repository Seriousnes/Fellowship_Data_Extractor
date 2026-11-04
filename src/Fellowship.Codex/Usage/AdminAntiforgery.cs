using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fellowship.Codex.Usage;

/// <summary>
/// Issues an antiforgery token inside the admin area alone. A public page has no form that posts,
/// so it receives no token, no antiforgery cookie and no <c>no-store</c> header the token would force.
/// </summary>
public sealed class AdminAntiforgery(IHttpContextAccessor accessor, IAntiforgery antiforgery) : AntiforgeryStateProvider
{
    private AntiforgeryRequestToken? token;

    public override AntiforgeryRequestToken? GetAntiforgeryToken()
    {
        if (token is not null) return token;

        if (accessor.HttpContext is not { } http || !http.Request.Path.StartsWithSegments(UsageRoutes.Admin)) return null;

        var tokens = antiforgery.GetAndStoreTokens(http);

        return token = tokens.RequestToken is { } request ? new AntiforgeryRequestToken(request, tokens.FormFieldName) : null;
    }

    public static void Register(IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddAntiforgery(options => options.Cookie.Path = UsageRoutes.Admin);

        // AddRazorComponents registers its own provider, which a plain Add would not displace.
        services.Replace(ServiceDescriptor.Scoped<AntiforgeryStateProvider, AdminAntiforgery>());
    }
}
