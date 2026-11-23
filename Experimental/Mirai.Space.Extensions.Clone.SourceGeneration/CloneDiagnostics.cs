using Microsoft.CodeAnalysis;

namespace Mirai.Space.Extensions.Clone.SourceGeneration;

internal static class CloneDiagnostics
{
    internal static readonly DiagnosticDescriptor Skipped = new(
        "MSCLONE001",
        "Clone source generation skipped",
        "Clone source generation was skipped for '{0}': {1}",
        "Mirai.Space.Extensions.Clone",
        DiagnosticSeverity.Info,
        true);

    internal static readonly DiagnosticDescriptor InvalidTarget = new(
        "MSCLONE002",
        "Invalid cloneable target",
        "'{0}' cannot use source-generated cloning: {1}",
        "Mirai.Space.Extensions.Clone",
        DiagnosticSeverity.Warning,
        true);

    internal static readonly DiagnosticDescriptor MethodSkipped = new(
        "MSCLONE003",
        "Generated clone method skipped",
        "The clone method for '{0}' was not generated: {1}",
        "Mirai.Space.Extensions.Clone",
        DiagnosticSeverity.Warning,
        true);
}
