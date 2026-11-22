namespace Mirai.Space.Extensions.Http.Abstractions;

public sealed record ApiProblemDetails(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    string? Instance);
