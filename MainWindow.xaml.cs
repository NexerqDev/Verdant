using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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

        private bool otherIdsLoaded = false;

        public MainWindow()
        {
            InitializeComponent();

            Account = new NaverAccount(PathToCookies);

            toggleUi(false);
            Tools.TryPromptUpdate();
        }

        private void toggleUi(bool status)
        {
            changeMapleIdButton.IsEnabled = otherIdsLoaded ? false : status;
            startButton.IsEnabled = status;
            tespiaCheckBox.IsEnabled = status;
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



            // check for a custom form
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1 && args[1].EndsWith(".dll"))
            {
                Assembly asm = Assembly.LoadFile(Path.GetFullPath(args[1]));
                Type t = asm.GetExportedTypes().First(a => typeof(Window).IsAssignableFrom(a));

                Window w = (Window)Activator.CreateInstance(t, Account);
                w.Title += " (powered by Verdant)";

                w.Show();
                Close();
                return;
            }



            // MAPLE LOGIN/CHANNEL
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

            Maple = new MapleGame(Account);

            try
            {
                await Maple.Init();
            }
            catch (VerdantException.GameNotFoundException)
            {
                MessageBox.Show("Looks like we couldn't find your installation of MapleStory! Make sure you have KMS + NGM installed properly!");
                Close();
                return;
            }
            catch (VerdantException.ChannelingRequiredException)
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

            toggleUi(true);
        }

        private bool noAuth = false;
        private void startButton_Click(object sender, RoutedEventArgs e)
        {
            startGame();
        }

        private async void startGame()
        {
            toggleUi(false);

            try
            {
                await Maple.Start((bool)tespiaCheckBox.IsChecked);
            }
            catch (VerdantException.NoAuthException)
            {
                if (noAuth)
                {
                    MessageBox.Show("We failed to auth. Login data will be deleted, and Verdant will be closed. Please try logging in completely again.");
                    File.Delete(PathToCookies);
                    Close();
                    return;
                }

                // last ditch
                noAuth = true;
                await mapleIdSelection(true);
                startButton.IsEnabled = false;

                if (Maple.MapleIds.Count == 1)
                {
                    mapleIdBox.Text = Maple.MapleIds[0];
                    await switchMapleId();
                    Close();
                }
                else
                {
                    MessageBox.Show("There was an error starting the game, but... we have one last trick. Please select the Maple ID in the dropdown box below that you would like to login to, and we can try again!", "Verdant");
                    mapleIdBox.Focus();
                    mapleIdBox.IsDropDownOpen = true;
                }

                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error starting game...\n\n" + ex.ToString());
            }

            Close();
        }

        private void changeMapleIdButton_Click(object sender, RoutedEventArgs e)
        {
            mapleIdSelection();
        }

        private async Task mapleIdSelection(bool inStart = false)
        {
            toggleUi(false);
            await Maple.GetMapleIds();
            otherIdsLoaded = true;

            if (!inStart && Maple.MapleIds.Count < 2)
            {
                MessageBox.Show("You only have 1 maple id...");
                return;
            }

            Maple.MapleIds.ForEach(x => mapleIdBox.Items.Add(x));
            mapleIdBox.IsEnabled = true;
            toggleUi(true);
        }

        private void mapleIdBox_DropDownClosed(object sender, EventArgs e)
        {
            switchMapleId();
        }

        private async Task switchMapleId()
        {
            if (String.IsNullOrEmpty(mapleIdBox.Text) || !Maple.MapleIds.Contains(mapleIdBox.Text))
                return;

            charImage.Source = null;
            toggleUi(false);
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
            tespiaCheckBox.IsChecked = false;

            if (noAuth)
            {
                Account.SaveCookies();
                startGame();
                return;
            }

            toggleUi(true);
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
            Account.UiLogout();
        }

        private async void tespiaCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            toggleUi(false);
            try
            {
                await Maple.UseTespia();
            }
            catch (MapleGame.TespiaNotEnabled)
            {
                var mbr = MessageBox.Show("You have not signed up for the Test Server yet. Click yes to open up your browser to the signup page, signup, then try again.", "Verdant", MessageBoxButton.YesNo, MessageBoxImage.Error);
                if (mbr == MessageBoxResult.Yes)
                    Process.Start("https://maplestory.nexon.game.naver.com/MyMaple/TestWorld/Apply");
                tespiaCheckBox.IsChecked = false;
            }
            toggleUi(true);
        }
    }
}
