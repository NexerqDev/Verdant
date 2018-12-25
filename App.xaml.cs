using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Verdant
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
#if !DEBUG
        public App() : base()
        {
            this.Dispatcher.UnhandledException += OnDispatcherUnhandledException;
        }

        // single instance ~ some from https://stackoverflow.com/questions/19147/what-is-the-correct-way-to-create-a-single-instance-wpf-application
        static Mutex siMutex = new Mutex(true, "NexerqDev.Verdant-SingleInstance");
        protected override void OnStartup(StartupEventArgs e)
        {
            if (!siMutex.WaitOne(TimeSpan.Zero, true))
            {
                MessageBox.Show("Only one instance of Verdant may be running at the same time!", "Error - Verdant", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
                siMutex = null;
                return;
            }
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (siMutex != null)
                siMutex.ReleaseMutex();
            base.OnExit(e);
        }

        void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show("A fatal error has been thrown in Verdant. Please report this error, alongside what you did in the lead up (in DETAIL) to the GitHub issue tracker - this would be much appreciated. Thank you!\n\n" + e.Exception.ToString());
            Application.Current.Shutdown();
            e.Handled = true;
        }
#endif
    }
}
