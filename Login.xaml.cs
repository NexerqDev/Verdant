using System;
using System.Collections.Generic;
using System.IO;
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

namespace Verdant
{
    /// <summary>
    /// Interaction logic for Login.xaml
    /// </summary>
    public partial class Login : Window
    {
        MainWindow mainWindow;
        bool doingOtp = false;
        bool loaded = false;

        public Login(MainWindow mw)
        {
            InitializeComponent();
            mainWindow = mw;

            init();
        }

        private async void init()
        {
            enableStuff(false);

            try
            {
                if (account.Preloaded)
                {
                    await account.EnsureLoggedIn();
                    Close();
                    return;
                }
            }
            catch (NaverAccount.LoginSessionExpiredException)
            {
                // session invalid.
                // just continue on down
                MessageBox.Show("Your Naver login has expired. Please login again.");
                File.Delete(mainWindow.PathToCookies);
            }
            catch (Exception e)
            {
                MessageBox.Show("Error...\n\n" + e.ToString());
            }
            finally
            {
                enableStuff(true);
                focusFields();

                tryClipboardLogin();
            }
        }

        private void enableStuff(bool b)
        {
            usernameBox.IsEnabled = b;
            passwordBox.IsEnabled = b;
            loginButton.IsEnabled = b;
            captchaBox.IsEnabled = b;
        }

        private NaverAccount account => mainWindow.Account;
        private async void loginButton_Click(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrEmpty(usernameBox.Text) || String.IsNullOrEmpty(passwordBox.Password))
                return; // drop if empty input
            if (account.WaitingCaptcha && String.IsNullOrEmpty(captchaBox.Text))
                return;

            enableStuff(false);

            statusLabel.Content = "Logging in...";
            try
            {
                if (account.WaitingCaptcha)
                {
                    try
                    {
                        await account.Login(usernameBox.Text, passwordBox.Password, captchaBox.Text);
                    }
                    catch (NaverAccount.WrongCaptchaException)
                    {
                        MessageBox.Show("Invalid captcha OR invalid password - please double check these and try again!");
                        enableStuff(true);
                        captchaBox.Visibility = Visibility.Hidden;
                        captchaImage.Visibility = Visibility.Hidden;
                        captchaLabel.Visibility = Visibility.Hidden;
                        refreshCaptchaButton.Visibility = Visibility.Hidden;
                        account.WaitingCaptcha = false;
                        captchaBox.Text = "";
                        return;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error...\n\n" + ex.ToString());
                        return;
                    }
                }
                else
                {
                    await account.Login(usernameBox.Text, passwordBox.Password);
                }
            }
            catch (NaverAccount.WrongPasswordException)
            {
                MessageBox.Show("Invalid password. Please try again.");
                enableStuff(true);
                return;
            }

            if (account.WaitingCaptcha)
            {
                captchaBox.Visibility = Visibility.Visible;
                captchaImage.Visibility = Visibility.Visible;
                captchaLabel.Visibility = Visibility.Visible;
                refreshCaptchaButton.Visibility = Visibility.Visible;

                statusLabel.Content = "Captcha required.";
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(account.CaptchaImageUrl, UriKind.Absolute);
                bi.EndInit();
                captchaImage.Source = bi;

                enableStuff(true);
                captchaBox.Focus();
                return;
            }

            if (account.WaitingOtp)
            {
                doingOtp = true;
                otpTimer();

                await account.DoOtp();
                doingOtp = false;
            }

            await account.EnsureLoggedIn();
            mainWindow.Account = account;

            if ((bool)rememberBox.IsChecked)
            {
                Properties.Settings.Default.naverId = usernameBox.Text;
                Properties.Settings.Default.Save();
            }

            Close();
        }

        private async void otpTimer()
        {
            int time = 5 * 60;
            while (true)
            {
                statusLabel.Content = $"OTP requested. Waiting... ({time} secs)";
                if (!doingOtp)
                    break;

                --time;
                await Task.Delay(1000);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!String.IsNullOrEmpty(Properties.Settings.Default.naverId))
            {
                usernameBox.Text = Properties.Settings.Default.naverId;
                passwordBox.Focus();
                rememberBox.IsChecked = true;
            }
            focusFields();
            loaded = true;
        }

        private void focusFields()
        {
            usernameBox.Focus();
            if (!String.IsNullOrEmpty(usernameBox.Text))
                passwordBox.Focus();
        }

        private void rememberBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!loaded)
                return;
            Properties.Settings.Default.naverId = "";
            Properties.Settings.Default.Save();
        }

        int refreshCaptchaTimes = 1;
        private void refreshCaptchaButton_Click(object sender, RoutedEventArgs e)
        {
            captchaImage.Source = null;
            BitmapImage bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri(account.CaptchaImageUrl + new string('1', ++refreshCaptchaTimes), UriKind.Absolute);
            bi.EndInit();
            captchaImage.Source = bi;
            captchaBox.Focus();
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

                enableStuff(false);
                statusLabel.Content = "Found clipboard-based login data, trying that...";
                foreach (Match m in devToolsRegex.Matches(clipboard))
                {
                    Cookie c = new Cookie();
                    c.Name = m.Groups[1].Value;
                    c.Value = m.Groups[2].Value;
                    c.Domain = ".naver.com";
                    c.Path = "/";
                    c.Expires = DateTime.Now.AddMonths(3);
                    c.HttpOnly = (c.Name == "NID_AUT") ? true : false;
                    account.Cookies.Add(c);
                }

                // catch will catch this too
                await account.EnsureLoggedIn();
                Close();
            }
            catch
            {
                statusLabel.Content = "Failed to clipboard login... Ready.";
                enableStuff(true);
            }
        }
    }
}
