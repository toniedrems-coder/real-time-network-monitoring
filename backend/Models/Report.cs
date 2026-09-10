namespace backend.Models;

public sealed record Report(
    Guid Id,
    DateTimeOffset GeneratedAt,
    string Format,
    byte[] Data);