using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;

namespace WebMConverter
{
    class FFprobe : Process
    {
        public string FFmpegPath;
        const string templateArguments = "{0} \"{1}\" -of xml{2}";
        // {0} is the format of the input file
        // {1} is the input file
        // {2} is automatically constructed list of things to output OR argument

        public FFprobe(string inputFile, string format = "-f avisynth", List<string> dataToProbe = null, string argument = null)
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string candidate = Path.Combine(basePath, "Binaries", "Win32", "ffprobe.exe");
            if (!File.Exists(candidate))
            {
                candidate = Path.Combine(Environment.CurrentDirectory, "Binaries", "Win32", "ffprobe.exe");
            }
            FFmpegPath = Path.GetFullPath(candidate);

            inputFile = Utility.ExtendedLenPath(inputFile);
            string sanitizedInputFile = SanitizeArgument(inputFile).Replace("\"", "\\\"");
            string sanitizedFormat = SanitizeArgument(format);

            if (argument == null) // No override arguments, time to construct this bad boy
            {
                if (dataToProbe == null)
                {
                    dataToProbe = new List<string>();
                    dataToProbe.Add("streams");
                }

                StringBuilder dataToProbeAsString = new StringBuilder();
                foreach (string Type in dataToProbe)
                {
                    string sanitizedType = SanitizeArgument(Type).Replace("\"", "");
                    dataToProbeAsString.Append(" -show_");
                    dataToProbeAsString.Append(sanitizedType);
                }
                StartInfo.Arguments = string.Format(templateArguments, sanitizedFormat, sanitizedInputFile, dataToProbeAsString);
            }
            else
            {
                string sanitizedOverride = SanitizeArgument(argument);
                StartInfo.Arguments = string.Format(templateArguments, sanitizedFormat, sanitizedInputFile, " " + sanitizedOverride);
            }

            StartInfo.FileName = FFmpegPath;
            StartInfo.WorkingDirectory = Path.GetDirectoryName(FFmpegPath);
            StartInfo.RedirectStandardInput = true;
            StartInfo.RedirectStandardOutput = true;
            StartInfo.RedirectStandardError = true;
            StartInfo.UseShellExecute = false; // Required to redirect IO streams and prevent shell command execution
            StartInfo.CreateNoWindow = true; // Hide console
            EnableRaisingEvents = true;
        }

        private static string SanitizeArgument(string arg)
        {
            if (string.IsNullOrEmpty(arg))
                return string.Empty;
            return arg.Replace("\r", "").Replace("\n", "").Replace("\0", "");
        }

        new public void Start()
        {
            base.Start();
            BeginErrorReadLine();
            BeginOutputReadLine();
        }

        public string Probe()
        {
            var output = new StringBuilder();
            OutputDataReceived += (sender, args) => output.AppendLine(args.Data);

            Start();
            WaitForExit();

            return output.ToString();
        }
    }
}
