using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WebMConverter.Dialogs
{
    public class UpdateResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Version { get; set; }
    }

    public partial class UpdateBinaries : Form
    {
        private static readonly string DirectDownloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
        private string _installedVersion;

        public UpdateBinaries() : this(null)
        {
        }

        public UpdateBinaries(string installedVersion)
        {
            _installedVersion = installedVersion ?? GetInstalledVersion();
            InitializeComponent();
        }

        public static string GetYtDlpPath()
        {
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Binaries", "Win64", Program.yt_dl);
            if (File.Exists(localPath))
                return localPath;

            string cwdPath = Path.Combine(Environment.CurrentDirectory, "Binaries", "Win64", Program.yt_dl);
            if (File.Exists(cwdPath))
                return cwdPath;

            return localPath;
        }

        public static string GetInstalledVersion()
        {
            try
            {
                string exePath = GetYtDlpPath();
                if (!File.Exists(exePath))
                    return null;

                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit(4000);
                    return string.IsNullOrEmpty(output) ? null : output;
                }
            }
            catch
            {
                return null;
            }
        }

        public static void SaveInstalledVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return;

            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                if (config.AppSettings.Settings["YTDLV"] == null)
                    config.AppSettings.Settings.Add("YTDLV", version);
                else
                    config.AppSettings.Settings["YTDLV"].Value = version;

                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
            }
            catch
            {
                // Ignore configuration save issues
            }
        }

        public static async Task<UpdateResult> RunSelfUpdateAsync(Action<string> logCallback = null)
        {
            string exePath = GetYtDlpPath();
            bool exists = File.Exists(exePath);

            if (exists)
            {
                logCallback?.Invoke("Checking for updates via yt-dlp.exe -U...");
                try
                {
                    var outputLines = new List<string>();
                    var psi = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = "-U",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using (var process = new Process { StartInfo = psi, EnableRaisingEvents = true })
                    {
                        var tcs = new TaskCompletionSource<int>();
                        process.OutputDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrWhiteSpace(e.Data))
                            {
                                outputLines.Add(e.Data);
                                logCallback?.Invoke(e.Data);
                            }
                        };
                        process.ErrorDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrWhiteSpace(e.Data))
                            {
                                outputLines.Add(e.Data);
                                logCallback?.Invoke(e.Data);
                            }
                        };
                        process.Exited += (s, e) => tcs.TrySetResult(process.ExitCode);

                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        int exitCode = await tcs.Task;

                        if (exitCode == 0)
                        {
                            string currentVer = GetInstalledVersion();
                            SaveInstalledVersion(currentVer);

                            string summary = outputLines.Count > 0 ? outputLines[outputLines.Count - 1] : "yt-dlp update completed.";
                            return new UpdateResult
                            {
                                Success = true,
                                Message = summary,
                                Version = currentVer
                            };
                        }
                        else
                        {
                            logCallback?.Invoke($"yt-dlp -U exited with code {exitCode}. Trying fallback direct download...");
                        }
                    }
                }
                catch (Exception ex)
                {
                    logCallback?.Invoke($"yt-dlp self-update failed: {ex.Message}. Trying fallback direct download...");
                }
            }

            // Fallback: download directly from official GitHub releases
            return await DownloadDirectAsync(logCallback);
        }

        public static async Task<UpdateResult> DownloadDirectAsync(Action<string> logCallback = null)
        {
            logCallback?.Invoke("Downloading latest yt-dlp binary from GitHub release...");
            string targetPath = GetYtDlpPath();
            string tempFile = Path.Combine(Path.GetTempPath(), Program.yt_dl + "." + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "WebMConverter-Updater");
                    using (var response = await httpClient.GetAsync(DirectDownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await contentStream.CopyToAsync(fileStream);
                        }
                    }
                }

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                if (File.Exists(targetPath))
                {
                    string backup = targetPath + ".old";
                    if (File.Exists(backup))
                    {
                        try { File.Delete(backup); } catch { }
                    }
                    try { File.Move(targetPath, backup); } catch { }
                }

                File.Move(tempFile, targetPath);

                string currentVer = GetInstalledVersion();
                SaveInstalledVersion(currentVer);
                logCallback?.Invoke($"yt-dlp downloaded and updated successfully (version {currentVer})!");

                return new UpdateResult
                {
                    Success = true,
                    Message = $"yt-dlp updated successfully to version {currentVer}.",
                    Version = currentVer
                };
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"Direct download failed: {ex.Message}");
                return new UpdateResult
                {
                    Success = false,
                    Message = ex.Message
                };
            }
            finally
            {
                try
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
                catch { }
            }
        }

        private async void UpdateBinaries_Shown(object sender, EventArgs e)
        {
            buttonClose.Enabled = false;
            progressBar.Style = ProgressBarStyle.Marquee;
            labelStatus.Text = string.IsNullOrEmpty(_installedVersion)
                ? "Checking for yt-dlp updates..."
                : $"Current version: {_installedVersion}. Checking updates...";

            var result = await RunSelfUpdateAsync((line) =>
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    this.InvokeIfRequired(() =>
                    {
                        labelStatus.Text = line;
                    });
                }
            });

            this.InvokeIfRequired(() =>
            {
                progressBar.Style = ProgressBarStyle.Blocks;
                progressBar.Value = 100;
                buttonClose.Enabled = true;
                buttonClose.Focus();

                if (result.Success)
                {
                    labelStatus.Text = string.IsNullOrEmpty(result.Message) ? "yt-dlp is up to date." : result.Message;
                }
                else
                {
                    labelStatus.Text = "Update failed: " + result.Message;
                }
            });
        }

        private void buttonClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        // Maintained for backward compatibility
        public async void GetLatestVersion()
        {
            await RunSelfUpdateAsync();
        }
    }
}
