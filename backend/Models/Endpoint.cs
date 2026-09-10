namespace backend.Models;

public sealed record Endpoint(
    Guid Id,
    string Name,
    string Url,
    string Status,
    DateTimeOffset CreatedAt);