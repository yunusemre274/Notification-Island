using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace NI.App.Services
{
    /// <summary>
    /// Resolves app icons from file paths or extracts from executables.
    /// </summary>
    public static class AppIconResolver
    {
        public static BitmapImage? Resolve(string? path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return null;
                if (!File.Exists(path)) return null;

                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(path);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Extract icon from an executable file.
        /// Requires System.Drawing reference for full implementation.
        /// </summary>
        public static BitmapImage? ExtractFromExe(string exePath)
        {
            // Simplified: would use Icon.ExtractAssociatedIcon in full implementation
            return null;
        }
    }
}
