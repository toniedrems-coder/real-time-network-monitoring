namespace backend.Models;

public enum FailureType
{
    Unknown,
    Timeout,
    HttpError,
    Unavailable,
    Anomaly
}