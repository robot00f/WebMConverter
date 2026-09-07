using System;
using System.Diagnostics;
using System.IO;

namespace WebMConverter
{
    class FFmpeg : Process
    {
        public string FFmpegPath;

        public FFmpeg(string argument, bool win32 = false)
        {
            string folder = (win32 || !Environment.Is64BitOperatingSystem) ? "Win32" : "Win64";

            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string candidate = Path.Combine(basePath, "Binaries", folder, "ffmpeg.exe");
            if (!File.Exists(candidate))
            {
                candidate = Path.Combine(Environment.CurrentDirectory, "Binaries", folder, "ffmpeg.exe");
            }
            FFmpegPath = Path.GetFullPath(candidate);

            StartInfo.FileName = FFmpegPath;
            StartInfo.WorkingDirectory = Path.GetDirectoryName(FFmpegPath);

            string sanitizedArgument = (argument ?? string.Empty).Replace("\r", "").Replace("\n", "").Replace("\0", "");
            StartInfo.Arguments = "-hide_banner -nostdin " + sanitizedArgument;

            StartInfo.RedirectStandardInput = true;
            StartInfo.RedirectStandardOutput = true;
            StartInfo.RedirectStandardError = true;
            StartInfo.UseShellExecute = false; // Required to redirect IO streams and prevent shell command execution
            StartInfo.CreateNoWindow = true; // Hide console
            EnableRaisingEvents = true; 
        }

        new public void Start() => Start(true);
        public void Start(bool outputReadLine)
        {
            base.Start();
            BeginErrorReadLine();
            if (outputReadLine)
                BeginOutputReadLine();
        }
    }
}
