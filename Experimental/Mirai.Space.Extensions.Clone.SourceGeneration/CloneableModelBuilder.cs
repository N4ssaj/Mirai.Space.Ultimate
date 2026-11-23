using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Mirai.Space.Extensions.Clone.SourceGeneration;

internal static class CloneableModelBuilder
{
    private const string CloneableAttributeName = "Mirai.Space.Extensions.Clone.CloneableAttribute";
    private const string CloneIgnoreAttributeName = "Mirai.Space.Extensions.Clone.CloneIgnoreAttribute";

    internal static CloneCandidate? Create(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not INamedTypeSymbol type)
        {
            return null;
        }

        var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var className = BuildClassName(type);
        var hintName = className + ".g.cs";
        var generateCloneMethod = context.Attributes.Any(
            attribute => attribute.NamedArguments.Any(
                argument => argument.Key == "GenerateCloneMethod" && argument.Value.Value is true));

        var methodSkipReason = GetMethodSkipReason(type, generateCloneMethod, cancellationToken);
        var canEmitMethods = generateCloneMethod && methodSkipReason is null;

        if (!IsSupportedTarget(type, out var targetReason))
        {
            return new CloneCandidate(
                typeName,
                type.Name,
                ns,
                hintName,
                className,
                type.DeclaredAccessibility == Accessibility.Public,
                type.IsValueType,
                type.IsRecord,
                type.IsReadOnly,
                Array.Empty<MemberCandidate>(),
                false,
                targetReason,
                CloneDiagnostics.InvalidTarget,
                generateCloneMethod,
                false,
                methodSkipReason);
        }

        if (!TryBuildMembers(type, out var members, out var memberReason))
        {
            return new CloneCandidate(
                typeName,
                type.Name,
                ns,
                hintName,
                className,
                type.DeclaredAccessibility == Accessibility.Public,
                type.IsValueType,
                type.IsRecord,
                type.IsReadOnly,
                Array.Empty<MemberCandidate>(),
                false,
                memberReason,
                CloneDiagnostics.Skipped,
                generateCloneMethod,
                false,
                methodSkipReason);
        }

