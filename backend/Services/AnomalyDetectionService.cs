using backend.Models;

namespace backend.Services;

public sealed class AnomalyDetectionService
{
    private const double MinimumStandardDeviation = 1;

    public IReadOnlyList<DetectedAnomaly> Detect(
        Guid endpointId,
        IReadOnlyList<EndpointMetrics> history)
    {
        if (history.Count < 3)
        {
            return [];
        }

        var anomalies = new List<DetectedAnomaly>();

        var baseline = history.SkipLast(1).ToArray();
        var latest = history[^1];

        // ---------------------------------------------------------
        // Latency anomaly detection
        // ---------------------------------------------------------

        var latencyMean =
            baseline.Average(metric => metric.LatencyMs);

        var latencyDeviation =
            StandardDeviation(
                baseline.Select(metric => metric.LatencyMs),
                latencyMean);

        var latencyThreshold =
            Math.Max(
                MinimumStandardDeviation,
                latencyDeviation * 2);

        if (Math.Abs(latest.LatencyMs - latencyMean) > latencyThreshold)
        {
            anomalies.Add(
                CreateAnomaly(
                    latest,
                    "LatencySpike",
                    latest.LatencyMs > latencyMean
                        ? "High"
                        : "Medium",
                    "Latency changed significantly from the recent baseline.",
                    latest.LatencyMs,
                    latencyMean));
        }

        // ---------------------------------------------------------
        // Availability anomaly detection
        // ---------------------------------------------------------

        var availabilityMean =
            baseline.Average(
                metric => metric.AvailabilityPercent);

        if (latest.AvailabilityPercent < availabilityMean - 1)
        {
            anomalies.Add(
                CreateAnomaly(
                    latest,
                    "AvailabilityDrop",
                    latest.AvailabilityPercent == 0
                        ? "Critical"
                        : "High",
                    "Availability dropped below the recent baseline.",
                    latest.AvailabilityPercent,
                    availabilityMean));
        }

        // ---------------------------------------------------------
        // Error-rate anomaly detection
        // ---------------------------------------------------------

        var errorRateMean =
            baseline.Average(
                metric => metric.ErrorRatePercent);

        if (latest.ErrorRatePercent > errorRateMean + 1)
        {
            anomalies.Add(
                CreateAnomaly(
                    latest,
                    "ErrorRateSpike",
                    "High",
                    "Error rate increased above the recent baseline.",
                    latest.ErrorRatePercent,
                    errorRateMean));
        }

        return anomalies;
    }

    private static DetectedAnomaly CreateAnomaly(
        EndpointMetrics metrics,
        string type,
        string severity,
        string description,
        double observedValue,
        double expectedValue)
    {
        return new DetectedAnomaly(
            Guid.NewGuid(),
            metrics.EndpointId,
            type,
            severity,
            description,
            metrics.Timestamp,
            Math.Round(observedValue, 2),
            Math.Round(expectedValue, 2))
        {
            // Links the anomaly to the exact metric that triggered it.
            MetricId = metrics.Id,

            // This service performs rule/statistical detection.
            DetectionMethod = "Rule"
        };
    }

    private static double StandardDeviation(
        IEnumerable<double> values,
        double mean)
    {
        var valuesArray = values.ToArray();

        return Math.Sqrt(
            valuesArray.Average(
                value => Math.Pow(value - mean, 2)));
    }
}