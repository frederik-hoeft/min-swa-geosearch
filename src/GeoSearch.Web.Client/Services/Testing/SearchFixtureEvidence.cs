using System.Globalization;
using System.Text;

namespace GeoSearch.Web.Client.Services.Testing;

/// <summary>
/// Captures one completed (or canceled) fixture run, independently of subsequent UI selections.
/// </summary>
public sealed class SearchFixtureEvidence
{
    private readonly IReadOnlyDictionary<string, SearchFixtureResult> _results;

    public SearchFixtureEvidence(int requestedSamples, DateTimeOffset startedAt, DateTimeOffset completedAt,
        TimeSpan totalRuntime, bool wasCanceled, IEnumerable<SearchFixtureResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        RequestedSamples = requestedSamples;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        TotalRuntime = totalRuntime;
        WasCanceled = wasCanceled;
        _results = results.ToDictionary(result => result.Fixture.Query, StringComparer.Ordinal);
    }

    public int RequestedSamples { get; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset CompletedAt { get; }

    public TimeSpan TotalRuntime { get; }

    public bool WasCanceled { get; }

    public static string FormatMs(double? duration) => duration?.ToString("F1", CultureInfo.InvariantCulture) ?? "—";

    public static string FormatDuration(TimeSpan? elapsed) => elapsed switch
    {
        null => "—",
        { TotalMinutes: >= 1 } time => $"{(int)time.TotalMinutes}m {time.Seconds:D2}s",
        { } time => $"{time.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)} s",
    };

    public string ToMarkdown()
    {
        int attempts = _results.Values.Sum(result => result.Attempts);
        int successful = _results.Values.Sum(result => result.SuccessfulRequests);
        double? average = successful == 0
            ? null
            : _results.Values.Sum(result => result.LatenciesMs.Sum()) / successful;

        StringBuilder output = new();
        output.AppendLine("## Lab 2 search fixture observations");
        output.AppendLine();
        output.AppendLine(CultureInfo.InvariantCulture,
            $"Measured: {StartedAt:yyyy-MM-dd HH:mm:ss zzz} to {CompletedAt:yyyy-MM-dd HH:mm:ss zzz}; browser-to-API, sequential HTTP requests, {RequestedSamples} requested per fixture; no warm-up; run {(WasCanceled ? "canceled" : "finished")}.");
        output.AppendLine(CultureInfo.InvariantCulture,
            $"Snapshot: {SnapshotLabel()}; successful requests: {successful}/{attempts}; overall average: {FormatMs(average)} ms; total runtime: {FormatDuration(TotalRuntime)}.");
        output.AppendLine("Per-request timing: end-to-end browser-to-API HTTP (including browser, network, reverse proxy and TLS overhead where applicable), excluding JSON parsing and UI rendering. Total runtime: wall-clock duration for the full run, including processing between requests. Failed requests are excluded from per-request latency statistics. Not a raw backend benchmark.");
        output.AppendLine();
        output.AppendLine("| Query | Expected | Observed top result (name / type / ID) | Best (ms) | Average (ms) | Worst (ms) | Median (ms) | Requests | Snapshot | Notes |");
        output.AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: | --- | --- | --- |");

        foreach (SearchFixture fixture in SearchFixtureCatalog.All)
        {
            _results.TryGetValue(fixture.Query, out SearchFixtureResult? result);
            string top = result?.TopResult is { } feature
                ? $"{feature.Name} ({feature.Label}) / {feature.Type} / {feature.Id}"
                : result?.SuccessfulRequests > 0 ? "No matches" : "Not measured";
            string note = result is null ? "Pending" : result.StatusLabel;
            if (fixture.KnownRankingIssue)
            {
                note += "; lab-identified ranking issue";
            }

            if (result?.TopResultVaried == true)
            {
                note += "; top result varied between samples";
            }

            if (result?.SnapshotVaried == true)
            {
                note += "; snapshot varied between samples";
            }

            if (result?.EmptyResponses > 0)
            {
                note += $"; {result.EmptyResponses}/{result.SuccessfulRequests} successful responses returned no matches";
            }

            if (result?.LastError is { } error)
            {
                note += $"; {error}";
            }

            output.AppendLine(CultureInfo.InvariantCulture, $"| {EscapeCell(fixture.Query)} | {EscapeCell(fixture.Expected)} | {EscapeCell(top)} | "
                + $"{FormatMs(result?.BestMs)} | {FormatMs(result?.AverageMs)} | {FormatMs(result?.WorstMs)} | "
                + $"{FormatMs(result?.MedianMs)} | {result?.SuccessfulRequests ?? 0}/{result?.Attempts ?? 0} (target {result?.RequestedSamples ?? RequestedSamples}) | "
                + $"{EscapeCell(result?.Snapshot ?? "—")} | {EscapeCell(note)} |");
        }

        return output.ToString();
    }

    private string SnapshotLabel()
    {
        if (_results.Values.Any(result => result.SnapshotVaried))
        {
            return "Multiple snapshots";
        }

        string[] snapshots = [.. _results.Values.Select(result => result.Snapshot)
            .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot))
            .Select(snapshot => snapshot!)
            .Distinct(StringComparer.Ordinal)];
        return snapshots.Length switch
        {
            0 => "Not recorded",
            1 => snapshots[0],
            _ => "Multiple snapshots",
        };
    }

    private static string EscapeCell(string value) => value.Replace("|", "\\|", StringComparison.Ordinal)
        .Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}
