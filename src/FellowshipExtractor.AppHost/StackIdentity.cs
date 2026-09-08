using System.Globalization;
using System.Security.Cryptography;
using System.Text;

static class StackIdentity
{
    public const string SharedVolume = "fellowship-azurite";

    public const string SharedContainer = "fellowship-azurite";

    public const string SharedProject = "fellowship";

    public const int SharedBlobPort = 10000;

    public static (string Project, int Offset, string Root) Stack(string from)
    {
        var checkout = Checkout(from);
        var root = checkout?.Root ?? from;

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(root.ToLowerInvariant()));
        var name = Convert.ToHexString(digest)[..8].ToLowerInvariant();

        var offset = Environment.GetEnvironmentVariable("FELLOWSHIP_PORT_OFFSET") is { Length: > 0 } chosen &&
            int.TryParse(chosen, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pinned)
            ? pinned
            : (int.Parse(name[..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture) % 89 + 1) * 10;

        var project = $"fellowship-{checkout?.Name ?? name}";

        return (project, offset, root);
    }

    public static (string Root, string? Name)? Checkout(string from)
    {
        for (var directory = new DirectoryInfo(from); directory is not null; directory = directory.Parent)
        {
            var marker = Path.Combine(directory.FullName, ".git");

            if (Directory.Exists(marker))
                return (directory.FullName, Branch(marker) ?? Slug(directory.Name));

            if (!File.Exists(marker)) continue;

            var pointer = File.ReadAllText(marker).Trim();
            if (!pointer.StartsWith("gitdir:", StringComparison.Ordinal)) continue;

            var linked = Path.GetFullPath(pointer["gitdir:".Length..].Trim(), directory.FullName);
            return (directory.FullName, Branch(linked) ?? Slug(directory.Name));
        }

        return null;
    }

    public static string? Branch(string gitDirectory)
    {
        var head = Path.Combine(gitDirectory, "HEAD");
        if (!File.Exists(head)) return null;

        var reference = File.ReadAllText(head).Trim();
        return reference.StartsWith("ref: refs/heads/", StringComparison.Ordinal)
            ? Slug(reference["ref: refs/heads/".Length..])
            : null;
    }

    public static string? Slug(string text)
    {
        var slug = new StringBuilder();

        foreach (var character in text.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character)) slug.Append(character);
            else if (slug.Length > 0 && slug[^1] != '-') slug.Append('-');
        }

        return slug.ToString().Trim('-') is { Length: > 0 } trimmed ? trimmed : null;
    }

    public static void Rebind(string variable, int offset, params int[] basePorts)
    {
        var current = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrEmpty(current)) return;

        var moved = current.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(address =>
            {
                if (!Uri.TryCreate(address.Trim(), UriKind.Absolute, out var uri)) return address;
                if (!basePorts.Contains(uri.Port)) return address;

                return new UriBuilder(uri) { Port = uri.Port + offset }.Uri.ToString().TrimEnd('/');
            });

        Environment.SetEnvironmentVariable(variable, string.Join(';', moved));
    }

    public static int Port(IDistributedApplicationBuilder builder, string key, int offset, int basePort) =>
        int.TryParse(builder.Configuration[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out var chosen)
            ? chosen
            : basePort + offset;
}

sealed record SharedStackAnnotation(string Container, string Project) : IResourceAnnotation;

static class ComposeStackExtensions
{
    public static IResourceBuilder<T> InSharedStack<T>(
        this IResourceBuilder<T> builder, string container, string project) where T : IResource =>
        builder.WithAnnotation(new SharedStackAnnotation(container, project));

    public static IDistributedApplicationBuilder InComposeStack(
        this IDistributedApplicationBuilder builder, string project, string workingDirectory)
    {
        foreach (var resource in builder.Resources)
        {
            if (!resource.Annotations.OfType<ContainerImageAnnotation>().Any() &&
                !resource.Annotations.OfType<DockerfileBuildAnnotation>().Any()) continue;

            var shared = resource.Annotations.OfType<SharedStackAnnotation>().FirstOrDefault();

            var service = resource.Name;
            var stack = shared?.Project ?? project;
            var container = shared?.Container ?? $"{project}-{service}";
            var root = shared is null ? workingDirectory : null;

            foreach (var annotation in resource.Annotations.OfType<ContainerNameAnnotation>().ToArray())
                resource.Annotations.Remove(annotation);

            resource.Annotations.Add(new ContainerNameAnnotation { Name = container });
            resource.Annotations.Add(new ContainerRuntimeArgsCallbackAnnotation(runtimeArgs =>
            {
                runtimeArgs.Add("--label");
                runtimeArgs.Add($"com.docker.compose.project={stack}");

                if (root is not null)
                {
                    runtimeArgs.Add("--label");
                    runtimeArgs.Add($"com.docker.compose.project.working_dir={root}");
                }

                runtimeArgs.Add("--label");
                runtimeArgs.Add($"com.docker.compose.service={service}");
                runtimeArgs.Add("--label");
                runtimeArgs.Add("com.docker.compose.oneoff=False");
            }));
        }

        return builder;
    }
}
