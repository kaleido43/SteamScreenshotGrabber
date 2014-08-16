using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Reflection;

namespace SteamScreenshotGrabber
{
    public partial class FormMain : Form
    {
        MultithreadedDownloader mtd = new MultithreadedDownloader();
        BackgroundWorker bgWorker = null; // The background thread that does the downloading
        int startPage = 0; // The first page to download
        int endPage = 0; // The last page to download
        string albumPageUrl = ""; // The URL of the current album page
        int numOfThreads = 2; // The number of download threads to use

        /// <summary>
        /// Boring constructor is boring...
        /// </summary>
        public FormMain()
        {
            InitializeComponent();
            Log.Instance.LogEvent += new StringDelegate(LogMsg);
        }

        /// <summary>
        /// Either kicks off the download thread or stops it, depending on what's currently going on.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonStart_Click(object sender, EventArgs e)
        {
            try
            {
                if (this.bgWorker == null)
                {
                    // Update the GUI
                    this.buttonStart.Text = "Stop";

                    // Read latest settings
                    this.startPage = Convert.ToInt32(this.textBoxPageStart.Text);
                    this.endPage = Convert.ToInt32(this.textBoxPageStop.Text);
                    this.numOfThreads = Convert.ToInt32(this.comboBox1.Text);

                    // Start up the worker thread
                    this.bgWorker = new BackgroundWorker();
                    this.bgWorker.WorkerReportsProgress = true;
                    this.bgWorker.WorkerSupportsCancellation = true;
                    this.bgWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bgWorker_RunWorkerCompleted);
                    this.bgWorker.DoWork += new DoWorkEventHandler(bgWorker_DoWork);
                    this.bgWorker.RunWorkerAsync();
                }
                else
                {
                    // Stop the worker thread
                    this.mtd.CancelAsync();
                    this.bgWorker.CancelAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private delegate void SetControlPropertyThreadSafeDelegate(string msg);
        public void LogMsg(string msg)
        {
            if (this.richTextBox.InvokeRequired)
            {
                this.richTextBox.Invoke(new SetControlPropertyThreadSafeDelegate(LogMsg), new object[] { msg });
            }
            else
            {
                this.richTextBox.AppendText(DateTime.Now.ToString("[HH:mm:ss] ") + msg + "\r\n");
                this.richTextBox.ScrollToCaret();
            }
        }

        /// <summary>
        /// The guts of the download thread.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void bgWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            int startPage = this.startPage;
            int endPage = this.endPage;

            for (int i = startPage; i <= endPage; i++)
            {
                if (this.bgWorker.CancellationPending) return;

                // Generate the URL to the current album page
                Log.Instance.LogMsg("Generating album page URL...");
                GenerateURL(i);
                Log.Instance.LogMsg("Generated: " + albumPageUrl);

                // Download the current album page
                Log.Instance.LogMsg("Downloading album page " + i + "...");
                string[] html = Utils.GetPageHtml(this.albumPageUrl);

                // Parse all screenshot page URLs from the album page
                Log.Instance.LogMsg("Searching for screenshot URLs on page " + i + "...");
                List<string> imageUrls = ParseImageUrls(html);
                Log.Instance.LogMsg("Found " + imageUrls.Count + " screenshots on this page.");

                // Download the HTML for each screenshot page
                Log.Instance.LogMsg("Attempting to download the page for each screenshot on this album page...");
                if (this.bgWorker.CancellationPending) return;
                List<string[]> pageHtmls = mtd.Download(imageUrls, this.numOfThreads);

                // Determine URLs and filenames for each screenshot
                List<string> screenshotUrls = new List<string>();
                List<string> screenshotFilenames = new List<string>();
                foreach (string[] lines in pageHtmls)
                {
                    foreach (string line in lines)
                    {
                        // Find the direct URL to the image itself
                        if (line.Contains("ActualMedia"))
                        {
                            // Grab the direct URL of the image
                            // Sample line of what I'm expecting:
                            // \t\t\t\t<a href=\"http://cloud-4.steampowered.com/ugc/595804413485986490/B75F91916AB097CA12337C01BB3B2A7A678A7F45/\" target=\"_blank\"><img id=\"ActualMedia\" class=\"screenshotEnlargeable\" src=\"http://cloud-4.steampowered.com/ugc/595804413485986490/B75F91916AB097CA12337C01BB3B2A7A678A7F45/1024x640.resizedimage\" width=\"1024\"></a>
                            screenshotUrls.Add(Regex.Match(line, "http[^\"]+").Captures[0].Value);
                        }

                        // Find the timestamp of the image
                        if (line.Contains("detailsStatRight") && line.Contains(" @ "))
                        {
                            // Sample line of what I'm expecting:
                            // \t\t\t\t\t\t\t\t<div class=\"detailsStatRight\">Feb 26, 2011 @ 2:40pm</div>
                            // \t\t\t\t\t\t\t\t<div class=\"detailsStatRight\">Feb 26 @ 2:40pm</div>
                            // Note there are two different formats.  The one missing the year is THIS year.

                            // Get the date of the screenshot
                            string date = Regex.Match(line, "[>]([^<]+)").Captures[0].Value; // ">Feb 26, 2011 @ 2:40pm"
                            date = date.TrimStart('>'); // "Feb 26, 2011 @ 2:40pm" or "Feb 26 @ 2:40pm"
                            if (date.Contains(","))
                            {
                                // "Feb 26, 2011 @ 2:40pm"
                                date = date.Replace(" @ ", " "); // "Feb 26, 2011 2:40pm"
                            }
                            else
                            {
                                // "Feb 26 @ 2:40pm"
                                date = date.Replace(" @ ", ", " + DateTime.Now.Year + " "); // "Feb 26, 2011 2:40pm"
                            }

                            DateTime dt = DateTime.Parse(date);

                            // Generate a filename (while loop is needed because there can be multiple screenshots taken the same minute)
                            int number = 0;
                            string imageDestinationFilename = "";
                            do
                            {
                                imageDestinationFilename = dt.ToString("yyyy-MM-dd HH-mm-") + number.ToString("D2") + ".jpg";
                                number++;
                            }
                            while (screenshotFilenames.Contains(imageDestinationFilename) || File.Exists(imageDestinationFilename));
                            screenshotFilenames.Add(imageDestinationFilename);
                        }
                    }
                }

                if (this.bgWorker.CancellationPending) return;

                // Now download each screenshot
                mtd.Download(screenshotUrls, screenshotFilenames, this.numOfThreads);
            }

            this.bgWorker.ReportProgress(0, "Finished downloading all screenshots.");
        }

        /// <summary>
        /// Runs after the download thread is done, resets the GUI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void bgWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.bgWorker = null;

            // Update the GUI
            this.richTextBox.AppendText(DateTime.Now.ToString("[HH:mm:ss] ") + "Done\r\n");
            this.richTextBox.ScrollToCaret();
            this.buttonStart.Text = "Start";
        }

