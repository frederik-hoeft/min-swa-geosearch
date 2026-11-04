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
