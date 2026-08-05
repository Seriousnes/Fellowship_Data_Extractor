global using AngleSharp.Dom;
global using Bunit;
global using FellowshipExtractor.Api;
global using FellowshipExtractor.Api.Components.Cx;
global using FellowshipExtractor.Api.Components.Sections;
global using FellowshipExtractor.Api.Components.Shared;
global using Microsoft.Extensions.DependencyInjection;
global using Xunit;

// AngleSharp.Dom also declares an Entity - a DOM entity node - so the bare name is ambiguous in
// every test that builds a record to cascade. The alias pins it to the domain type, which is what
// each of them means, and leaves AngleSharp's DOM API reachable for the assertions that read it.
global using Entity = Fellowship.SDK.Entity;
