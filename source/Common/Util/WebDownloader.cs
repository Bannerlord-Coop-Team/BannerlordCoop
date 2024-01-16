using System;
using System.Net;

namespace Common.Util
{
    public class WebDownloader
    {
        public void DownloadFile(string url, string fullPath)
        {
            using WebClient wc = new WebClient();
            wc.DownloadFile(new Uri(url), fullPath);
        }

        public void DownloadFileAsync(string url, string fullPath)
        {
            using WebClient wc = new WebClient();
            wc.DownloadFileAsync(new Uri(url), fullPath);
        }
    }
}