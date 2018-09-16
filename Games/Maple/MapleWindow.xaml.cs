using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace Verdant.Games.Maple
{
    /// <summary>
    /// Interaction logic for MapleWindow.xaml
    /// </summary>
    public partial class MapleWindow : Window
    {
        private MainWindow mainWindow;

        public MapleGame Maple;
        public bool ToExit = false;

        public MapleWindow(MainWindow mw)
        {
            InitializeComponent();
            mainWindow = mw;

            mapleIdBox.IsEnabled = false;
            changeMapleIdButton.IsEnabled = false;
            startButton.IsEnabled = false;
            exitCheckbox.IsEnabled = false;
        }

        bool loaded = false;
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (Process.GetProcessesByName("MapleStory").Length > 0
             || Process.GetProcessesByName("NGM").Length > 0)
            {
                MessageBox.Show("You are already playing maple!");
                Close();
                return;
            }

            mapleIdLabel.Content = "Loading...";
            Maple = new MapleGame(mainWindow.Account);
            if (!(await Maple.Init()))
            {
                MessageBox.Show("error connecting to maple rn...");
                Close();
                return;
            }

            mapleIdLabel.Content = "Maple ID (메이플ID): " + Maple.MapleId;
            changeMapleIdButton.IsEnabled = true;
            startButton.IsEnabled = true;

            exitCheckbox.IsEnabled = true;
            exitCheckbox.IsChecked = Properties.Settings.Default.autoMapleExit;
            loaded = true;
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

            if ((bool)exitCheckbox.IsChecked)
                ToExit = true;

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
            mapleIdBox.Text = Maple.MapleId;
            mapleIdBox.IsEnabled = true;
        }

        private async void mapleIdBox_DropDownClosed(object sender, EventArgs e)
        {
            // in case
            if (mapleIdBox.Text == Maple.MapleId)
                return;

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

            mapleIdLabel.Content = "Maple ID (메이플ID): " + Maple.MapleId;
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

        private void exitCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            if (!loaded)
                return;
            Properties.Settings.Default.autoMapleExit = (bool)exitCheckbox.IsChecked;
            Properties.Settings.Default.Save();
        }
    }
}
