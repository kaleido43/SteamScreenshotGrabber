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
        public FormMain()
        {
            InitializeComponent();
            GenerateURL(Convert.ToInt32(this.textBoxPageStart.Text));
        }

        private void optionsChanged(object sender, EventArgs e)
        {
            try
            {
                GenerateURL(Convert.ToInt32(this.textBoxPageStart.Text));
            }
            catch
            {
                GenerateURL(1);
            }
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            try
            {
                //this.toolStripStatusLabel.Text = "Fetching URL";
                //Application.DoEvents();
                //this.textBox1.Text = Wget(this.labelURL.Text);
                //this.toolStripStatusLabel.Text = "Ready";

                int startPage = Convert.ToInt32(this.textBoxPageStart.Text);
                int endPage = Convert.ToInt32(this.textBoxPageStop.Text);

                // 
                for (int i = startPage; i <= endPage; i++)
                {
                    UpdateStatus("Generating URL");
                    GenerateURL(i);

                    UpdateStatus("Fetching page " + i);
                    Wget(this.labelURL.Text, "albumpage.html");
                    string[] html = File.ReadAllLines("albumpage.html");

                    UpdateStatus("Parsing image URLs from page " + i);
                    List<string> imageUrls = ParseImageUrls(html);

                    for (int j = 0; j < imageUrls.Count; j++)
                    {
                        UpdateStatus("Fetching image " + (j + 1) + "/" + imageUrls.Count + " from page " + i);
                        DownloadImage(imageUrls[j]);
                    }
                }

                // foreach page:

                // wget url

                // Grab all screenshots in the album page
                // ParseImageUrls(html)

            }
            catch (Exception ex)
            {
            }
        }

        private void DownloadImage(string imagePageURL)
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
        }

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

        private void GenerateURL(int pageNum)
        {
            string order = "newest";
            if (this.radioButton2.Checked) order = "oldest";

            // Create a URL
            // Sample: http://steamcommunity.com/profiles/76561198003853507/screenshots/?p=1&sort=oldestfirst&view=grid
            this.labelURL.Text = "http://steamcommunity.com/profiles/" +
                this.textBoxSteamID.Text + "/screenshots/?p=" +
                pageNum +
                "&sort=" +
                order +
                "first&view=grid";
        }

        // Grabbed from:  http://stackoverflow.com/questions/26233/fastest-c-sharp-code-to-download-a-web-page
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

        private void UpdateStatus(string msg)
        {
            this.toolStripStatusLabel.Text = msg;
            Application.DoEvents();
        }

    }
}
