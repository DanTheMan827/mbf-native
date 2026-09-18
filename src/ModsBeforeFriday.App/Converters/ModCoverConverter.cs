using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ModsBeforeFriday.App.Converters;

public sealed class ModCoverConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        var text = value as string;
        if (string.IsNullOrWhiteSpace(text) || !Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return new BitmapImage(uri);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
