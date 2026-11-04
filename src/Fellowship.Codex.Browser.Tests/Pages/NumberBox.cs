namespace Fellowship.Codex.Browser.Tests.Pages;

/// <summary>A number input, driven by key presses so each one raises its own input event.</summary>
public sealed class NumberBox(ILocator input)
{
    public ILocator Input { get; } = input;

    public Task<string> ValueAsync() => Input.InputValueAsync();

    public Task PressAsync(string key) => Input.PressAsync(key);

    public Task TypeAsync(string text) => Input.PressSequentiallyAsync(text);

    /// <summary>Selects the whole text, then types <paramref name="text"/> over it key by key.</summary>
    public async Task ReplaceAsync(string text)
    {
        await Input.SelectTextAsync();
        await Input.PressSequentiallyAsync(text);
    }

    /// <summary>Empties the box and leaves it, which raises its change event.</summary>
    public async Task ClearAndCommitAsync()
    {
        await Input.SelectTextAsync();
        await Input.PressAsync("Backspace");
        await Input.PressAsync("Tab");
    }

    /// <summary>Types <paramref name="text"/> over the whole text and leaves the box, which raises its change event.</summary>
    public async Task CommitAsync(string text)
    {
        await ReplaceAsync(text);
        await Input.PressAsync("Tab");
    }
}
