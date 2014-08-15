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

namespace SteamScreenshotGrabber
{
    public partial class FormMain : Form
    {

        BackgroundWorker bgWorker = null; // The background thread that does the downloading
        int startPage = 0; // The first page to download
        int endPage = 0; // The last page to download
        string albumPageUrl = ""; // THe URL of the current album page

        /// <summary>
        /// Boring constructor is boring...
        /// </summary>
        public FormMain()
        {
            InitializeComponent();
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
                    this.buttonStart.Text = "Stop";

                    this.startPage = Convert.ToInt32(this.textBoxPageStart.Text);
                    this.endPage = Convert.ToInt32(this.textBoxPageStart.Text);

                    // Start up the worker thread
                    this.bgWorker = new BackgroundWorker();
                    this.bgWorker.WorkerReportsProgress = true;
                    this.bgWorker.WorkerSupportsCancellation = true;
                    this.bgWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bgWorker_RunWorkerCompleted);
                    this.bgWorker.ProgressChanged += new ProgressChangedEventHandler(bgWorker_ProgressChanged);
                    this.bgWorker.DoWork += new DoWorkEventHandler(bgWorker_DoWork);
                    this.bgWorker.RunWorkerAsync();
                }
                else
                {
                    // Stop the worker thread
                    this.bgWorker.CancelAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
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
                if (this.bgWorker.CancellationPending)
                {
                    this.bgWorker.ReportProgress(0, "Cancelling...");
                    return;
                }

                // Generate the URL to the current album page
                this.bgWorker.ReportProgress(0, "Generating album page URL...");
                GenerateURL(i);
                this.bgWorker.ReportProgress(0, "Generated: " + albumPageUrl);

                // Download the current album page
                this.bgWorker.ReportProgress(0, "Downloading album page " + i + "...");
                Wget(this.albumPageUrl, "albumpage.html");
                string[] html = File.ReadAllLines("albumpage.html");

                // Parse all screenshot page URLs from the album page
                this.bgWorker.ReportProgress(0, "Seraching for screenshot URLs on page " + i + "...");
                List<string> imageUrls = ParseImageUrls(html);
                this.bgWorker.ReportProgress(0, "Found " + imageUrls.Count + " screenshots on this page.");

                // Download each image
                this.bgWorker.ReportProgress(0, "Downloading screenshots...");
                for (int j = 0; j < imageUrls.Count; j++)
                {
                    this.bgWorker.ReportProgress(0, "Downloading screenshot " + (j + 1) + "/" + imageUrls.Count + " from page " + i + "...");
                    string imgFile = DownloadImage(imageUrls[j]);
                    this.bgWorker.ReportProgress(0, "Screenshot saved to: " + imgFile);

                    if (this.bgWorker.CancellationPending)
                    {
                        this.bgWorker.ReportProgress(0, "Cancelling...");
                        return;
                    }
                }
            }

            this.bgWorker.ReportProgress(0, "Finished downloading all screenshots.");
        }

        /// <summary>
        /// Used by the download thread to update the GUI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void bgWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            this.richTextBox.AppendText(DateTime.Now.ToString("[HH:mm:ss] ") + (string)e.UserState + "\r\n");
            this.richTextBox.ScrollToCaret();
        }

        /// <summary>
        /// Runs after the download thread is done, resets the GUI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void bgWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.richTextBox.AppendText(DateTime.Now.ToString("[HH:mm:ss] ") + "Done\r\n");
            this.richTextBox.ScrollToCaret();
            this.bgWorker = null;
            this.buttonStart.Text = "Start";
        }

        /// <summary>
        /// Given a URL to the page that contains a screenshot, downloads the image and names it appropriately
        /// </summary>
        /// <param name="imagePageURL"></param>
        /// <returns></returns>
        private string DownloadImage(string imagePageURL)
        {
            Wget(imagePageURL, "imagepage.html");
            string[] lines = File.ReadAllLines("imagepage.html");

            string imageUrl = "";
            string imageDestinationFilename = "";

            foreach (string line in lines)
            {
                // Find the direct URL to the image itself
                if (line.Contains("ActualMedia"))
                {
                    // Grab the direct URL of the image
                    // Sample line of what I'm expecting:
                    // \t\t\t\t<a href=\"http://cloud-4.steampowered.com/ugc/595804413485986490/B75F91916AB097CA12337C01BB3B2A7A678A7F45/\" target=\"_blank\"><img id=\"ActualMedia\" class=\"screenshotEnlargeable\" src=\"http://cloud-4.steampowered.com/ugc/595804413485986490/B75F91916AB097CA12337C01BB3B2A7A678A7F45/1024x640.resizedimage\" width=\"1024\"></a>
                    imageUrl = Regex.Match(line, "http[^\"]+").Captures[0].Value;
                }

                // Find the timestamp of the image
                if (line.Contains("detailsStatRight") && line.Contains(" @ "))
                {
                    // Sample line of what I'm expecting:
                    // \t\t\t\t\t\t\t\t<div class=\"detailsStatRight\">Feb 26, 2011 @ 2:40pm</div>

                    // Get the date of the screenshot
                    string date = Regex.Match(line, "[>]([^<]+)").Captures[0].Value; // ">Feb 26, 2011 @ 2:40pm"
                    date = date.TrimStart('>'); // "Feb 26, 2011 @ 2:40pm"
                    date = date.Replace(" @ ", " "); // "Feb 26, 2011 2:40pm"
                    DateTime dt = DateTime.Parse(date);

                    // Generate a filename (this is needed because there can be multiple screenshots taken the same minute)
                    int number = 0;
                    do
                    {
                        imageDestinationFilename = dt.ToString("yyyy-MM-dd HH-mm-") + number.ToString("D2") + ".jpg";
                        number++;
                    }
                    while (File.Exists(imageDestinationFilename));
                }
            }

            // Download the screenshot
            Wget(imageUrl, imageDestinationFilename);
            return imageDestinationFilename;
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

        // Thanks to:  http://stackoverflow.com/questions/26233/fastest-c-sharp-code-to-download-a-web-page
        public string Wget(string url)
        {
            WebClient client = new WebClient();
            client.DownloadFile(url, "imagepage.html");
            return client.DownloadString(url);
        }
        public void Wget(string url, string localFilename)
        {
            WebClient client = new WebClient();
            if (File.Exists(localFilename)) File.Delete(localFilename);
            client.DownloadFile(url, localFilename);
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
