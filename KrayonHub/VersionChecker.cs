using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

public class VersionChecker
{
    private readonly string _url;
    private readonly string _installedVersionFile;
    private static readonly HttpClient _client = new HttpClient();

    // Guarda el archivo junto al ejecutable
    private static string DefaultInstalledFile =>
        Path.Combine(AppContext.BaseDirectory, "installed_version.txt");

    public VersionChecker(string url, string installedVersionFile = null)
    {
        _url = url;
        _installedVersionFile = installedVersionFile ?? DefaultInstalledFile;
    }

    // Lee la versión actualmente instalada del archivo (si no existe = 0.0.0)
    public string GetInstalledVersion()
    {
        if (!File.Exists(_installedVersionFile))
            return "0.0.0";

        string v = File.ReadAllText(_installedVersionFile).Trim();
        return string.IsNullOrWhiteSpace(v) ? "0.0.0" : v;
    }

    // Guarda la versión instalada — llamar después de una descarga exitosa
    public void SaveInstalledVersion(string version)
    {
        File.WriteAllText(_installedVersionFile, version.Trim());
    }

    public async Task<CheckResult> CheckAsync()
    {
        string localVersion = GetInstalledVersion();

        try
        {
            string response = await _client.GetStringAsync(_url);
            var data = JsonSerializer.Deserialize<VersionData>(response);

            if (data == null || string.IsNullOrWhiteSpace(data.version))
                return CheckResult.Fail("Invalid server response");

            bool updateAvailable = new Version(data.version) > new Version(localVersion);

            return new CheckResult
            {
                Success = true,
                LocalVersion = localVersion,
                RemoteVersion = data.version,
                DownloadUrl = data.url,
                UpdateAvailable = updateAvailable
            };
        }
        catch (Exception ex)
        {
            return CheckResult.Fail(ex.Message);
        }
    }

    public async Task DownloadAndInstallAsync(
        string downloadUrl,
        string remoteVersion,
        string engineFolderPath,
        IProgress<(double percent, string status)> progress)
    {
        string tempZip = Path.Combine(Path.GetTempPath(), "KrayonEngine_update.zip");

        try
        {
            progress?.Report((0, "Connecting..."));

            using var response = await _client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1;
            long downloaded = 0;

            using (var fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var stream = await response.Content.ReadAsStreamAsync())
            {
                byte[] buffer = new byte[81920];
                int read;

                while ((read = await stream.ReadAsync(buffer)) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, read));
                    downloaded += read;

                    double percent = totalBytes > 0
                        ? (double)downloaded / totalBytes * 90
                        : 45;

                    string mb = $"{downloaded / 1_048_576.0:F1} MB";
                    string total = totalBytes > 0 ? $" / {totalBytes / 1_048_576.0:F1} MB" : "";
                    progress?.Report((percent, $"Downloading... {mb}{total}"));
                }
            }

            progress?.Report((90, "Extracting files..."));

            if (Directory.Exists(engineFolderPath))
                Directory.Delete(engineFolderPath, true);

            Directory.CreateDirectory(engineFolderPath);
            ZipFile.ExtractToDirectory(tempZip, engineFolderPath, overwriteFiles: true);

            // ✅ Guarda la versión recién instalada para no volver a preguntar
            SaveInstalledVersion(remoteVersion);

            progress?.Report((100, "Update complete!"));
        }
        finally
        {
            if (File.Exists(tempZip))
                File.Delete(tempZip);
        }
    }

    private class VersionData
    {
        public string version { get; set; } = "";
        public string url { get; set; } = "";
    }
}

public class CheckResult
{
    public bool Success { get; set; }
    public bool UpdateAvailable { get; set; }
    public string LocalVersion { get; set; } = "";
    public string RemoteVersion { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string ErrorMessage { get; set; } = "";

    public static CheckResult Fail(string msg) => new() { Success = false, ErrorMessage = msg };
}