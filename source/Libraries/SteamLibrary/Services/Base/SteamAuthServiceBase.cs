using Playnite.SDK;
using Playnite.SDK.Events;
using SteamLibrary.Models;
using System;
using System.Diagnostics;

namespace SteamLibrary.Services.Base
{
    public abstract class SteamAuthServiceBase
    {
        protected ILogger logger = LogManager.GetLogger();
        protected IPlayniteAPI PlayniteApi { get; }
        protected abstract string TargetUrl { get; }

        public SteamAuthServiceBase(IPlayniteAPI playniteApi)
        {
            PlayniteApi = playniteApi;
        }
        
        public SteamUserToken GetAccessToken()
        {
            using (var view = PlayniteApi.WebViews.CreateOffscreenView())
            {
                view.NavigateAndWait(TargetUrl);
                return GetSteamUserTokenFromWebView(view)
                       ?? throw new Exception(PlayniteApi.Resources.GetString(LOC.SteamNotLoggedInError));
            }
        }

        public void Login()
        {
            var view = PlayniteApi.WebViews.CreateView(600, 720);
            try
            {
                view.LoadingChanged += CloseWhenLoggedIn;
                view.DeleteDomainCookies(".steamcommunity.com");
                view.DeleteDomainCookies("steamcommunity.com");
                view.DeleteDomainCookies("steampowered.com");
                view.DeleteDomainCookies("store.steampowered.com");
                view.DeleteDomainCookies("help.steampowered.com");
                view.DeleteDomainCookies("login.steampowered.com");
                view.Navigate(TargetUrl);
                view.OpenDialog();
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(PlayniteApi.Resources.GetString(LOC.SteamNotLoggedInError), "");
                logger.Error(e, "Failed to authenticate user.");
            }
            finally
            {
                if (view != null)
                {
                    view.LoadingChanged -= CloseWhenLoggedIn;
                    view.Dispose();
                }
            }
        }

        private void CloseWhenLoggedIn(object sender, WebViewLoadingChangedEventArgs e)
        {
            if (e.IsLoading)
                return;

            var view = (IWebView)sender;
            var userToken = GetSteamUserTokenFromWebView(view);
            if (userToken?.AccessToken != null)
                view.Close();
        }

        protected abstract SteamUserToken? GetSteamUserTokenFromWebView(IWebView webView);
    }
}
