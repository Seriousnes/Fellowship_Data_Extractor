namespace Fellowship.SDK.Tests;

public sealed class Checkout
{
    public FellowshipExport Export { get; } = FellowshipExport.Load(Output());

    private static string Output()
    {
        for (var here = new DirectoryInfo(AppContext.BaseDirectory); here is not null; here = here.Parent)
        {
            var output = Path.Combine(here.FullName, "output");

            if (Directory.Exists(output)) return output;
        }

        throw new DirectoryNotFoundException($"No output folder above {AppContext.BaseDirectory}.");
    }
}

[CollectionDefinition(Name)]
public sealed class Corpus : ICollectionFixture<Checkout>
{
    public const string Name = "corpus";
}
