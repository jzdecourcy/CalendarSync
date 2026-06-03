using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace CalendarSync;

internal sealed record SyncConfig(
    string SourceUrl,
    string OutputPath,
    string? CalendarName,
    string[] IncludePatterns);

internal static class Program
{
    private static readonly HttpClient Http = new();

    private static async Task<int> Main()
    {
        try
        {
            SyncConfig config = LoadConfig();

            Console.WriteLine($"Fetching: {config.SourceUrl}");

            string source = await Http.GetStringAsync(config.SourceUrl);

            Console.WriteLine($"Source size: {source.Length:N0} chars");

            CalendarFilter.Result result =
                CalendarFilter.Filter(source, config.IncludePatterns, config.CalendarName);

            string outputPath = ResolveOutputPath(config.OutputPath);

            string? directory = Path.GetDirectoryName(outputPath);

            if (!String.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(outputPath, result.Output);

            Console.WriteLine($"Kept {result.Kept} event(s), dropped {result.Dropped}.");

            Console.WriteLine($"Wrote: {outputPath}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");

            return 1;
        }
    }

    private static SyncConfig LoadConfig()
    {
        string configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        string json = File.ReadAllText(configPath);

        SyncConfig config = JsonSerializer.Deserialize<SyncConfig>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("appsettings.json could not be parsed.");

        // Allow CI to override source/output without editing the committed config.
        string? urlOverride = Environment.GetEnvironmentVariable("CALSYNC_SOURCE_URL");

        string? outOverride = Environment.GetEnvironmentVariable("CALSYNC_OUTPUT_PATH");

        return config with
        {
            SourceUrl = String.IsNullOrEmpty(urlOverride) ? config.SourceUrl : urlOverride,
            OutputPath = String.IsNullOrEmpty(outOverride) ? config.OutputPath : outOverride
        };
    }

    private static string ResolveOutputPath(string outputPath)
    {
        if (Path.IsPathRooted(outputPath))
        {
            return outputPath;
        }

        // Resolve relative paths against the repository root (two levels above the project).
        string? repoRoot = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");

        if (String.IsNullOrEmpty(repoRoot))
        {
            repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        }

        return Path.GetFullPath(Path.Combine(repoRoot, outputPath));
    }
}
