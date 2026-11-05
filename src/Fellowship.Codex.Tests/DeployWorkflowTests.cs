using Fellowship.SDK;

namespace Fellowship.Codex.Tests;

public class DeployWorkflowTests
{
    private static readonly string Workflow =
        Path.Combine(Project.Repository, ".github", "workflows", "deploy.yml");

    private static readonly string[] Lines = File.ReadAllLines(Workflow);

    private static string Prefix(string key)
    {
        foreach (var line in Lines)
        {
            var read = line.Trim();

            if (read.StartsWith($"{key}:", StringComparison.Ordinal)) return read[(key.Length + 1)..].Trim();
        }

        throw new InvalidOperationException(
            $"The deploy workflow names no {key}, which is the bucket prefix the codex reads.");
    }

    [Fact]
    public void The_workflow_publishes_the_export_under_the_folder_the_codex_lists()
    {
        Assert.Equal(ExportReader.DataFolder, Prefix("EXPORTS"));
    }

    [Fact]
    public void The_workflow_publishes_the_textures_under_the_folder_a_texture_name_composes_with()
    {
        Assert.Equal(TextureSizes.Folder, Prefix("TEXTURES"));
    }

    private static readonly string[] Robots =
        File.ReadAllLines(Path.Combine(Project.Folder, "wwwroot", "robots.txt"));

    private static IEnumerable<string> Rule(string name) =>
        Robots.Where(line => line.StartsWith($"{name}:", StringComparison.Ordinal)).Select(line => line[(name.Length + 1)..].Trim());

    [Fact]
    public void The_robots_sitemap_line_names_the_site_the_workflow_writes_the_sitemap_for()
    {
        var site = Prefix("SITE");

        Assert.Equal($"{site}/{Sitemap.FileName}", Assert.Single(Rule("Sitemap")));
        Assert.StartsWith($"{site}/", Prefix("HEALTH_URL"), StringComparison.Ordinal);
    }

    [Fact]
    public void Robots_keep_crawlers_out_of_the_admin_area_and_let_previews_read_cards()
    {
        Assert.Contains(Usage.UsageRoutes.Admin, Rule("Disallow"));
        Assert.Contains($"/{Cards.Prefix}/", Rule("Allow"));
    }

    [Fact]
    public void The_workflow_publishes_both_files_a_build_folder_carries()
    {
        var text = string.Join('\n', Lines);

        Assert.Contains(ExportReader.EntitiesName, text, StringComparison.Ordinal);
        Assert.Contains(ExportReader.SettingsName, text, StringComparison.Ordinal);
    }
}
