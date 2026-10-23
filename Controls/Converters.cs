using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace NI.Controls;

public sealed class InverseBooleanVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
public sealed class ByteImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not byte[] bytes || bytes.Length == 0) return null;
        try
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 40; image.DecodePixelHeight = 40; image.StreamSource = stream; image.EndInit(); image.Freeze(); return image;
        }
        catch { return null; }
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
