using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SOGMAPanelManager;

internal sealed class SogmaUpdateInfo
{
    public required Version Version { get; init; }
    public required string DownloadUrl { get; init; }
    public string Notes { get; init; } = string.Empty;
}

internal static class UpdaterService
{
    public const string Repository = "LuissMaker/SOGMA-Panel-Manager";
    public static readonly Version CurrentVersion = new(1, 0, 8);

    private const string ExpectedAssetPrefix = "SOGMA-Panel-Manager-v";
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SOGMA-Panel-Manager", CurrentVersion.ToString()));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    public static async Task<SogmaUpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        string api = $"https://api.github.com/repos/{Repository}/releases/latest";
        using HttpResponseMessage response = await Http.GetAsync(api, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        JsonElement root = doc.RootElement;
        string tag = root.TryGetProperty("tag_name", out JsonElement tagElement)
            ? tagElement.GetString() ?? string.Empty
            : string.Empty;
        string cleanVersion = tag.Trim().TrimStart('v', 'V');

        if (!Version.TryParse(cleanVersion, out Version? remoteVersion) || remoteVersion <= CurrentVersion)
            return null;

        string notes = root.TryGetProperty("body", out JsonElement bodyElement)
            ? bodyElement.GetString() ?? string.Empty
            : string.Empty;

        if (!root.TryGetProperty("assets", out JsonElement assets) || assets.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("La publicación de GitHub no contiene archivos de actualización.");

        string? downloadUrl = null;
        string exactAsset = $"{ExpectedAssetPrefix}{remoteVersion}.zip";

        foreach (JsonElement asset in assets.EnumerateArray())
        {
            string name = asset.TryGetProperty("name", out JsonElement nameElement)
                ? nameElement.GetString() ?? string.Empty
                : string.Empty;
            string url = asset.TryGetProperty("browser_download_url", out JsonElement urlElement)
                ? urlElement.GetString() ?? string.Empty
                : string.Empty;

            if (string.Equals(name, exactAsset, StringComparison.OrdinalIgnoreCase))
            {
                downloadUrl = url;
                break;
            }

            if (downloadUrl is null &&
                name.StartsWith(ExpectedAssetPrefix, StringComparison.OrdinalIgnoreCase) &&
                name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                downloadUrl = url;
            }
        }

        if (string.IsNullOrWhiteSpace(downloadUrl))
            throw new InvalidOperationException($"No se encontró {exactAsset} en la publicación v{remoteVersion}.");

        return new SogmaUpdateInfo
        {
            Version = remoteVersion,
            DownloadUrl = downloadUrl,
            Notes = notes
        };
    }

    public static async Task DownloadAndInstallAsync(
        SogmaUpdateInfo update,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string updateRoot = Path.Combine(Path.GetTempPath(), "SOGMA_Update_" + Guid.NewGuid().ToString("N"));
        string zipPath = Path.Combine(updateRoot, "update.zip");
        string extractPath = Path.Combine(updateRoot, "payload");
        Directory.CreateDirectory(updateRoot);
        Directory.CreateDirectory(extractPath);

        try
        {
            using HttpResponseMessage response = await Http.GetAsync(
                update.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            long? length = response.Content.Headers.ContentLength;
            await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using FileStream output = new(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);

            byte[] buffer = new byte[128 * 1024];
            long total = 0;
            while (true)
            {
                int read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read <= 0) break;
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                total += read;
                if (length is > 0)
                    progress?.Report(Math.Clamp(total / (double)length.Value, 0, 1));
            }

            progress?.Report(1);
            ZipFile.ExtractToDirectory(zipPath, extractPath, overwriteFiles: true);

            string payloadPath = ResolvePayloadRoot(extractPath);
            string expectedExe = Path.Combine(payloadPath, "SOGMA Panel Manager.exe");
            if (!File.Exists(expectedExe))
                throw new InvalidDataException("La actualización no contiene 'SOGMA Panel Manager.exe'.");

            LaunchExternalUpdater(payloadPath, updateRoot);
        }
        catch
        {
            TryDeleteDirectory(updateRoot);
            throw;
        }
    }

    private static string ResolvePayloadRoot(string extractPath)
    {
        if (File.Exists(Path.Combine(extractPath, "SOGMA Panel Manager.exe")))
            return extractPath;

        string[] dirs = Directory.GetDirectories(extractPath);
        string[] files = Directory.GetFiles(extractPath);
        if (dirs.Length == 1 && files.Length == 0 && File.Exists(Path.Combine(dirs[0], "SOGMA Panel Manager.exe")))
            return dirs[0];

        return extractPath;
    }

    private static void LaunchExternalUpdater(string payloadPath, string updateRoot)
    {
        string installPath = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string exePath = Path.Combine(installPath, "SOGMA Panel Manager.exe");
        int pid = Environment.ProcessId;
        string scriptPath = Path.Combine(Path.GetTempPath(), "SOGMA_Updater_" + Guid.NewGuid().ToString("N") + ".cmd");

        string script = $"""\n@echo off
setlocal
set "PID={pid}"
set "SOURCE={payloadPath}"
set "TARGET={installPath}"
set "APP={exePath}"
set "TEMPROOT={updateRoot}"

:wait_for_app
for /f "tokens=2" %%P in ('tasklist /FI "PID eq %PID%" /NH 2^>nul') do (
    if "%%P"=="%PID%" (
        timeout /t 1 /nobreak >nul
        goto wait_for_app
    )
)

robocopy "%SOURCE%" "%TARGET%" /E /R:5 /W:1 /NFL /NDL /NJH /NJS /NP >nul
set "RC=%ERRORLEVEL%"
if %RC% GEQ 8 (
    start "" cmd /c "echo No se pudo instalar la actualizacion de SOGMA. Codigo robocopy: %RC% & pause"
    exit /b %RC%
)

start "" "%APP%"
timeout /t 2 /nobreak >nul
rmdir /s /q "%TEMPROOT%" 2>nul
(goto) 2>nul & del "%~f0"
""";

        File.WriteAllText(scriptPath, script);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c start \"\" \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = installPath
        });
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch { }
    }
}
