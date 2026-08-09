using Fellowship.SDK.Documents;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using FellowshipExtractor.Api.Models;

namespace FellowshipExtractor.Api.Services;

public interface IFragmentService
{
    Task<string> Render<T>(Dictionary<string, object?> parameters) where T : IComponent;
}

public sealed class FragmentService(IServiceScopeFactory scopes, ILoggerFactory loggers) : IFragmentService
{
    public async Task<string> Render<T>(Dictionary<string, object?> parameters)
        where T : IComponent
    {
        await using var scope = scopes.CreateAsyncScope();

        await using var renderer = new HtmlRenderer(scope.ServiceProvider, loggers);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var rendered = await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters));

            return rendered.ToHtmlString();
        });
    }
}
