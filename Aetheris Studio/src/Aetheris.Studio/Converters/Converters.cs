using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;
using System.Globalization;

namespace Aetheris.Studio.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is Visibility.Visible;
    }
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is not true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is not true;
    }
}

public sealed class NotNullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value == null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public sealed class NotNullToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value != null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var count = value switch
        {
            int i => i,
            long l => (int)l,
            System.Collections.ICollection c => c.Count,
            _ => 0
        };

        var inverse = parameter?.ToString()?.Equals("inverse", StringComparison.OrdinalIgnoreCase) == true;

        if (inverse)
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        return count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public sealed class EnumToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value == null) return string.Empty;

        var enumType = value.GetType();
        if (!enumType.IsEnum) return value.ToString() ?? string.Empty;

        var name = Enum.GetName(enumType, value);
        if (string.IsNullOrEmpty(name)) return string.Empty;

        // Add spaces before capital letters for readability
        return System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public sealed class FormatStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var format = parameter as string;
        if (string.IsNullOrEmpty(format))
            return value?.ToString() ?? string.Empty;

        try
        {
            return string.Format(CultureInfo.CurrentCulture, format, value);
        }
        catch
        {
            return value?.ToString() ?? string.Empty;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public sealed class ByteArrayToBitmapConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not byte[] bytes || bytes.Length == 0) return null;

        try
        {
            var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            var writer = new Windows.Storage.Streams.DataWriter(stream);
            writer.WriteBytes(bytes);
            writer.StoreAsync().GetResults();
            writer.FlushAsync().GetResults();
            stream.Seek(0);

            var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
            bitmap.SetSourceAsync(stream).AsTask().Wait();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}