        members.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));
        return new CloneCandidate(
            typeName,
            type.Name,
            ns,
            hintName,
            className,
            type.DeclaredAccessibility == Accessibility.Public,
            type.IsValueType,
            type.IsRecord,
            type.IsReadOnly,
            members.ToArray(),
            true,
            null,
            null,
            generateCloneMethod,
            canEmitMethods,
            methodSkipReason);
    }

    private static bool IsSupportedTarget(INamedTypeSymbol type, out string reason)
    {
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct))
        {
            reason = "only classes and structs are supported";
            return false;
        }

        if (type.IsAbstract)
        {
            reason = "abstract and static types are not supported";
            return false;
        }

        if (type.IsStatic)
        {
            reason = "static types are not supported";
            return false;
        }

        if (type.IsRefLikeType)
        {
            reason = "ref structs are not supported";
            return false;
        }

        if (type.IsFileLocal)
        {
            reason = "file-local types are not supported";
            return false;
        }

        if (!IsTypeAccessible(type))
        {
            reason = "the type is not accessible from generated code";
            return false;
        }

        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.TypeParameters.Length > 0)
            {
                reason = "generic types are not supported";
                return false;
            }
        }

        var baseType = type.BaseType;
        if (baseType is not null
            && baseType.SpecialType is not SpecialType.System_Object and not SpecialType.System_ValueType)
        {
            reason = "types with a base class are not supported";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool TryBuildMembers(
        INamedTypeSymbol type,
        out List<MemberCandidate> members,
        out string reason)
    {
        members = new List<MemberCandidate>();
        foreach (var member in type.GetMembers())
        {
            if (member is not IFieldSymbol field || field.IsStatic)
            {
                continue;
            }

            if (field.IsImplicitlyDeclared)
            {
                var property = FindBackingProperty(type, field);
                if (property is null)
                {
                    // Primary-constructor captures and event backing fields are compiler state we
                    // cannot assign safely; let the runtime fallback handle the whole type.
                    reason = $"compiler-generated field '{field.Name}' is not an auto-property backing field";
                    return false;
                }

                if (IsIgnored(property) || IsIgnored(field))
                {
                    continue;
                }

                if (property.GetMethod is null || property.SetMethod is null)
                {
                    reason = $"auto-property '{property.Name}' has no accessible setter";
                    return false;
                }

                if (property.SetMethod.IsInitOnly)
                {
                    reason = $"auto-property '{property.Name}' is init-only";
                    return false;
                }

                if (!IsAccessible(property.GetMethod) || !IsAccessible(property.SetMethod))
                {
                    reason = $"auto-property '{property.Name}' has an inaccessible accessor";
                    return false;
                }

                if (IsUnsupportedMemberType(property.Type))
                {
                    reason = $"auto-property '{property.Name}' has an unsupported type";
                    return false;
                }

                members.Add(new MemberCandidate(property.Name, property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                continue;
            }

            if (field.IsConst)
            {
                continue;
            }

            if (IsIgnored(field))
            {
                continue;
            }

            if (field.IsReadOnly)
            {
                reason = $"field '{field.Name}' is readonly";
                return false;
            }

            if (field.RefKind != RefKind.None || field.IsFixedSizeBuffer)
            {
                reason = $"field '{field.Name}' is a ref or fixed buffer field";
                return false;
            }

            if (IsUnsupportedMemberType(field.Type))
            {
                reason = $"field '{field.Name}' has an unsupported type";
                return false;
            }

            if (!IsAccessible(field))
            {
                reason = $"field '{field.Name}' is not accessible from generated code";
                return false;
            }

            members.Add(new MemberCandidate(field.Name, field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        }

        reason = string.Empty;
        return true;
    }

    private static bool IsUnsupportedMemberType(ITypeSymbol type) =>
        type.TypeKind == TypeKind.Pointer || type.IsRefLikeType;

    private static bool IsAccessible(ISymbol symbol) =>
        symbol.DeclaredAccessibility is Accessibility.Public
            or Accessibility.Internal
            or Accessibility.ProtectedOrInternal;

    private static bool IsTypeAccessible(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility is Accessibility.Private
                or Accessibility.Protected
                or Accessibility.ProtectedAndInternal)
            {
                return false;
            }
        }

        return true;
    }

    private static IPropertySymbol? FindBackingProperty(INamedTypeSymbol type, IFieldSymbol field)
    {
        var name = field.Name;
        if (name.Length == 0 || name[0] != '<' || !name.EndsWith(">k__BackingField", StringComparison.Ordinal))
        {
            return null;
        }

        var end = name.IndexOf('>');
        if (end <= 1)
        {
            return null;
        }

        var propertyName = name.Substring(1, end - 1);
        return type.GetMembers(propertyName).OfType<IPropertySymbol>().FirstOrDefault(static property => !property.IsStatic);
    }

    private static bool IsIgnored(ISymbol symbol) =>
        symbol.GetAttributes().Any(
            attribute => attribute.AttributeClass?.ToDisplayString() == CloneIgnoreAttributeName);

    private static string? GetMethodSkipReason(
        INamedTypeSymbol type,
        bool generateCloneMethod,
        CancellationToken cancellationToken)
    {
        if (!generateCloneMethod)
        {
            return null;
        }

        if (type.ContainingType is not null)
        {
            return "nested types are not supported for generated methods";
        }

        if (!IsPartial(type, cancellationToken))
        {
            return "the type must be declared partial";
        }

        if (HasMemberConflict(type, "Clone") || HasMemberConflict(type, "DeepClone"))
        {
            return "a member named Clone or DeepClone already exists";
        }

        return null;
    }

    private static bool IsPartial(INamedTypeSymbol type, CancellationToken cancellationToken)
    {
        var declarations = type.DeclaringSyntaxReferences;
        if (declarations.Length == 0)
        {
            return false;
        }

        foreach (var declaration in declarations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (declaration.GetSyntax(cancellationToken) is not TypeDeclarationSyntax syntax
                || !syntax.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasMemberConflict(INamedTypeSymbol type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.GetMembers(name).Length > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildClassName(INamedTypeSymbol type)
    {
        var displayName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var builder = new StringBuilder("ClonePlan_");
        foreach (var character in type.MetadataName)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        builder.Append('_');
        builder.Append(ShortHash(displayName));
        return builder.ToString();
    }

    private static string ShortHash(string value)
    {
        var hash = 2166136261u;
        foreach (var character in value)
        {
            hash ^= character;
            hash *= 16777619u;
        }

        return hash.ToString("x8", CultureInfo.InvariantCulture);
    }
}
