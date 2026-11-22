namespace Mirai.Space.Extensions.Http.Abstractions;

public sealed record RequestProblemDetails(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    string? Instance);
