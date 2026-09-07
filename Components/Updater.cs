using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace WebMConverter.Components
{
    public class Updater : Process
    {
        public readonly string UpdaterPath;

        public Updater()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string candidate = Path.Combine(basePath, "WebMConverter.Updater.exe");
            if (!File.Exists(candidate))
            {
                candidate = Path.Combine(Environment.CurrentDirectory, "WebMConverter.Updater.exe");
            }
            UpdaterPath = Path.GetFullPath(candidate);

            string updateCandidate = Path.Combine(basePath, "WebMConverter.Updater.update.exe");
            if (!File.Exists(updateCandidate))
            {
                updateCandidate = Path.Combine(Environment.CurrentDirectory, "WebMConverter.Updater.update.exe");
            }
            string UpdatedUpdaterPath = Path.GetFullPath(updateCandidate);

            if (File.Exists(UpdatedUpdaterPath))
            {
                if (File.Exists(UpdaterPath))
                    File.Delete(UpdaterPath);

                File.Move(UpdatedUpdaterPath, UpdaterPath);
            }

            StartInfo.FileName = UpdaterPath;
            StartInfo.WorkingDirectory = Path.GetDirectoryName(UpdaterPath);
            StartInfo.RedirectStandardInput = true;
            StartInfo.RedirectStandardOutput = true;
            StartInfo.RedirectStandardError = true;
            StartInfo.UseShellExecute = false; // Required to redirect IO streams and prevent shell command execution
            StartInfo.CreateNoWindow = true; // Hide console
            EnableRaisingEvents = true; 
        }

        new public void Start()
        {
            base.Start();
            BeginErrorReadLine();
            BeginOutputReadLine();
        }

        // success, new version available, version string OR error, changelog
        public Tuple<bool, bool, string, string> Check(string currentVersion)
        {
            if (!File.Exists(UpdaterPath))
                return new Tuple<bool, bool, string, string>(false, false, "Updater has been removed.", null);

            int lastDot = currentVersion.LastIndexOf('.');
            if (lastDot > 0)
            {
                currentVersion = currentVersion.Substring(0, lastDot);
            }

            // Sanitize version argument: only allow alphanumeric, dot, hyphen, underscore, plus
            var sanitizedVersion = new StringBuilder();
            foreach (char c in (currentVersion ?? string.Empty))
            {
                if (char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' || c == '+')
                {
                    sanitizedVersion.Append(c);
                }
            }

            StartInfo.Arguments = "check " + sanitizedVersion.ToString();

            var output = new StringBuilder();
            OutputDataReceived += (sender, args) => output.AppendLine(args.Data);

            Start();
            WaitForExit();

            var outputString = output.ToString().Trim();

            if (ExitCode != 0)
                return new Tuple<bool, bool, string, string>(false, false, output.ToString(), null);

            if (outputString.Length == 0)
                return new Tuple<bool, bool, string, string>(true, false, null, null);

            var versionAndChangelog = outputString.Split(new[] { '\n' }, 2);

            string newVersion = versionAndChangelog[0].Trim();
            string changelog = versionAndChangelog.Length > 1 ? versionAndChangelog[1].Trim() : newVersion;

            return new Tuple<bool, bool, string, string>(true, true, newVersion, changelog);
        }
    }
}
