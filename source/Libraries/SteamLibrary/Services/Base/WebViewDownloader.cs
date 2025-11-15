using Playnite.SDK;

namespace SteamLibrary.Services.Base
{
    public interface IWebViewDownloader
    {
        string DownloadPageSource(string url);
    }

    public class WebViewDownloader : IWebViewDownloader
    {
        private readonly IWebViewFactory webViewFactory;

        public WebViewDownloader(IWebViewFactory webViewFactory)
        {
            this.webViewFactory = webViewFactory;
        }

        public string DownloadPageSource(string url)
        {
            using (var webView = webViewFactory.CreateOffscreenView())
            {
                webView.NavigateAndWait(url);
                return webView.GetPageSource();
            }
        }
    }
}