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
using System.Windows.Navigation;

namespace Verdant
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public NaverAccount Account = null;

        public string PathToCookies = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "login.vdt");

        public MainWindow()
        {
            InitializeComponent();

            Account = new NaverAccount(PathToCookies);

            (new Login(this)).ShowDialog();
            statusLabel.Content = "logged in as: " + Account.Nickname;

            if (!String.IsNullOrEmpty(Account.AvatarUrl))
            {
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(Account.AvatarUrl, UriKind.Absolute);
                bi.EndInit();
                avatarImage.Source = bi;
            }
        }

        private void autoStartBox_Checked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.autoMapleStart = (bool)autoStartBox.IsChecked;
            Properties.Settings.Default.Save();
        }

        private void logoutButton_Click(object sender, RoutedEventArgs e)
        {
            File.Delete(PathToCookies);
            MessageBox.Show("Logged out. Please restart Verdant if you wish to relogin.");
            Application.Current.Shutdown();
        }

        private void mapleImage_MouseUp(object sender, MouseButtonEventArgs e)
        {
            loadMaple();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (Properties.Settings.Default.autoMapleStart)
                loadMaple();
        }

        private void loadMaple()
        {
            var w = new Games.Maple.MapleWindow(this);
            w.ShowDialog();
            if (w.ToExit)
                Application.Current.Shutdown();
        }
    }
}