        /// <summary>
        /// Finds all image URLs in the provided HTML
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        private List<string> ParseImageUrls(string[] html)
        {
            List<string> urls = new List<string>();

            foreach (string line in html)
            {
                if (!line.Contains("http://steamcommunity.com/sharedfiles/filedetails/?id="))
                    continue;

                string pattern = "http://steamcommunity.com/sharedfiles/filedetails/[?]id=[0-9]+";
                Match m = Regex.Match(line, pattern);
                string result = m.Captures[0].Value;
                //Wget(result);
                urls.Add(result);
            }

            return urls;
        }

        /// <summary>
        /// Generates a URL to the appropriate screenshot album page
        /// </summary>
        /// <param name="pageNum"></param>
        private void GenerateURL(int pageNum)
        {
            string order = "newest";
            if (this.radioButton2.Checked) order = "oldest";

            this.startPage = Convert.ToInt32(this.textBoxPageStart.Text);
            this.endPage = Convert.ToInt32(this.textBoxPageStop.Text);

            // Create a URL
            // Sample: http://steamcommunity.com/profiles/76561198003853507/screenshots/?p=1&sort=oldestfirst&view=grid
            this.albumPageUrl = "http://steamcommunity.com/profiles/" +
                this.textBoxSteamID.Text + "/screenshots/?p=" +
                pageNum +
                "&sort=" +
                order +
                "first&view=grid";
        }

        /// <summary>
        /// Keeps the GUI sized as the size changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FormMain_Resize(object sender, EventArgs e)
        {
            this.richTextBox.Width = this.ClientSize.Width - this.richTextBox.Left * 2;
            this.richTextBox.Height = this.ClientSize.Height - this.richTextBox.Top - this.richTextBox.Left;
            this.buttonStart.Left = this.ClientSize.Width - this.buttonStart.Width - this.richTextBox.Left;
        }

        /// <summary>
        /// Keeps the GUI sized as the size changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FormMain_Load(object sender, EventArgs e)
        {
            FormMain_Resize(null, null);
            this.comboBox1.SelectedIndex = 1;
        }

        /// <summary>
        /// Attempt to shut down the background thread on shutdown
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                this.bgWorker.CancelAsync();
            }
            catch
            {
                // Either it wasn't running or we couldn't cancel it
                // Oh well, at least we tried...
            }
        }
    }
}
