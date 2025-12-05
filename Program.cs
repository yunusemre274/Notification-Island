using System;
using System.Windows;

namespace NI
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            
            var mainWindow = new MainWindow();
            app.Run(mainWindow);
        }
    }
}
