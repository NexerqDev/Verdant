using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Verdant
{
    public static class Tools
    {
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
    }
}
