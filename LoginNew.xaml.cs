using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Verdant
{
    /// <summary>
    /// Interaction logic for LoginNew.xaml
    /// </summary>
    public partial class LoginNew : Window
    {
        private NaverAccount account;

        public LoginNew(NaverAccount account)
        {
            InitializeComponent();

            this.account = account;

            // we specifically use this url redirect so we know when auth success
            webBrowser.Navigate("https://nid.naver.com/nidlogin.login?mode=form&url=https://blog.naver.com/nexerq");
        }

        private void webBrowser_Navigating(object sender, System.Windows.Navigation.NavigatingCancelEventArgs e)
        {
            if (e.Uri.AbsoluteUri == "https://blog.naver.com/nexerq")
            {
                // check if auth really success
                e.Cancel = true;

                try
                {
                    getSystemCookiesAndTest();
                    Close();
                }
                catch
                {
                    MessageBox.Show("Something went very wrong while logging in...", "Verdant");
                    Close();
                }
            }
        }

        private void getSystemCookiesAndTest()
        {
            string cookies = Tools.GetCookieStringData("https://naver.com");
            if (cookies != null && cookies.Contains("NID_AUT"))
            {
                // success - now we add properly, then double check and go!
                foreach (string s in cookies.Split(new string[] { "; " }, StringSplitOptions.None))
                {
                    string[] ss = s.Split('=');

                    Cookie c = new Cookie();
                    c.Name = ss[0];
                    c.Value = ss[1];
                    c.Domain = ".naver.com";
                    c.Path = "/";
                    c.Expires = DateTime.Now.AddMonths(3);
                    c.HttpOnly = (c.Name == "NID_AUT") ? true : false;
                    account.Cookies.Add(c);
                }

                Task.Run(() => account.EnsureLoggedIn()).Wait();
                account.SaveCookies();
                account.LoggedIn = true;
            }
        }

        private void resetButton_Click(object sender, RoutedEventArgs e)
        {
            webBrowser.Navigate("https://nid.naver.com/nidlogin.login?mode=form&url=https://blog.naver.com/nexerq");
        }
    }
}
