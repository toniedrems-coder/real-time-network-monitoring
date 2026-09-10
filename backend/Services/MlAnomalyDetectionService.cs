using backend.Models;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.TimeSeries;

namespace backend.Services;

/// <summary>
/// ML.NET-based anomaly detector using an IID spike detection model (Microsoft.ML.TimeSeries).
/// Runs independently alongside <see cref="AnomalyDetectionService"/>'s rule-based/statistical
/// checks, giving a second, model-driven signal for less obviously-threshold-crossing patterns
/// (e.g. gradual drift or subtle irregular spikes the fixed 2-sigma rule may miss).
/// </summary>
public sealed class MlAnomalyDetectionService
{
    private const int MinimumHistoryForMl = 12;
    private const double ConfidenceLevel = 95d;

    private readonly MLContext mlContext = new(seed: 0);

    public IReadOnlyList<DetectedAnomaly> Detect(Guid endpointId, IReadOnlyList<EndpointMetrics> history)
    {
        if (history.Count < MinimumHistoryForMl)
        {
            return [];
        }

        var anomalies = new List<DetectedAnomaly>();

        TryDetectSpike(
            endpointId,
            history,
            metric => metric.LatencyMs,
            "LatencySpike",
            "Latency",
            anomalies);

        TryDetectSpike(
            endpointId,
            history,
            metric => metric.ErrorRatePercent,
            "ErrorRateSpike",
            "Error rate",
            anomalies);

        TryDetectSpike(
            endpointId,
            history,
            metric => metric.ThroughputMbps,
            "ThroughputAnomaly",
            "Throughput",
            anomalies);

        return anomalies;
    }

    private void TryDetectSpike(
        Guid endpointId,
        IReadOnlyList<EndpointMetrics> history,
        Func<EndpointMetrics, double> selector,
        string type,
        string label,
        List<DetectedAnomaly> anomalies)
    {
        var series = history.Select(metric => new TimeSeriesPoint { Value = (float)selector(metric) }).ToList();
        var dataView = mlContext.Data.LoadFromEnumerable(series);
        var pvalueHistoryLength = Math.Max(4, Math.Min(series.Count / 2, 30));

        var pipeline = mlContext.Transforms.DetectIidSpike(
            outputColumnName: nameof(SpikePrediction.Prediction),
            inputColumnName: nameof(TimeSeriesPoint.Value),
            confidence: ConfidenceLevel,
            pvalueHistoryLength: pvalueHistoryLength);

        ITransformer model;
        try
        {
            model = pipeline.Fit(dataView);
        }
        catch (Exception)
        {
            // Not enough variance/points for the model to fit reliably yet; skip this cycle.
            return;
        }

        var transformed = model.Transform(dataView);
        var predictions = mlContext.Data
            .CreateEnumerable<SpikePrediction>(transformed, reuseRowObject: false)
            .ToArray();

        if (predictions.Length == 0)
        {
            return;
        }

        var latestPrediction = predictions[^1];
        var isAlert = latestPrediction.Prediction.Length > 0 && latestPrediction.Prediction[0] == 1;
        if (!isAlert)
        {
            return;
        }

        var latestMetric = history[^1];
        var observedValue = selector(latestMetric);
        var rawScore = latestPrediction.Prediction.Length > 1 ? latestPrediction.Prediction[1] : observedValue;
        var pValue = latestPrediction.Prediction.Length > 2 ? latestPrediction.Prediction[2] : 0d;
        var severity = pValue < 0.01 ? "High" : "Medium";

        anomalies.Add(new DetectedAnomaly(
            Guid.NewGuid(),
            endpointId,
            type,
            severity,
            $"{label} flagged as a statistical spike by the ML.NET IID spike detection model.",
            latestMetric.Timestamp,
            Math.Round(observedValue, 2),
            Math.Round(rawScore, 2))
        {
            DetectionMethod = "MachineLearning"
        });
    }

    private sealed class TimeSeriesPoint
    {
        public float Value { get; set; }
    }

    private sealed class SpikePrediction
    {
        [VectorType(3)]
        public double[] Prediction { get; set; } = [];
    }
}
