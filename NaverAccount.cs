using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Verdant
{
    public class NaverAccount
    {
        private HttpClientHandler WebClientHandler;
        private string pathToSavedCookies;

        public HttpClient WebClient;

        public CookieContainer Cookies
        {
            get { return WebClientHandler.CookieContainer; }
            set { WebClientHandler.CookieContainer = value; }
        }

        public bool Preloaded = false;
        public bool LoggedIn = false;

        public NaverAccount(string pathToSavedCookies = null)
        {
            this.pathToSavedCookies = pathToSavedCookies;
            var cc = new CookieContainer();
            if (pathToSavedCookies != null && File.Exists(pathToSavedCookies))
            {
                try
                {
                    using (Stream s = File.Open(pathToSavedCookies, FileMode.Open))
                    {
                        var f = new BinaryFormatter();
                        cc = (CookieContainer)f.Deserialize(s);
                        Preloaded = true;
                    }
                }
                catch
                {
                    // corrupted?
                    File.Delete(pathToSavedCookies);
                }
            }

            WebClientHandler = new HttpClientHandler()
            {
                AllowAutoRedirect = true,
                UseCookies = true,
                CookieContainer = cc
            };

            WebClient = new HttpClient(WebClientHandler);

            WebClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; WOW64; Trident/7.0; rv:11.0) like Gecko");
            WebClient.DefaultRequestHeaders.Add("DNT", "1");
            WebClient.DefaultRequestHeaders.Add("Accept-Language", "ko-KR");
        }

        public void SaveCookies()
        {
            if (pathToSavedCookies == null)
                return;

            using (Stream s = File.Create(pathToSavedCookies))
            {
                BinaryFormatter f = new BinaryFormatter();
                f.Serialize(s, Cookies);
            }
        }

        public class LoginSessionExpiredException : Exception { }
        public async Task EnsureLoggedIn()
        {
            // not the best, but we have to use new to prevent auto 302
            using (var hch = new HttpClientHandler() { AllowAutoRedirect = false, CookieContainer = Cookies, UseCookies = true })
            using (var hc = new HttpClient(hch))
            {
                var hrm = await hc.GetAsync("https://nid.naver.com/user2/api/route.nhn?m=routePcMyInfo");
                if (hrm.StatusCode != HttpStatusCode.Redirect)
                    throw new Exception("wtf");

                if (hrm.Headers.Location.OriginalString.Contains("nidlogin.login"))
                    throw new LoginSessionExpiredException();

                LoggedIn = true;
            }
        }

        public void Logout()
        {
            try
            {
                // run synchronously
                var r = WebClient.GetAsync("http://static.nid.naver.com/sso/logout.nhn?return_url=https%3A%2F%2Fwww.naver.com%2F").Result;
            }
            catch { }
            File.Delete(pathToSavedCookies);
        }

        /// <summary>
        /// UI logout: includes message box, IE cookie clear and hard quits the app. Use just Logout() for a soft logout.
        /// </summary>
        public void UiLogout()
        {
            Logout();
            Tools.ForceClearNaverIE();

            System.Windows.MessageBox.Show("Logged out. Please restart Verdant if you wish to relogin.");
            Environment.Exit(0);
        }
    }
}
