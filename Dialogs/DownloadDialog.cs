using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.WindowsAPICodePack.Taskbar;
using Timer = System.Windows.Forms.Timer;
using static WebMConverter.Utility;
using System.Linq;

namespace WebMConverter.Dialogs
{
    public partial class DownloadDialog : Form
    {
        public string Outfile { get; set; }
        public string OutputPath { get; set; }

        private readonly string _infile;
        private readonly string _options;
        private YoutubeDL _downloaderProcess;

        private Timer _timer;
        private bool _ended;
        private bool _panic;
        private bool _isUpdating;

        private TaskbarManager taskbarManager;

        public DownloadDialog(string url, string options, string outputPath)
        {
            InitializeComponent();
            pictureStatus.BackgroundImage = StatusImages.Images["Happening"];

            if (url.Contains('@') && String.IsNullOrEmpty(options))
            {
                var parts = url.Split(new[] { '@' }, 2);
                url = parts[0];
                options = parts[1];
            }

            string sanitizedUrl = SanitizeInput(url);
            string sanitizedOptions = SanitizeInput(options);

            _infile = '"' + sanitizedUrl.Replace("\"", "\\\"") + '"';
            _options = String.IsNullOrEmpty(sanitizedOptions) ? String.Empty : $" --download-sections \"{sanitizedOptions.Replace("\"", "\\\"")}\"";
            OutputPath = outputPath;

