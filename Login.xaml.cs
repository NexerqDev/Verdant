using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                if (File.Exists(mainWindow.PathToCookies))
                {
                    await account.GetUserDetails();
                    Close();
                    return;
                }
            }
            catch (NaverAccount.LoginSessionExpiredException)
            {
                // session invalid.
                // just continue on down
                MessageBox.Show("Your Naver login has expired. Please login again.");
            }
            catch (Exception e)
            {
                MessageBox.Show("Error...\n\n" + e.ToString());
            }
            finally
            {
                enableStuff(true);
                focusFields();
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
            if (account.WaitingCaptcha)
            {
                try
                {
                    await account.Login(usernameBox.Text, passwordBox.Password, captchaBox.Text);
                }
                catch (NaverAccount.WrongCaptchaException)
                {
                    MessageBox.Show("Invalid captcha... relog pls");
                    enableStuff(true);
                    captchaBox.Visibility = Visibility.Hidden;
                    captchaImage.Visibility = Visibility.Hidden;
                    captchaLabel.Visibility = Visibility.Hidden;
                    account.WaitingCaptcha = false;
                    return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error...\n\n" + ex.ToString());
                }
            }
            else
            {
                await account.Login(usernameBox.Text, passwordBox.Password);
            }

            if (account.WaitingCaptcha)
            {
                captchaBox.Visibility = Visibility.Visible;
                captchaImage.Visibility = Visibility.Visible;
                captchaLabel.Visibility = Visibility.Visible;

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

            await account.GetUserDetails();
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
    }
}
