using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using mshtml;

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
                    addNaverCookieData(ss[0], ss[1]);
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

        private void addNaverCookieData(string name, string value)
        {
            Cookie c = new Cookie();
            c.Name = name;
            c.Value = value;
            c.Domain = ".naver.com";
            c.Path = "/";
            c.Expires = DateTime.Now.AddMonths(3);
            c.HttpOnly = (c.Name == "NID_AUT") ? true : false;
            account.Cookies.Add(c);
        }

        Regex devToolsRegex = new Regex("^(.*?)\\t(.*?)\\t", RegexOptions.Multiline);
        private async void tryClipboardLogin()
        {
            // clipboard cookie based login - fuck naver's absolutely terrible captcha, and cbs working out how bvsd works! :)
            // we have to use dev tools copy paste though because NID_AUT is httponly, so cant just use document.cookie :(
            try
            {
                string clipboard = Clipboard.GetText(TextDataFormat.Text);
                if (!clipboard.Contains("NID_AUT\t"))
                    return;

                var mbr = MessageBox.Show("Found what looks like Chrome devtools cookies clipboard data to login with. Use it?", "Verdant", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (mbr == MessageBoxResult.No)
                    return;

                webBrowser.IsEnabled = false;
                foreach (Match m in devToolsRegex.Matches(clipboard))
                    addNaverCookieData(m.Groups[1].Value, m.Groups[2].Value);

                // catch will catch this too
                await account.EnsureLoggedIn();
                account.SaveCookies();
                Close();
            }
            catch
            {
                MessageBox.Show("Clipboard login failed.", "Verdant", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                webBrowser.IsEnabled = true;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            tryClipboardLogin();
        }

        private void webBrowser_LoadCompleted(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            // QOL: tick the stay signed in automatically
            if (e.Uri.AbsoluteUri.Contains("/nidlogin.login"))
            {
                var document = (HTMLDocument)webBrowser.Document;
                IHTMLElement staySignedIn = document.getElementById("login_chk");
                if (staySignedIn != null)
                    staySignedIn.click();
            }
        }
    }
}
