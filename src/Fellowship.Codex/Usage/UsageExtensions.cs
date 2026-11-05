using System.Security.Claims;

using AspNet.Security.OAuth.GitHub;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

using Fellowship.Codex.Components.Sections;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fellowship.Codex.Usage;

public static class UsageExtensions
{
    public const string OwnerPolicy = "owner";

    public const string Login = $"{UsageRoutes.Admin}/login";

    public const string Logout = $"{UsageRoutes.Admin}/logout";

    public const string Report = $"{UsageRoutes.Admin}/report";

    public const string Database = $"{UsageRoutes.Admin}/database";

    public const string Clear = $"{UsageRoutes.Admin}/clear";

    public const string ClearDays = "days";

    private const string DatabaseType = "application/vnd.sqlite3";

    private const int CopyBuffer = 64 * 1024;

    private const string Callback = $"{UsageRoutes.Admin}/signin-github";

    private const string CookieName = "codex-admin";

    private static readonly TimeSpan Session = TimeSpan.FromDays(7);

    public static UsageSettings AddUsage(this WebApplicationBuilder builder)
    {
        var settings = UsageSettings.From(builder.Configuration);
        var services = builder.Services;

        services.AddSingleton(settings);
        services.TryAddSingleton(TimeProvider.System);
        AdminAntiforgery.Register(services);

        if (settings.Counting)
        {
            services.AddSingleton<UsageCounter>();
            services.AddSingleton(new UsageStore(settings.Database!));
            services.AddHostedService<UsageFlush>();
            services.AddSingleton<ILoggerProvider, ErrorCounting>();
            services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(settings.Keys!));
        }

        // The cookie path keeps the sign-in cookie off every request outside the admin area.
        var authentication = services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = CookieName;
                options.Cookie.Path = UsageRoutes.Admin;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = Session;
                options.SlidingExpiration = true;
                options.LoginPath = Login;
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        if (settings.SignIn)
        {
            authentication.AddGitHub(options =>
            {
                options.ClientId = settings.ClientId!;
                options.ClientSecret = settings.ClientSecret!;
                options.CallbackPath = Callback;
                options.CorrelationCookie.Path = UsageRoutes.Admin;
            });
        }

        services.AddAuthorizationBuilder().AddPolicy(OwnerPolicy, policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim(ClaimTypes.NameIdentifier, settings.Owners));

        return settings;
    }

    /// <summary>Counts requests. It is first in the pipeline, so cached and rejected responses are counted.</summary>
    public static IApplicationBuilder UseUsage(this IApplicationBuilder app, UsageSettings settings) =>
        settings.Counting ? app.UseMiddleware<UsageMiddleware>() : app;

    public static IEndpointRouteBuilder MapAdmin(this IEndpointRouteBuilder app, UsageSettings settings)
    {
        app.MapGet(Login, (HttpContext http) => settings.SignIn
                ? Results.Challenge(new AuthenticationProperties { RedirectUri = UsageRoutes.Admin }, [GitHubAuthenticationDefaults.AuthenticationScheme])
                : Results.NotFound())
            .ExcludeFromDescription();

        // The report body alone, which the dashboard fetches to refresh in place.
        app.MapGet(Report, (int? days, HttpContext http) =>
            {
                http.Response.Headers.CacheControl = "no-store";

                return new RazorComponentResult<UsageReport>(
                    new Dictionary<string, object?> { [nameof(UsageReport.Days)] = UsageRanges.Pick(days) });
            })
            .RequireAuthorization(OwnerPolicy)
            .ExcludeFromDescription();

        app.MapPost(Logout, async (HttpContext http) =>
            {
                await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                return Results.LocalRedirect("/");
            })
            .WithMetadata(new RequireAntiforgeryTokenAttribute())
            .ExcludeFromDescription();

        if (settings.Counting) app.MapUsageData();

        return app;
    }

    private static void MapUsageData(this IEndpointRouteBuilder app)
    {
        app.MapGet(Database, (UsageStore store, TimeProvider time, HttpContext http) =>
            {
                var file = $"{store.Path}.{Guid.NewGuid():N}.copy";

                store.Snapshot(file);
                http.Response.Headers.CacheControl = "no-store";

                // The copy goes when the response closes this stream.
                var copy = new FileStream(
                    file, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBuffer, FileOptions.Asynchronous | FileOptions.DeleteOnClose);

                return Results.File(copy, DatabaseType, $"fellowship-codex-usage-{time.GetUtcNow():yyyy-MM-dd}.db");
            })
            .RequireAuthorization(OwnerPolicy)
            .ExcludeFromDescription();

        app.MapPost(Clear, ([FromForm(Name = ClearDays)] int days, UsageStore store, TimeProvider time) =>
            {
                if (days < 1) return Results.BadRequest();

                store.Clear(time.GetUtcNow() - TimeSpan.FromDays(days));

                return Results.LocalRedirect(UsageRoutes.Admin);
            })
            .RequireAuthorization(OwnerPolicy)
            .WithMetadata(new RequireAntiforgeryTokenAttribute())
            .ExcludeFromDescription();
    }
}
