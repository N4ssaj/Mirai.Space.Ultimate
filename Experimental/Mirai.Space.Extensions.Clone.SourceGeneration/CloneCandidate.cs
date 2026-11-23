using Microsoft.CodeAnalysis;

namespace Mirai.Space.Extensions.Clone.SourceGeneration;

internal sealed record CloneCandidate(
    string TypeDisplayName,
    string DeclarationName,
    string? Namespace,
    string HintName,
    string ClassName,
    bool IsPublic,
    bool IsValueType,
    bool IsRecord,
    bool IsReadOnly,
    MemberCandidate[] Members,
    bool IsEligible,
    string? SkipReason,
    DiagnosticDescriptor? Diagnostic,
    bool GenerateCloneMethod,
    bool CanEmitMethods,
    string? MethodSkipReason);
