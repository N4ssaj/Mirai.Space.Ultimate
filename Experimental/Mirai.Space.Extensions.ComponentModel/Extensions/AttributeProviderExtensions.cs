using PolyType.Abstractions;

namespace Mirai.Space.Extensions.ComponentModel;

internal static class AttributeProviderExtensions
{
    extension(IGenericCustomAttributeProvider attributeProvider)
    {
        internal Attribute[] GetAttributes()
        {
            ArgumentNullException.ThrowIfNull(attributeProvider);

            var attributes = attributeProvider.GetCustomAttributes<Attribute>(inherit: true);

            // The generic overload is the source-generated, reflection-free path.
            return attributes is Attribute[] array ? array : [.. attributes];
        }
    }
}