using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.WindowsAPICodePack.Taskbar;
using Timer = System.Windows.Forms.Timer;
using static WebMConverter.Utility;
using System.Linq;

namespace WebMConverter.Dialogs
{
    public partial class DownloadOptionsDialog : Form
    {
        public string Outfile { get; set; }
        public string OutputPath { get; set; }

        private readonly string _infile;
        private readonly string _options;
        private YoutubeDL _downloaderProcess;

        private Timer _timer;
        private bool _ended;
        private bool _panic;

        private TaskbarManager taskbarManager;

        public DownloadOptionsDialog(string url, string outputPath)
        {
            InitializeComponent();
            pictureStatus.BackgroundImage = StatusImages.Images["Happening"];

            string actualUrl = url;
            if (url.Contains('@'))
            {
                var parts = url.Split(new[] { '@' }, 2);
                actualUrl = parts[0];
                _options = String.IsNullOrEmpty(parts[1]) ? String.Empty : $" --download-sections \"{parts[1]}\"";
            }
            else
            {
                _options = String.Empty;
            }

            _infile = '"' + actualUrl.Replace(@"""", @"\""") + '"';
            OutputPath = outputPath;

            taskbarManager = TaskbarManager.Instance;
            buttonLoad.Enabled = false;
        }

        private void ProcessOnErrorDataReceived(object sender, DataReceivedEventArgs args)
        {
            if (args.Data != null)
                boxOutput.Invoke((Action)(() => boxOutput.AppendText(Environment.NewLine + args.Data)));
        }

        private void ProcessOnOutputDataReceived(object sender, DataReceivedEventArgs args)
        {
            if (args.Data != null)
            {
                boxOutput.Invoke((Action)(() => boxOutput.AppendText(Environment.NewLine + args.Data)));

                if (DataContainsProgress(args.Data))
                {
                    ParseAndUpdateProgress(args.Data);
                    if(String.IsNullOrEmpty(Outfile))
                        GetId(args.Data);
                }

            }
        }

        private void GetId(string data)
        {
            if(data.Contains("Destination:"))
                Outfile = data.Split('[').LastOrDefault().Split(']')[0];
        }

        // example youtube-dl line:
        // [download]  51.5% of ~3.85MiB at 20.49MiB/s ETA 00:00

        private bool DataContainsFFMPEG(string data) => data.StartsWith("[ffmpeg]");
        private bool DataContainsProgress(string data) => data.StartsWith("[download]");

        private void ParseAndUpdateProgress(string input)
        {
            var r = new Regex(@"([0-9.]+)%");
            var m = r.Match(input);
            if (m.Success)
            {
                var progress = float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
                progressBar.InvokeIfRequired(() =>
                {
                    progressBar.Value = (int)(progress * 10); // progressBar maximum is 1000
                });
                taskbarManager.SetProgressValue((int)(progress * 10), 1000);
            }
        }

        private void DownloadDialog_Load(object sender, EventArgs e)
        {
            boxOutput.AppendText($"{Environment.NewLine}Starting Process");
            _downloaderProcess = new YoutubeDL(null);
            
            if (_infile.IndexOf("youtu", StringComparison.OrdinalIgnoreCase) >= 0)
                _downloaderProcess.StartInfo.Arguments = $@"--list-formats --extractor-args ""youtube:player_client=android,web"" --compat-options no-youtube-unavailable-videos {_infile}";
            else
                _downloaderProcess.StartInfo.Arguments = $@"--list-formats {_infile}";

            _downloaderProcess.ErrorDataReceived += ProcessOnErrorDataReceived;
            _downloaderProcess.OutputDataReceived += ProcessOnOutputDataReceived;
            _downloaderProcess.Exited += (o, args) => boxOutput.Invoke((Action)(() =>
            {
                boxOutput.AppendText($"{Environment.NewLine}--- YT-DLP HAS EXITED ---");
                buttonCancel.Enabled = false;

                _timer = new Timer {Interval = 500};
                _timer.Tick += Exited;
                _timer.Start();
            }));

            taskbarManager.SetProgressState(TaskbarProgressBarState.Normal);
            progressBar.Style = ProgressBarStyle.Blocks;
            _downloaderProcess.Start();
        }

        private void Exited(object sender, EventArgs eventArgs)
        {
            _timer.Stop();

            if (_downloaderProcess.ExitCode != 0)
            {
                string outputText = boxOutput.Text ?? string.Empty;
                bool isForbidden = outputText.IndexOf("403", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   outputText.IndexOf("Forbidden", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isBotOrSign = outputText.IndexOf("bot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   outputText.IndexOf("Sign in", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isExtractor = outputText.IndexOf("extractor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   outputText.IndexOf("Unable to extract", StringComparison.OrdinalIgnoreCase) >= 0;

                boxOutput.AppendText($"{Environment.NewLine}{Environment.NewLine}{Program.yt_dl} exited with exit code {_downloaderProcess.ExitCode}. That's usually bad.");
                boxOutput.AppendText($"{Environment.NewLine}--------------------------------------------------------------------------------");
                boxOutput.AppendText($"{Environment.NewLine}[ERROR DIAGNOSIS]");
                if (isForbidden)
                    boxOutput.AppendText($"{Environment.NewLine}• HTTP 403 Forbidden detected: YouTube has rejected request signatures.");
                else if (isBotOrSign)
                    boxOutput.AppendText($"{Environment.NewLine}• Anti-bot verification or sign-in requirement detected from YouTube.");
                else if (isExtractor)
                    boxOutput.AppendText($"{Environment.NewLine}• Stream extractor failure: YouTube player JavaScript format has changed.");
                else
                    boxOutput.AppendText($"{Environment.NewLine}• Download failed with exit code {_downloaderProcess.ExitCode}.");

                boxOutput.AppendText($"{Environment.NewLine}{Environment.NewLine}Actionable Steps to Resolve:");
                boxOutput.AppendText($"{Environment.NewLine}1. Update yt-dlp to the latest release (yt-dlp.exe -U) from Settings / General tab or via prompt.");
                boxOutput.AppendText($"{Environment.NewLine}2. If YouTube requires account verification, supply cookies or check the video in a browser.");
                boxOutput.AppendText($"{Environment.NewLine}3. Once updated, close this window and retry downloading the video.");
                boxOutput.AppendText($"{Environment.NewLine}--------------------------------------------------------------------------------{Environment.NewLine}");

                pictureStatus.BackgroundImage = StatusImages.Images["Failure"];
                buttonCancel.Enabled = true;
                taskbarManager.SetProgressState(TaskbarProgressBarState.Error);

                if (isForbidden || _downloaderProcess.ExitCode == 1)
                {
                    var promptResult = MessageBox.Show(this,
                        $"yt-dlp failed (Exit code {_downloaderProcess.ExitCode}" + (isForbidden ? " - HTTP 403 Forbidden" : "") + ").\n\n" +
                        "YouTube frequently changes its player API and anti-bot verification.\n\n" +
                        "Would you like to run 'yt-dlp -U' now to update yt-dlp to the latest release?",
                        "YouTube API Error - Update yt-dlp?",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (promptResult == DialogResult.Yes)
                    {
                        using (var updater = new UpdateBinaries())
                        {
                            updater.ShowDialog(this);
                        }
                    }
                }
            }
            else
            {
                if(!buttonLoad.Enabled)
                    boxOutput.AppendText($"{Environment.NewLine}{Environment.NewLine}Process finished!");
                else
                {
                    boxOutput.AppendText($"{Environment.NewLine}{Environment.NewLine}Video downloaded succesfully!");
                    buttonLoad.Text = "Load";
                }
                    
                pictureStatus.BackgroundImage = StatusImages.Images["Success"];
                buttonLoad.Enabled = true;
                buttonCancel.Enabled = true;
                buttonCancel.Text = "Close";
                MoveNewFile();
                this.Activate();
            }

            _ended = true;
        }

        private void MoveNewFile()
        {
            if (String.IsNullOrEmpty(Outfile))
                return;

            string[] fileEntries = Directory.GetFiles(".");
            foreach (string fileName in fileEntries) 
            {
                if (fileName.Contains(Outfile))
                {
                    Outfile = fileName;
                    break;
                }
            }

            string targetDir = string.IsNullOrWhiteSpace(OutputPath) ? Environment.CurrentDirectory : OutputPath;
            string fileNameOnly = Path.GetFileName(Outfile);
            string fullSource = Path.GetFullPath(Outfile);
            string finalFile = Path.Combine(targetDir, fileNameOnly);

            if (!fullSource.Equals(Path.GetFullPath(finalFile), StringComparison.OrdinalIgnoreCase))
            {
                while (File.Exists(finalFile))
                {
                    finalFile = Utility.IncreaseFileNumber(finalFile);
                }
                File.Move(fullSource, finalFile);
            }
            Outfile = Path.GetFileName(finalFile);
            OutputPath = targetDir;
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            if (buttonCancel.Text.Equals("Close"))
            {
                if(!_downloaderProcess.HasExited)
                    KillProcessAndChildren(_downloaderProcess.Id);

                Dispose();
                return;
            }

            if (!_ended || _panic) //Prevent stack overflow
            {
                if (!_downloaderProcess.HasExited)
                    KillProcessAndChildren(_downloaderProcess.Id);
            }
            else
                Close();
        }

        private void ConverterForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _panic = true; //Shut down while avoiding exceptions
            buttonCancel_Click(sender, e);
            taskbarManager.SetProgressState(TaskbarProgressBarState.NoProgress);
        }

        private void ConverterForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _downloaderProcess.Dispose();
        }

        private void buttonLoad_Click(object sender, EventArgs e)
        {
            if (buttonLoad.Text.Equals("Download") && String.IsNullOrEmpty(txtFormatNumber.Text))
            {
                boxOutput.AppendText($"{Environment.NewLine}{Environment.NewLine}Please select a format first before continue.");
            }
            else if (buttonLoad.Text.Equals("Download") && !String.IsNullOrEmpty(txtFormatNumber.Text))
            {
                boxOutput.AppendText($"{Environment.NewLine}Starting Process");
                _downloaderProcess = new YoutubeDL(null);

                string format = txtFormatNumber.Text.Trim();
                if (!format.StartsWith("\"") && format.Contains(" "))
                {
                    format = $"\"{format}\"";
                }

                if (_infile.IndexOf("youtu", StringComparison.OrdinalIgnoreCase) >= 0)
                    _downloaderProcess.StartInfo.Arguments = $@"-f {format} --no-mtime --extractor-args ""youtube:player_client=android,web"" --compat-options no-youtube-unavailable-videos {_infile}{_options}".Trim();
                else
                    _downloaderProcess.StartInfo.Arguments = $@"-f {format} --no-mtime {_infile}{_options}".Trim();

                _downloaderProcess.ErrorDataReceived += ProcessOnErrorDataReceived;
                _downloaderProcess.OutputDataReceived += ProcessOnOutputDataReceived;
                _downloaderProcess.Exited += (o, args) => boxOutput.Invoke((Action)(() =>
                {
                    boxOutput.AppendText($"{Environment.NewLine}--- YT-DLP HAS EXITED ---");
                    buttonCancel.Enabled = false;

                    _timer = new Timer { Interval = 500 };
                    _timer.Tick += Exited;
                    _timer.Start();
                }));

                taskbarManager.SetProgressState(TaskbarProgressBarState.Normal);
                progressBar.Style = ProgressBarStyle.Blocks;
                _downloaderProcess.Start();
            }
            else
            {
                DialogResult = DialogResult.OK;
                Close();
            }

        }

        private void boxOutput_TextChanged(object sender, EventArgs e) => NativeMethods.SendMessage(boxOutput.Handle, 0x115, 7, 0);

        internal string GetOutfile()
        {
            if (String.IsNullOrEmpty(Outfile))
                return String.Empty;

            string targetDir = string.IsNullOrWhiteSpace(OutputPath) ? Environment.CurrentDirectory : OutputPath;
            return Path.Combine(targetDir, Outfile);
        }
    }
}
