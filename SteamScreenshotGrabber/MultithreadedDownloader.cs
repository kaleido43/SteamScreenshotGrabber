using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace SteamScreenshotGrabber
{
    public class MultithreadedDownloader
    {
        public bool cancellationPending = false;
        private int threadsRunning = 0;

        List<string[]> receivedHtml = new List<string[]>();
        List<string> urlsToDownload = new List<string>();
        List<string> filenamesToUse = new List<string>();

        public void CancelAsync()
        {
            this.cancellationPending = true;
        }

        /// <summary>
        /// Given a list of URLs to web pages, downloads them all and returns the HTML in a list
        /// </summary>
        /// <param name="urls"></param>
        /// <param name="threads"></param>
        /// <returns></returns>
        public List<string[]> Download(List<string> urls, int threads)
        {
            // Save urls as a member var
            this.urlsToDownload = new List<string>(urls);
            this.receivedHtml.Clear();

            // Spawn threads
            Log.Instance.LogMsg("Spawning " + threads + " download threads...");
            for (int i = 0; i < threads; i++)
            {
                new Thread(new ThreadStart(DownloadHtmlThreadBody)).Start();
            }
            Thread.Sleep(100); // Wait a bit to allow threads to start up

            // Wait for threads to finish
            while (threadsRunning > 0)
            {
                Thread.Sleep(50);
            }

            // Return the HTML
            Log.Instance.LogMsg("All download threads done.");
            this.cancellationPending = false;
            return new List<string[]>(receivedHtml);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="urls"></param>
        /// <param name="threads"></param>
        /// <returns></returns>
        public void Download(List<string> urls, List<string> filenames, int threads)
        {
            // Save urls as a member var
            this.urlsToDownload = new List<string>(urls);
            this.filenamesToUse = new List<string>(filenames);

            // Spawn threads
            Log.Instance.LogMsg("Spawning " + threads + " download threads...");
            for (int i = 0; i < threads; i++)
            {
                new Thread(new ThreadStart(DownloadFilesThreadBody)).Start();
            }
            Thread.Sleep(100); // Wait a bit to allow threads to start up

            // Wait for threads to finish
            while (threadsRunning > 0)
            {
                Thread.Sleep(50);
            }

            // Return the HTML
            Log.Instance.LogMsg("All download threads done.");
        }

        private void DownloadHtmlThreadBody()
        {
            lock (this) threadsRunning++;
            Log.Instance.LogMsg("Download thread started.");

            while (true)
            {
                if (this.cancellationPending) break;

                string url = "";

                // Grab a URL to download
                lock (this)
                {
                    if (this.urlsToDownload.Count == 0) break;
                    url = this.urlsToDownload[0];
                    this.urlsToDownload.RemoveAt(0);
                }

                Log.Instance.LogMsg("Attempting to download " + url + "...");

                // Try to download it until success (sometimes it can fail)
                bool downloaded = false;
                do
                {
                    try
                    {
                        if (this.cancellationPending) break;
                        string[] html = Utils.GetPageHtml(url);
                        lock (this)
                        {
                            this.receivedHtml.Add(html);
                        }
                        downloaded = true;
                        Log.Instance.LogMsg("Downloaded " + url + ".");
                    }
                    catch
                    {
                        Log.Instance.LogMsg("Error downloading " + url + ", trying again.");
                    }
                }
                while (!downloaded);
            }

            Log.Instance.LogMsg("Download thread ended.");
            lock (this) threadsRunning--;
        }

        private void DownloadFilesThreadBody()
        {
            lock (this) threadsRunning++;
            Log.Instance.LogMsg("Download thread started.");

            while (true)
            {
                if (this.cancellationPending) break;

                string url = "";
                string file = "";

                // Grab a URL to download
                lock (this)
                {
                    if (this.urlsToDownload.Count == 0) break;
                    url = this.urlsToDownload[0];
                    file = this.filenamesToUse[0];
                    this.urlsToDownload.RemoveAt(0);
                    this.filenamesToUse.RemoveAt(0);
                }

                Log.Instance.LogMsg("Attempting to download " + url + "...");

                // Try to download it until success (sometimes it can fail)
                bool downloaded = false;
                do
                {
                    try
                    {
                        if (this.cancellationPending) break;
                        Utils.Wget(url, file);
                        downloaded = true;
                        Log.Instance.LogMsg("Downloaded " + url + " to file " + file + ".");
                    }
                    catch
                    {
                        Log.Instance.LogMsg("Error downloading " + url + ", trying again.");
                    }
                }
                while (!downloaded);
            }

            Log.Instance.LogMsg("Download thread ended.");
            lock (this) threadsRunning--;
        }
    }
}
