﻿using AmazonGamesLibrary.Models;
using Microsoft.Win32;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using PlayniteExtensions.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace AmazonGamesLibrary.Services
{
    public class AmazonAccountClient
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private AmazonGamesLibrary library;
        private const string loginUrl = @"https://www.amazon.com/ap/signin?openid.ns=http://specs.openid.net/auth/2.0&openid.claimed_id=http://specs.openid.net/auth/2.0/identifier_select&openid.identity=http://specs.openid.net/auth/2.0/identifier_select&openid.mode=checkid_setup&openid.oa2.scope=device_auth_access&openid.ns.oa2=http://www.amazon.com/ap/ext/oauth/2&openid.oa2.response_type=token&openid.oa2.client_id=device:6330386435633439383366623032393938313066303133343139383335313266234132554D56484F58375550345637&language=en_US&marketPlaceId=ATVPDKIKX0DER&openid.return_to=https://www.amazon.com&openid.pape.max_auth_age=0&openid.assoc_handle=amzn_sonic_games_launcher&pageId=amzn_sonic_games_launcher";
        private readonly string tokensPath;

        public AmazonAccountClient(AmazonGamesLibrary library)
        {
            this.library = library;
            tokensPath = Path.Combine(library.GetPluginUserDataPath(), "tokens.json");
        }

        public async Task Login()
        {
            var callbackUrl = string.Empty;
            using (var webView = library.PlayniteApi.WebViews.CreateView(490, 660))
            {
                webView.LoadingChanged += (s, e) =>
                {
                    var url = webView.GetCurrentAddress();
                    if (url.Contains("openid.oa2.access_token"))
                    {
                        callbackUrl = url;
                        webView.Close();
                    }
                };

                if (File.Exists(tokensPath))
                {
                    File.Delete(tokensPath);
                }

                webView.DeleteDomainCookies(".amazon.com");
                webView.Navigate(loginUrl);
                webView.OpenDialog();
            }

            if (!callbackUrl.IsNullOrEmpty())
            {
                var rediUri = new Uri(callbackUrl);
                var fragments = HttpUtility.ParseQueryString(rediUri.Query);
                var token = fragments["openid.oa2.access_token"];
                await Authenticate(token);
            }
        }

        private async Task Authenticate(string accessToken)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "AGSLauncher/1.0.0");
                var reqData = new DeviceRegistrationRequest();
                reqData.auth_data.access_token = accessToken;
                reqData.registration_data.app_name = "AGSLauncher for Windows";
                reqData.registration_data.app_version = "1.0.0";
                reqData.registration_data.device_model = "Windows";
                reqData.registration_data.device_serial = GetMachineGuid().ToString("N");
                reqData.registration_data.device_type = "A2UMVHOX7UP4V7";
                reqData.registration_data.domain = "Device";
                reqData.registration_data.os_version = Environment.OSVersion.Version.ToString(4);
                reqData.requested_extensions = new List<string> { "customer_info", "device_info" };
                reqData.requested_token_type = new List<string> { "bearer", "mac_dms" };

                var authPostContent = Serialization.ToJson(reqData, true);

                var authResponse = await client.PostAsync(
                    @"https://api.amazon.com/auth/register",
                    new StringContent(authPostContent, Encoding.UTF8, "application/json"));

                var authResponseContent = await authResponse.Content.ReadAsStringAsync();
                var authData = Serialization.FromJson<DeviceRegistrationResponse>(authResponseContent);
                if (authData.response?.success != null)
                {
                    Encryption.EncryptToFile(
                        tokensPath,
                        Serialization.ToJson(authData.response.success.tokens.bearer),
                        Encoding.UTF8,
                        WindowsIdentity.GetCurrent().User.Value);
                }
            }
        }

        public async Task<List<Entitlement>> GetAccountEntitlements()
        {
            if (!(await GetIsUserLoggedIn()))
            {
                throw new Exception("User is not authenticated.");
            }

            var entitlements = new List<Entitlement>();
            var token = LoadToken();
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "com.amazon.agslauncher.win/3.0.9124.0");
                client.DefaultRequestHeaders.Add("X-Amz-Target", "com.amazon.animusdistributionservice.entitlement.AnimusEntitlementsService.GetEntitlements");
                client.DefaultRequestHeaders.Add("x-amzn-token", token.access_token);

                string nextToken = null;
                var reqData = new EntitlementsRequest
                {
                    // not sure what key this is but it's some key from Amazon.Fuel.Plugin.Entitlement.dll
                    keyId = "d5dc8b8b-86c8-4fc4-ae93-18c0def5314d",
                    hardwareHash = Guid.NewGuid().ToString("N")
                };

                do
                {
                    reqData.nextToken = nextToken;
                    var strCont = new StringContent(Serialization.ToJson(reqData, true), Encoding.UTF8, "application/json");
                    strCont.Headers.TryAddWithoutValidation("Expect", "100-continue");
                    strCont.Headers.TryAddWithoutValidation("Content-Encoding", "amz-1.0");

                    var entlsResponse = await client.PostAsync(
                        @"https://gaming.amazon.com/api/distribution/entitlements",
                        strCont);

                    var entlsResponseContent = await entlsResponse.Content.ReadAsStringAsync();
                    var entlsData = Serialization.FromJson<EntitlementsResponse>(entlsResponseContent);
                    nextToken = entlsData?.nextToken;
                    if (entlsData?.entitlements.HasItems() == true)
                    {
                        entitlements.AddRange(entlsData.entitlements);
                    }
                } while (!nextToken.IsNullOrEmpty());

                return entitlements;
            }
        }

        private DeviceRegistrationResponse.Response.Success.Tokens.Bearer LoadToken()
        {
            if (File.Exists(tokensPath))
            {
                try
                {
                    return Serialization.FromJson<DeviceRegistrationResponse.Response.Success.Tokens.Bearer>(
                        Encryption.DecryptFromFile(
                            tokensPath,
                            Encoding.UTF8,
                            WindowsIdentity.GetCurrent().User.Value));
                }
                catch (Exception e)
                {
                    logger.Error(e, "Failed to load saved tokens.");
                }
            }

            return null;
        }

        private async Task<DeviceRegistrationResponse.Response.Success.Tokens.Bearer> RefreshTokens()
        {
            var token = LoadToken();
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "com.amazon.agslauncher.win/1.1.133.2-9e2c3a3");
                var reqData = new TokenRefreshRequest
                {
                    app_name = "AGSLauncher",
                    app_version = "1.1.133.2-9e2c3a3",
                    source_token = token.refresh_token,
                    requested_token_type = "access_token",
                    source_token_type = "refresh_token"
                };

                var authPostContent = Serialization.ToJson(reqData, true);
                var strcont = new StringContent(authPostContent, Encoding.UTF8, "application/json");
                strcont.Headers.TryAddWithoutValidation("Expect", "100-continue");

                var authResponse = await client.PostAsync(
                    @"https://api.amazon.com/auth/token",
                    strcont);

                var authResponseContent = await authResponse.Content.ReadAsStringAsync();
                var authData = Serialization.FromJson<DeviceRegistrationResponse.Response.Success.Tokens.Bearer>(authResponseContent);
                token.access_token = authData.access_token;
                Encryption.EncryptToFile(
                    tokensPath,
                    Serialization.ToJson(token),
                    Encoding.UTF8,
                    WindowsIdentity.GetCurrent().User.Value);
                return token;
            }
        }

        public async Task<bool> GetIsUserLoggedIn()
        {
            var token = LoadToken();
            if (token == null)
            {
                return false;
            }

            token = await RefreshTokens();
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "AGSLauncher/1.0.0");
                client.DefaultRequestHeaders.Add("Authorization", "bearer " + token.access_token);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                var infoResponse = await client.GetAsync(@"https://api.amazon.com/user/profile");
                var infoResponseContent = await infoResponse.Content.ReadAsStringAsync();
                var infoData = Serialization.FromJson<ProfileInfo>(infoResponseContent);
                return !infoData.user_id.IsNullOrEmpty();
            }
        }

        public static Guid GetMachineGuid()
        {
            RegistryKey root = null;
            if (Environment.Is64BitOperatingSystem)
            {
                root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            }
            else
            {
                root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            }

            try
            {
                using (var cryptography = root.OpenSubKey("SOFTWARE\\Microsoft\\Cryptography"))
                {
                    return Guid.Parse((string)cryptography.GetValue("MachineGuid"));
                }
            }
            finally
            {
                root.Dispose();
            }
        }
    }
}
