using System.ComponentModel;
using PolyType;

namespace Mirai.Space.Extensions.ComponentModel;

public static class TypeShapeProviderExtensions
{
    extension(ITypeShapeProvider shapeProvider)
    {
        public PolyTypeTypeDescriptionProvider CreateTypeDescriptionProvider()
        {
            ArgumentNullException.ThrowIfNull(shapeProvider);
            return new(shapeProvider);
        }

        public PolyTypeTypeDescriptionProvider CreateTypeDescriptionProvider(TypeDescriptionProvider? parent)
        {
            ArgumentNullException.ThrowIfNull(shapeProvider);
            return new(shapeProvider, parent);
        }
    }
}