using Playnite.SDK;
using Playnite.SDK.Events;
using SteamLibrary.Models;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SteamLibrary.Services.Base
{
    public abstract class SteamAuthServiceBase
    {
        protected readonly ILogger logger = LogManager.GetLogger();
        protected IPlayniteAPI PlayniteApi { get; }
        protected abstract string TargetUrl { get; }
        private SteamUserToken? userTokenFromLogin;

        protected SteamAuthServiceBase(IPlayniteAPI playniteApi)
        {
            PlayniteApi = playniteApi;
        }

        public async Task<SteamUserToken> GetAccessTokenAsync()
        {
            using var view = PlayniteApi.WebViews.CreateOffscreenView();

            view.NavigateAndWait(TargetUrl);

            return await GetSteamUserTokenFromWebViewAsync(view)
                   ?? throw new Exception(PlayniteApi.Resources.GetString(LOC.SteamNotLoggedInError));
        }

        public SteamUserToken? Login()
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

                userTokenFromLogin = null;
                view.OpenDialog();
                return userTokenFromLogin;
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(PlayniteApi.Resources.GetString(LOC.SteamNotLoggedInError), "");
                logger.Error(e, "Failed to authenticate user.");
                return null;
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

        private async void CloseWhenLoggedIn(object sender, WebViewLoadingChangedEventArgs e)
        {
            try
            {
                if (e.IsLoading)
                    return;

                var view = (IWebView)sender;
                var token = await GetSteamUserTokenFromWebViewAsync(view);
                if (token?.AccessToken != null)
                {
                    userTokenFromLogin = token;
                    view.Close();
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to check authentication status");
            }
        }

        protected abstract Task<SteamUserToken?> GetSteamUserTokenFromWebViewAsync(IWebView webView);
    }
}
