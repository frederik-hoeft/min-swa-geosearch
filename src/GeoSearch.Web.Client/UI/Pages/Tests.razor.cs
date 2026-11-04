using GeoSearch.Web.Client.Services.Testing;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using System.Globalization;
using System.Text;

namespace GeoSearch.Web.Client.UI.Pages;

/// <summary>Renders instructor-provided fixture observations without coupling to the map workspace.</summary>
public sealed partial class Tests : ComponentBase, IDisposable
{
    private readonly Dictionary<string, SearchFixtureResult> _results = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _explanations = new(StringComparer.Ordinal);
    private CancellationTokenSource? _runCancellation;
    private string? ActiveQuery { get; set; }
    private string? CopyError { get; set; }
    private bool IsRunning { get; set; }
    private bool WasCanceled { get; set; }
    private int Repetitions { get; set; } = 5;

    [Inject]
    private SearchFixtureBenchmarkService Benchmark { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    private int AttemptedRequests => _results.Values.Sum(result => result.Attempts);

    private int SuccessfulRequests => _results.Values.Sum(result => result.SuccessfulRequests);

    private double? OverallAverageMs => SuccessfulRequests == 0
        ? null
        : _results.Values.Sum(result => result.LatenciesMs.Sum()) / SuccessfulRequests;

    private string SnapshotLabel
    {
        get
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
    }

    private double ProgressPercent => 100.0 * _results.Count / SearchFixtureCatalog.All.Count;

    private string StatusText => IsRunning
        ? $"Running {_results.Count + 1} of {SearchFixtureCatalog.All.Count}: {ActiveQuery}"
        : WasCanceled
            ? $"Canceled after {_results.Count} fixtures. Completed measurements are retained."
            : _results.Count == 0
                ? "No measurements yet. Run all fixtures with the API available."
                : $"Last run completed {_results.Count} of {SearchFixtureCatalog.All.Count} fixtures.";

    private SearchFixtureResult? GetResult(SearchFixture fixture) => _results.GetValueOrDefault(fixture.Query);

    private string GetExplanation(SearchFixture fixture) => _explanations.GetValueOrDefault(fixture.Query, string.Empty);

    private void SetExplanation(SearchFixture fixture, string explanation) => _explanations[fixture.Query] = explanation;

    private static string FormatMs(double? duration) => duration?.ToString("F1", CultureInfo.InvariantCulture) ?? "—";

    private static Color StatusColor(SearchFixtureResult result) => result switch
    {
        { SuccessfulRequests: 0 } => Color.Error,
        { FailedRequests: > 0 } => Color.Warning,
        { TopResultVaried: true } or { SnapshotVaried: true } => Color.Warning,
        { EmptyResponses: > 0 } => Color.Info,
        { WorstMs: >= 300 } => Color.Warning,
        _ => Color.Success,
    };

    private static string StatusLabel(SearchFixtureResult result) => result switch
    {
        { SuccessfulRequests: 0 } => "Failed",
        { FailedRequests: > 0 } => "Partial",
        { TopResultVaried: true } or { SnapshotVaried: true } => "Inconsistent",
        { EmptyResponses: > 0 } => "No/variable matches",
        { WorstMs: >= 300 } => "Above target",
        _ => "Measured",
    };

    private async Task RunAllAsync()
    {
        if (IsRunning)
        {
            return;
        }

        _results.Clear();
        CopyError = null;
        WasCanceled = false;
        IsRunning = true;
        _runCancellation = new CancellationTokenSource();

        try
        {
            foreach (SearchFixture fixture in SearchFixtureCatalog.All)
            {
                ActiveQuery = fixture.Query;
                await InvokeAsync(StateHasChanged);
                SearchFixtureResult result = await Benchmark.RunAsync(fixture, Repetitions, _runCancellation.Token);
                _results[fixture.Query] = result;
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException) when (_runCancellation.IsCancellationRequested)
        {
            WasCanceled = true;
        }
        finally
        {
            ActiveQuery = null;
            IsRunning = false;
            _runCancellation.Dispose();
            _runCancellation = null;
        }
    }

    private void CancelRun() => _runCancellation?.Cancel();

    private async Task CopyEvidenceAsync()
    {
        CopyError = null;
        try
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", BuildEvidence());
            Snackbar.Add("Test evidence copied to clipboard.", Severity.Success);
        }
        catch (JSException)
        {
            CopyError = "Clipboard access was denied. Use HTTPS or localhost, then allow clipboard access and retry.";
        }
    }

    private string BuildEvidence()
    {
        StringBuilder output = new();
        output.AppendLine("## Lab 2 search fixture observations");
        output.AppendLine();
        output.AppendLine(CultureInfo.InvariantCulture, $"Measured: {DateTimeOffset.Now:yyyy-MM-dd HH:mm zzz}; browser-to-API, sequential HTTP requests, {Repetitions} requested per fixture; no warm-up.");
        output.AppendLine(CultureInfo.InvariantCulture, $"Snapshot: {SnapshotLabel}; successful requests: {SuccessfulRequests}/{AttemptedRequests}; overall average: {FormatMs(OverallAverageMs)} ms.");
        output.AppendLine("Timing: .NET Stopwatch around full HTTP response (includes network/download, excludes JSON parsing). Failed requests are excluded from timing statistics.");
        output.AppendLine();
        output.AppendLine("| Query | Expected | Observed top result (name / type / ID) | Best (ms) | Average (ms) | Worst (ms) | Median (ms) | Requests | Snapshot | Notes |");
        output.AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: | --- | --- | --- |");

        foreach (SearchFixture fixture in SearchFixtureCatalog.All)
        {
            SearchFixtureResult? result = GetResult(fixture);
            string top = result?.TopResult is { } feature
                ? $"{feature.Name} ({feature.Label}) / {feature.Type} / {feature.Id}"
                : result?.SuccessfulRequests > 0 ? "No matches" : "Not measured";
            string note = result is null ? "Pending" : StatusLabel(result);
            if (fixture.KnownRankingIssue)
            {
                string explanation = GetExplanation(fixture);
                note += string.IsNullOrWhiteSpace(explanation)
                    ? "; lab-identified ranking issue; explanation not yet recorded"
                    : $"; explanation: {explanation}";
            }

            if (result?.TopResultVaried == true)
            {
                note += "; top result varied between samples";
            }

            if (result?.SnapshotVaried == true)
            {
                note += "; snapshot varied between samples";
            }

            if (result?.LastError is { } error)
            {
                note += $"; {error}";
            }

            output.AppendLine(CultureInfo.InvariantCulture, $"| {EscapeCell(fixture.Query)} | {EscapeCell(fixture.Expected)} | {EscapeCell(top)} | "
                + $"{FormatMs(result?.BestMs)} | {FormatMs(result?.AverageMs)} | {FormatMs(result?.WorstMs)} | "
                + $"{FormatMs(result?.MedianMs)} | {result?.SuccessfulRequests ?? 0}/{result?.Attempts ?? 0} (target {result?.RequestedSamples ?? Repetitions}) | "
                + $"{EscapeCell(result?.Snapshot ?? "—")} | {EscapeCell(note)} |");
        }

        return output.ToString();
    }

    private static string EscapeCell(string value) => value.Replace("|", "\\|", StringComparison.Ordinal)
        .Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

    public void Dispose()
    {
        _runCancellation?.Cancel();
        _runCancellation?.Dispose();
        GC.SuppressFinalize(this);
    }
}