            taskbarManager = TaskbarManager.Instance;
        }

        private static string SanitizeInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;
            return input.Replace("\r", "").Replace("\n", "").Replace("\0", "").Trim();
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
            buttonUpdateYtDlp.Enabled = false;
            boxOutput.AppendText($"{Environment.NewLine}Starting Process");
            _downloaderProcess = new YoutubeDL(null);

            if (_infile.IndexOf("youtu", StringComparison.OrdinalIgnoreCase) >= 0)
                _downloaderProcess.StartInfo.Arguments = $@"-f ""bestvideo*+bestaudio/best"" --no-mtime --extractor-args ""youtube:player_client=android,web"" --compat-options no-youtube-unavailable-videos{_options} -- {_infile}".Trim();
            else
                _downloaderProcess.StartInfo.Arguments = $@"-f ""bestvideo*+bestaudio/best"" --no-mtime{_options} -- {_infile}".Trim();

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
                boxOutput.AppendText($"{Environment.NewLine}{Environment.NewLine}--------------------------------------------------------------------------------");
                boxOutput.AppendText($"{Environment.NewLine}[ERROR DIAGNOSIS]");
                if (isForbidden)
                    boxOutput.AppendText($"{Environment.NewLine}• HTTP 403 Forbidden detected: YouTube has rejected request signatures.");
                else if (isBotOrSign)
                    boxOutput.AppendText($"{Environment.NewLine}• Anti-bot verification or sign-in requirement detected from YouTube.");
                else if (isExtractor)
                    boxOutput.AppendText($"{Environment.NewLine}• Stream extractor failure: YouTube player JavaScript format has changed.");
                else
                    boxOutput.AppendText($"{Environment.NewLine}• Download failed with exit code {_downloaderProcess.ExitCode}.");

                boxOutput.AppendText($"{Environment.NewLine}{Environment.NewLine}Why this happens:");
                boxOutput.AppendText($"{Environment.NewLine}YouTube frequently updates its internal player APIs, streaming formats, and anti-bot");
                boxOutput.AppendText($"{Environment.NewLine}challenges. When this occurs, older yt-dlp versions cannot extract video streams.");
                boxOutput.AppendText($"{Environment.NewLine}{Environment.NewLine}[ACTIONABLE RECOVERY STEPS]");
                boxOutput.AppendText($"{Environment.NewLine}1. Click 'Update yt-dlp' below to update to the latest stable release (runs yt-dlp.exe -U).");
                boxOutput.AppendText($"{Environment.NewLine}2. If YouTube requires account verification, supply cookies or check the video in a browser.");
                boxOutput.AppendText($"{Environment.NewLine}3. Once updated, close this window and retry downloading the video.");
                boxOutput.AppendText($"{Environment.NewLine}--------------------------------------------------------------------------------{Environment.NewLine}");

                pictureStatus.BackgroundImage = StatusImages.Images["Failure"];
                buttonCancel.Enabled = true;
                buttonUpdateYtDlp.Enabled = true;
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
                        _ = TriggerYtDlpSelfUpdateAsync();
                    }
                }
            }
            else
            {
                boxOutput.AppendText($"{Environment.NewLine}{Environment.NewLine}Video downloaded succesfully!");
                pictureStatus.BackgroundImage = StatusImages.Images["Success"];
                buttonLoad.Enabled = true;
                buttonUpdateYtDlp.Enabled = true;
                buttonCancel.Enabled = true;
                buttonCancel.Text = "Close";
                MoveNewFile();
                this.Activate();
            }

            _ended = true;
        }

        private async void buttonUpdateYtDlp_Click(object sender, EventArgs e)
        {
            await TriggerYtDlpSelfUpdateAsync();
        }

        public async Task TriggerYtDlpSelfUpdateAsync()
        {
            if (_isUpdating)
                return;

            _isUpdating = true;
            buttonUpdateYtDlp.Enabled = false;
            buttonUpdateYtDlp.Text = "Updating...";
            buttonCancel.Enabled = false;

            boxOutput.AppendText($"{Environment.NewLine}{Environment.NewLine}========================================================");
            boxOutput.AppendText($"{Environment.NewLine}[UPDATE] Starting yt-dlp self-update: yt-dlp.exe -U");
            boxOutput.AppendText($"{Environment.NewLine}========================================================");

            progressBar.Style = ProgressBarStyle.Marquee;
            taskbarManager.SetProgressState(TaskbarProgressBarState.Indeterminate);

            try
            {
                var result = await UpdateBinaries.RunSelfUpdateAsync((line) =>
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        boxOutput.InvokeIfRequired(() =>
                        {
                            boxOutput.AppendText(Environment.NewLine + "[yt-dlp] " + line);
                        });
                    }
                });

                boxOutput.AppendText($"{Environment.NewLine}--------------------------------------------------------");
                if (result.Success)
                {
                    boxOutput.AppendText($"{Environment.NewLine}[UPDATE SUCCESS] {result.Message}");
                    boxOutput.AppendText($"{Environment.NewLine}yt-dlp is now updated. You can close this window and retry downloading.");
                    pictureStatus.BackgroundImage = StatusImages.Images["Success"];
                    taskbarManager.SetProgressState(TaskbarProgressBarState.Normal);
                    MessageBox.Show(this,
                        $"yt-dlp update completed!\n\n{result.Message}\n\nYou can now retry downloading your video.",
                        "yt-dlp Update Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    boxOutput.AppendText($"{Environment.NewLine}[UPDATE NOTICE] {result.Message}");
                    taskbarManager.SetProgressState(TaskbarProgressBarState.Error);
                    MessageBox.Show(this,
                        $"yt-dlp update notice:\n\n{result.Message}",
                        "Update Notice",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                boxOutput.AppendText($"{Environment.NewLine}========================================================");
            }
            catch (Exception ex)
            {
                boxOutput.AppendText($"{Environment.NewLine}[UPDATE ERROR] {ex.Message}");
            }
            finally
            {
                _isUpdating = false;
                progressBar.Style = ProgressBarStyle.Blocks;
                progressBar.Value = 0;
                taskbarManager.SetProgressState(TaskbarProgressBarState.NoProgress);
                buttonUpdateYtDlp.Text = "Update yt-dlp";
                buttonUpdateYtDlp.Enabled = true;
                buttonCancel.Enabled = true;
            }
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
            if (_isUpdating)
            {
                MessageBox.Show(this, "yt-dlp is currently updating. Please wait until it completes.", "Update in progress", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

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
            DialogResult = DialogResult.OK;
            Close();
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
