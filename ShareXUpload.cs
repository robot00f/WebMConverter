using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;

namespace WebMConverter
{
    static class ShareXUpload
    {
        public static bool Enabled { get; private set; }
        public static string InstallPath { get; private set; }

        public static void CheckEnabled()
        {
            var installPath = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Classes\*\shell\ShareX\command", null, null);
            if (installPath == null) return;

            var start = installPath.IndexOf('"') + 1;
            var end = installPath.IndexOf('"', start);
            if (start > 0 && end > start)
            {
                string rawPath = installPath.Substring(start, end - start);
                if (File.Exists(rawPath))
                {
                    InstallPath = Path.GetFullPath(rawPath);
                    Enabled = true;
                }
            }
        }
    }

    class ShareX : Process
    {
        public ShareX(string filename)
        {
            if (!ShareXUpload.Enabled || string.IsNullOrWhiteSpace(ShareXUpload.InstallPath)) return;

            string exePath = Path.GetFullPath(ShareXUpload.InstallPath);
            if (!File.Exists(exePath)) return;

            StartInfo.FileName = exePath;
            StartInfo.WorkingDirectory = Path.GetDirectoryName(exePath);
            StartInfo.UseShellExecute = false;
            StartInfo.CreateNoWindow = true;

            string sanitizedFile = (filename ?? string.Empty).Replace("\r", "").Replace("\n", "").Replace("\0", "").Replace("\"", "\\\"");
            StartInfo.Arguments = $@"""{sanitizedFile}""";
        }
    }
}
