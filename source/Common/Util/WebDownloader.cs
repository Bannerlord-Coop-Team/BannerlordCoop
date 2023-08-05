using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

#if DEBUG
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
#endif