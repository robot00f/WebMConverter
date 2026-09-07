using System;
using System.Diagnostics;
using System.IO;

namespace WebMConverter.Components
{
    class MkvExtract : Process
    {
        private readonly string _programPath;

        public MkvExtract(string argument)
        {
            if (string.IsNullOrEmpty(argument))
                throw new ArgumentNullException(nameof(argument));

            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string candidate = Path.Combine(basePath, "Binaries", "Win32", "mkvextract.exe");
            if (!File.Exists(candidate))
            {
                candidate = Path.Combine(Environment.CurrentDirectory, "Binaries", "Win32", "mkvextract.exe");
            }
            _programPath = Path.GetFullPath(candidate);

            StartInfo.FileName = _programPath;
            StartInfo.WorkingDirectory = Path.GetDirectoryName(_programPath);

            string sanitizedArgument = argument.Replace("\r", "").Replace("\n", "").Replace("\0", "");
            StartInfo.Arguments = sanitizedArgument;

            StartInfo.RedirectStandardInput = true;
            StartInfo.RedirectStandardOutput = true;
            StartInfo.RedirectStandardError = true;
            StartInfo.UseShellExecute = false; // Required to redirect IO streams and prevent shell command execution
            StartInfo.CreateNoWindow = true; // Hide console
            EnableRaisingEvents = true;

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
