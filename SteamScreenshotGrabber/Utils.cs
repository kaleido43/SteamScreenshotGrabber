using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;

namespace SteamScreenshotGrabber
{
    class Utils
    {
        /// <summary>
        /// Fetches the HTML for a webpage.  Each line of HTML is an entry in the return array.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static string[] GetPageHtml(string url)
        {
            // Download the html
            WebClient client = new WebClient();
            string html = client.DownloadString(url);

            // Split into lines
            string[] lines = html.Split('\r', '\n');

            // Remove empty lines
            List<string> nonEmptyLines = new List<string>();
            foreach (string line in lines)
            {
                if (line.Length > 0)
                {
                    nonEmptyLines.Add(line);
                }
            }
            return nonEmptyLines.ToArray();
        }

        // Thanks to:  http://stackoverflow.com/questions/26233/fastest-c-sharp-code-to-download-a-web-page
        public static void Wget(string url, string localFilename)
        {
            WebClient client = new WebClient();
            if (File.Exists(localFilename)) File.Delete(localFilename);
            client.DownloadFile(url, localFilename);
        }
    }
}
