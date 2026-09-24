# Codex.FellowshipAnalyzer.SDK

All entity documents and API request/response models, plus a strongly-typed .NET API client for accessing codex.fellowshipanalyzer.com.

## Install the package

The package is published to GitHub Packages.

1. Create a GitHub personal access token (classic) with the `read:packages` scope. For more
   information about creating one, see
   [Managing your personal access tokens](https://docs.github.com/en/authentication/keeping-your-account-secure/managing-your-personal-access-tokens).

2. Add the feed as a NuGet source:

   ```powershell
   dotnet nuget add source https://nuget.pkg.github.com/Seriousnes/index.json --name fellowship --username GITHUB_USERNAME --password GITHUB_TOKEN --store-password-in-clear-text
   ```

   Replace the following:

   - `GITHUB_USERNAME`: your GitHub account name
   - `GITHUB_TOKEN`: the personal access token

3. Add the package to your project:

   ```powershell
   dotnet add package Codex.FellowshipAnalyzer.SDK
   ```

## Read the codex

`CodexClient` takes an `HttpClient` and addresses codex.fellowshipanalyzer.com unless the
`HttpClient` already has a `BaseAddress`:

```csharp
using Fellowship.SDK;
using Fellowship.SDK.Client;
using Fellowship.SDK.Records;

using var http = new HttpClient();
var client = new CodexClient(http);

SearchResponse response = await client.SearchAsync("Helena");
SearchResult first = response.Results[0];

AbilityRecord ability = await client.GetAsync<AbilityRecord>(id: 1);
EntityRecord entity = await client.GetAsync(EntityType.Item, id: 1);
```

`TooltipAsync` returns a self-contained HTML element to render the tooltip:

```csharp
string tooltipHtml = await codex.TooltipAsync<AbilityTooltipRequest>(
    id: 1, new AbilityTooltipRequest { Hero = "Ardeos", ShowIcon = true });
```
