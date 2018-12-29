using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using System.Windows.Navigation;

namespace Verdant
{
    public partial class MainWindow : Window
    {
        public string PathToCookies = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "login.vdt");

        public NaverAccount Account = null;
        public MapleGame Maple;

        public MainWindow()
        {
            InitializeComponent();

            Account = new NaverAccount(PathToCookies);

            mapleIdBox.IsEnabled = false;
            changeMapleIdButton.IsEnabled = false;
            startButton.IsEnabled = false;
        }

        private async Task checkAccountLogin()
        {
            try
            {
                await Account.EnsureLoggedIn();
            }
            catch (NaverAccount.LoginSessionExpiredException)
            {
                // session invalid.
                MessageBox.Show("Your Naver login has expired. A new login is required.");
                File.Delete(PathToCookies);
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (Process.GetProcessesByName("MapleStory").Length > 0
             || Process.GetProcessesByName("NGM").Length > 0)
            {
                var mbr = MessageBox.Show("You are already playing MapleStory! Continue?", "Verdant", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                if (mbr == MessageBoxResult.No)
                {
                    Close();
                    return;
                }
            }

            mapleIdLabel.Content = "Loading...";


            // NAVER LOGIN
            // check for preexist login data, and verify if session still valid
            // or, if new, show the dialog!
            // we need to run the async function synchronously - not the greatest but we'll just do this for now
            if (Account.Preloaded)
            {
                await checkAccountLogin();
                if (!Account.LoggedIn)
                    (new LoginNew(Account)).ShowDialog();
            }
            else
                (new LoginNew(Account)).ShowDialog();

            if (!Account.LoggedIn)
            {
                Close();
                return;
            }

            statusLabel.Content = "Logged in.";


            // MAPLE LOGIN/CHANNEL
            Maple = new MapleGame(Account);

            try
            {
                await Maple.Init();
            }
            catch (MapleGame.MapleNotFoundException)
            {
                MessageBox.Show("Looks like we couldn't find your installation of MapleStory! Make sure you have KMS + NGM installed properly!");
                Close();
                return;
            }
            catch (MapleGame.ChannelingRequiredException)
            {
                mapleIdLabel.Content = "Loading... (channeling)";
                try
                {
                    await Maple.Channel();
                }
                catch
                {
                    MessageBox.Show("Error channeling with Nexon and your Naver account. Please try again later.");
                    Close();
                    return;
                }
            }
            catch
            {
                MessageBox.Show("Error connecting to Maple right now, or you have not made a Maple ID! Please try again later.");
                Close();
                return;
            }

            mapleIdLabel.Content = "Web Main Character (대표 캐릭터): " + Maple.MainCharName;

            if (Maple.CharacterImageUrl != null)
                charImage.Source = Tools.UrlToXamlImage(Maple.CharacterImageUrl);

            changeMapleIdButton.IsEnabled = true;
            startButton.IsEnabled = true;
        }

        private async void startButton_Click(object sender, RoutedEventArgs e)
        {
            startButton.IsEnabled = false;

            try
            {
                await Maple.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error starting game...\n\n" + ex.ToString());
            }

            Close();
        }

        private async void changeMapleIdButton_Click(object sender, RoutedEventArgs e)
        {
            changeMapleIdButton.IsEnabled = false;
            await Maple.GetMapleIds();

            if (Maple.MapleIds.Count < 2)
            {
                MessageBox.Show("You only have 1 maple id...");
                return;
            }

            Maple.MapleIds.ForEach(x => mapleIdBox.Items.Add(x));
            mapleIdBox.Text = Maple.MainCharName;
            mapleIdBox.IsEnabled = true;
        }

        private async void mapleIdBox_DropDownClosed(object sender, EventArgs e)
        {
            // in case
            if (mapleIdBox.Text == Maple.MainCharName)
                return;

            charImage.Source = null;
            mapleIdBox.IsEnabled = false;
            mapleIdLabel.Content = "Switching...";

            try
            {
                await Maple.SwitchMapleId(mapleIdBox.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error swiching IDs...\n\n" + ex.ToString());
            }

            mapleIdLabel.Content = "Web Main Character (대표 캐릭터): " + Maple.MainCharName;
            if (Maple.CharacterImageUrl != null)
                charImage.Source = Tools.UrlToXamlImage(Maple.CharacterImageUrl);
            mapleIdBox.IsEnabled = true;
        }

        private void mapleIdBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // https://social.msdn.microsoft.com/Forums/vstudio/en-US/5e14706e-308b-44b1-8d74-aadd89e4c940/disable-up-and-down-arrow-access-key-of-combobox?forum=wpf
            if (e.Key == Key.Down || e.Key == Key.Up)
            {
                e.Handled = true;
                return; // do not call the base class method OnPreviewKeyDown()
            }
            base.OnPreviewKeyDown(e);
        }

        private void logoutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // run synchronously
                var r = Account.WebClient.GetAsync("http://static.nid.naver.com/sso/logout.nhn?return_url=https%3A%2F%2Fwww.naver.com%2F").Result;
            }
            catch { }

            File.Delete(PathToCookies);
            MessageBox.Show("Logged out. Please restart Verdant if you wish to relogin.");
            Application.Current.Shutdown();
        }
    }
}
