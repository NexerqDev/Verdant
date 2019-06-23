using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Verdant
{
    public static class Tools
    {
        static Tools()
        {
            // https://stackoverflow.com/questions/2859790/the-request-was-aborted-could-not-create-ssl-tls-secure-channel
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        public static BitmapImage UrlToXamlImage(string url)
        {
            BitmapImage bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri(url, UriKind.Absolute);
            bi.EndInit();
            return bi;
        }

        public static string GetNgmPath()
        {
            using (var reg = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32)) // 32bit reg
            using (RegistryKey key = reg.OpenSubKey("Software\\Nexon\\Shared", false))
            {
                if (key == null)
                    return null;

                return key.GetValue(null).ToString() + @"\NGM\NGM.exe";
            }
        }

        [DllImport("wininet.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool InternetGetCookieEx(string pchURL, string pchCookieName, StringBuilder pchCookieData, ref uint pcchCookieData, int dwFlags, IntPtr lpReserved);
        public const int INTERNET_COOKIE_HTTPONLY = 0x00002000;
        public static string GetCookieStringData(string cookieUri)
        {
            uint datasize = 1024;
            StringBuilder cookieData = new StringBuilder((int)datasize);
            if (InternetGetCookieEx(cookieUri, null, cookieData, ref datasize, INTERNET_COOKIE_HTTPONLY, IntPtr.Zero) && cookieData.Length > 0)
            {
                return cookieData.ToString();
            }
            else
            {
                return null;
            }
        }

        [DllImport("wininet.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool InternetSetCookieEx(string lpszUrlName, string lpszCookieName, string lpszCookieData, uint dwFlags, IntPtr dwReserved);

        public static void ForceClearNaverIE()
        {
            InternetSetCookieEx("https://naver.com/", "NID_AUT", "", Tools.INTERNET_COOKIE_HTTPONLY, IntPtr.Zero);
        }

        public static string GetProgramVersion()
        {
            Version v = Assembly.GetExecutingAssembly().GetName().Version;
            return $"v{v.Major}.{v.Minor}.{v.Build}";
        }

        private static Regex githubReleaseRegex = new Regex("\"tag_name\":\"(v\\d\\.\\d\\.\\d)\"");
        public static async Task<string> GetLatestGithubReleaseName()
        {
            using (var h = new HttpClient())
            {
                var req = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/NexerqDev/Verdant/releases/latest");
                req.Headers.Add("Accept", "application/vnd.github.v3+json");
                req.Headers.Add("User-Agent", "NexerqDev/Verdant-UpdateChecker"); // gh api now REQUIRES this.

                HttpResponseMessage res = await h.SendAsync(req);

                if (!res.IsSuccessStatusCode)
                    return null;
                string data = await res.Content.ReadAsStringAsync();

                Match m = githubReleaseRegex.Match(data);
                if (!m.Success)
                    return null;

                return m.Groups[1].Value;
            }
        }

        public static async void TryPromptUpdate()
        {
            if (Properties.Settings.Default.ignoredUpdate)
                return;

            try
            {
                string remote = await GetLatestGithubReleaseName();
                if (remote == null)
                    return;

                string local = GetProgramVersion();

                if (!remote.StartsWith(local))
                {
                    MessageBoxResult mbr = MessageBox.Show("A new update for Verdant is available!: " + remote + "\n\nClick yes to open the GitHub release page to download the latest update, no to ignore this update for now, or cancel to suppress update checking.", "Verdant - Update available!", MessageBoxButton.YesNoCancel, MessageBoxImage.Information);
                    if (mbr == MessageBoxResult.No)
                        return;

                    if (mbr == MessageBoxResult.Cancel)
                    {
                        Properties.Settings.Default.ignoredUpdate = true;
                        Properties.Settings.Default.Save();
                        return;
                    }

                    Process.Start("https://github.com/NexerqDev/Verdant/releases/latest");
                }

                Debug.WriteLine("verdant already up to date (github)");
            }
            catch { }
        }
    }
}
