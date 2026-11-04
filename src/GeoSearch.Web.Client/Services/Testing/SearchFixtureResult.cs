using GeoSearch.Web.Shared.GeoJson;

namespace GeoSearch.Web.Client.Services.Testing;

/// <summary>Observed browser-to-API response timings and results for one Lab 2 fixture.</summary>
public sealed record SearchFixtureResult(
    SearchFixture Fixture,
    int RequestedSamples,
    int Attempts,
    IReadOnlyList<double> LatenciesMs,
    GeoJsonProperties? TopResult,
    string? Snapshot,
    int EmptyResponses,
    int FailedRequests,
    string? LastError,
    bool TopResultVaried,
    bool SnapshotVaried)
{
    public int SuccessfulRequests => LatenciesMs.Count;

    public bool HasMixedMatches => EmptyResponses > 0 && EmptyResponses < SuccessfulRequests;

    public string StatusLabel => this switch
    {
        { SuccessfulRequests: 0 } => "Failed",
        { FailedRequests: > 0 } => "Partial",
        { HasMixedMatches: true } => "Mixed matches",
        { TopResultVaried: true } or { SnapshotVaried: true } => "Inconsistent",
        { EmptyResponses: > 0 } => "No matches",
        { WorstMs: >= 300 } => "E2E ≥ 300 ms",
        _ => "Measured",
    };

    public double? BestMs => SuccessfulRequests == 0 ? null : LatenciesMs.Min();

    public double? WorstMs => SuccessfulRequests == 0 ? null : LatenciesMs.Max();

    public double? AverageMs => SuccessfulRequests == 0 ? null : LatenciesMs.Average();

    public double? MedianMs
    {
        get
        {
            if (SuccessfulRequests == 0)
            {
                return null;
            }

            double[] sorted = [.. LatenciesMs.Order()];
            int middle = sorted.Length / 2;
            return sorted.Length % 2 == 0
                ? (sorted[middle - 1] + sorted[middle]) / 2
                : sorted[middle];
        }
    }
}
