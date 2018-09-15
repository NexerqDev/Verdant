using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Verdant
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App() : base()
        {
            this.Dispatcher.UnhandledException += OnDispatcherUnhandledException;
        }

        void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show("A fatal error has been thrown in Verdant. Please report this error, alongside what you did in the lead up (in DETAIL) to the GitHub issue tracker - this would be much appreciated. Thank you!\n\n" + e.Exception.ToString());
            Application.Current.Shutdown();
            e.Handled = true;
        }
    }
}
