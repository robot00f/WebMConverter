using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace WebMConverter
{
    class FFplay : Process
    {
        public string FFplayPath;

        StringBuilder errorLog;
        public string ErrorLog => errorLog.ToString().Trim();

        public FFplay(string argument)
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string candidate = Path.Combine(basePath, "Binaries", "Win32", "ffplay.exe");
            if (!File.Exists(candidate))
            {
                candidate = Path.Combine(Environment.CurrentDirectory, "Binaries", "Win32", "ffplay.exe");
            }
            FFplayPath = Path.GetFullPath(candidate);

            StartInfo.FileName = FFplayPath;
            StartInfo.WorkingDirectory = Path.GetDirectoryName(FFplayPath);

            string sanitizedArgument = (argument ?? string.Empty).Replace("\r", "").Replace("\n", "").Replace("\0", "");
            StartInfo.Arguments = sanitizedArgument;

            StartInfo.RedirectStandardInput = true;
            StartInfo.RedirectStandardOutput = true;
            StartInfo.RedirectStandardError = true;
            StartInfo.UseShellExecute = false; // Required to redirect IO streams and prevent shell command execution
            StartInfo.CreateNoWindow = true; // Hide console
            EnableRaisingEvents = true;

            errorLog = new StringBuilder();
            ErrorDataReceived += (sender, args) => errorLog.AppendLine(args.Data);

#if DEBUG
            OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
            ErrorDataReceived += (sender, args) => Console.WriteLine(args.Data);
#endif
        }

        new public void Start()
        {
            base.Start();
            BeginErrorReadLine();
            BeginOutputReadLine();
        }
    }
}
