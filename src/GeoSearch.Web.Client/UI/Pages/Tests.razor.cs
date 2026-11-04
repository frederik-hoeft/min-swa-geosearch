using GeoSearch.Web.Client.Services.Testing;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using System.Diagnostics;

namespace GeoSearch.Web.Client.UI.Pages;

/// <summary>Renders instructor-provided fixture observations without coupling to the map workspace.</summary>
public sealed partial class Tests : ComponentBase, IDisposable
{
    private readonly Dictionary<string, SearchFixtureResult> _results = new(StringComparer.Ordinal);
    private CancellationTokenSource? _runCancellation;
    private SearchFixtureEvidence? _lastRun;
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

    private static string FormatMs(double? duration) => SearchFixtureEvidence.FormatMs(duration);

    private static string FormatDuration(TimeSpan? elapsed) => SearchFixtureEvidence.FormatDuration(elapsed);

    private static Color StatusColor(SearchFixtureResult result) => result switch
    {
        { SuccessfulRequests: 0 } => Color.Error,
        { FailedRequests: > 0 } => Color.Warning,
        { HasMixedMatches: true } => Color.Warning,
        { TopResultVaried: true } or { SnapshotVaried: true } => Color.Warning,
        { EmptyResponses: > 0 } => Color.Info,
        { WorstMs: >= 300 } => Color.Warning,
        _ => Color.Success,
    };

    private async Task RunAllAsync()
    {
        if (IsRunning)
        {
            return;
        }

        _results.Clear();
        _lastRun = null;
        CopyError = null;
        WasCanceled = false;
        IsRunning = true;
        _runCancellation = new CancellationTokenSource();
        int requestedSamples = Repetitions;
        DateTimeOffset startedAt = DateTimeOffset.Now;
        Stopwatch runStopwatch = Stopwatch.StartNew();

        try
        {
            foreach (SearchFixture fixture in SearchFixtureCatalog.All)
            {
                ActiveQuery = fixture.Query;
                await InvokeAsync(StateHasChanged);
                SearchFixtureResult result = await Benchmark.RunAsync(fixture, requestedSamples, _runCancellation.Token);
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
            runStopwatch.Stop();
            _lastRun = new SearchFixtureEvidence(requestedSamples, startedAt, DateTimeOffset.Now,
                runStopwatch.Elapsed, WasCanceled, _results.Values);
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
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", _lastRun?.ToMarkdown() ?? string.Empty);
            Snackbar.Add("Test evidence copied to clipboard.", Severity.Success);
        }
        catch (JSException)
        {
            CopyError = "Clipboard access was denied. Use HTTPS or localhost, then allow clipboard access and retry.";
        }
    }

    public void Dispose()
    {
        _runCancellation?.Cancel();
        _runCancellation?.Dispose();
        GC.SuppressFinalize(this);
    }
}
