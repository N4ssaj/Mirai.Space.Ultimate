using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Mirai.Space.Extensions.Clone.SourceGeneration;

[Generator(LanguageNames.CSharp)]
public sealed class CloneGenerator : IIncrementalGenerator
{
    private const string CloneableAttributeName = "Mirai.Space.Extensions.Clone.CloneableAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                CloneableAttributeName,
                static (node, _) => node is TypeDeclarationSyntax,
                static (attributeContext, cancellationToken) => CloneableModelBuilder.Create(attributeContext, cancellationToken))
            .Where(static candidate => candidate is not null);

        context.RegisterSourceOutput(
            candidates,
            static (productionContext, candidate) => CloneEmitter.Emit(productionContext, candidate!));
    }
}
