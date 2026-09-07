using System;
using System.Diagnostics;
using System.IO;

namespace WebMConverter
{
    static class VideoDownload
    {
        public static bool Enabled { get; private set; }

        public static string GetResolvedYtDlpPath()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string candidate = Path.Combine(basePath, "Binaries", "Win64", Program.yt_dl);
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);

            candidate = Path.Combine(Environment.CurrentDirectory, "Binaries", "Win64", Program.yt_dl);
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);

            return Path.GetFullPath(Path.Combine(basePath, "Binaries", "Win64", Program.yt_dl));
        }

        public static void CheckEnabled()
        {
            string exePath = GetResolvedYtDlpPath();
            Enabled = File.Exists(exePath);
        }
    }

    class YoutubeDL : Process
    {
        public YoutubeDL(string arguments)
        {
            string exePath = VideoDownload.GetResolvedYtDlpPath();
            StartInfo.FileName = exePath;
            StartInfo.WorkingDirectory = Path.GetDirectoryName(exePath);

            string sanitizedArguments = (arguments ?? string.Empty).Replace("\r", "").Replace("\n", "").Replace("\0", "");
            StartInfo.Arguments = sanitizedArguments;

            StartInfo.RedirectStandardInput = true;
            StartInfo.RedirectStandardOutput = true;
            StartInfo.RedirectStandardError = true;
            StartInfo.UseShellExecute = false; // Required to redirect IO streams and prevent shell command execution
            StartInfo.CreateNoWindow = true; // Hide console
            EnableRaisingEvents = true;
        }

        new public void Start()
        {
            Start(true);
        }

        public void Start(bool OutputReadLine)
        {
            base.Start();
            BeginErrorReadLine();
            if (OutputReadLine)
                BeginOutputReadLine();
        }
    }
}
