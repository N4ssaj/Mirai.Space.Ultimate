using System.ComponentModel;
using System.Globalization;

namespace Mirai.Space.Extensions.ComponentModel.Tests;

public sealed class UpperCaseConverter : TypeConverter
{
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        return value is string text ? text.ToUpperInvariant() : base.ConvertFrom(context, culture, value);
    }
}
