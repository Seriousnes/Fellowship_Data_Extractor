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

    [Fact]
    public void The_workflow_publishes_both_files_a_build_folder_carries()
    {
        var text = string.Join('\n', Lines);

        Assert.Contains(ExportReader.EntitiesName, text, StringComparison.Ordinal);
        Assert.Contains(ExportReader.SettingsName, text, StringComparison.Ordinal);
    }
}
