using System;
using System.Windows;
using NI.App.Services;

namespace NI.App
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            
            // Use frozen resources for performance
            app.Resources.Add("FrozenBlack", CreateFrozenBrush("#000000"));
            app.Resources.Add("FrozenWhite", CreateFrozenBrush("#FFFFFF"));
            app.Resources.Add("FrozenGreen", CreateFrozenBrush("#2EE36E"));
            
            var main = new MainWindow();
            app.Run(main);
        }

        private static System.Windows.Media.SolidColorBrush CreateFrozenBrush(string hex)
        {
            var brush = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
    }
}
