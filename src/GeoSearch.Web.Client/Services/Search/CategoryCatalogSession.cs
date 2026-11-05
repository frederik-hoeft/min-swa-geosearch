using System.Globalization;
using System.Text;

namespace GeoSearch.Web.Client.Services.Search;

/// <summary>Loads the backend category vocabulary once; matching is purely local.</summary>
internal sealed class CategoryCatalogSession(SearchApiClient api) : IDisposable
{
    private CancellationTokenSource _lifetime = new();
    private Task? _request;
    public IReadOnlyList<string> Categories { get; private set; } = [];
    private bool _disposed;

    public bool Loading => _request is { IsCompleted: false };
    public string? Error { get; private set; }
    public bool HasLoaded { get; private set; }

    public Task EnsureLoadedAsync(Func<Task> onUpdated)
    {
        if (_disposed || HasLoaded)
        {
            return Task.CompletedTask;
        }

        return _request is { IsCompleted: false } request ? request : _request = LoadAsync(onUpdated);
    }

    public Task RetryAsync(Func<Task> onUpdated)
    {
        if (_disposed)
        {
            return Task.CompletedTask;
        }

        Error = null;
        return EnsureLoadedAsync(onUpdated);
    }

    public IReadOnlyList<string> Match(string query)
    {
        string needle = Normalize(query.Trim());
        if (needle.Length == 0)
        {
            return [];
        }

        return Categories.Where(category => Normalize(category).Contains(needle, StringComparison.Ordinal))
            .Take(5).ToArray();
    }

    private async Task LoadAsync(Func<Task> onUpdated)
    {
        try
        {
            IReadOnlyList<string>? categories = await api.GetCategoriesAsync(_lifetime.Token);
            if (!_disposed)
            {
                Categories = (categories ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToArray();
                Error = null;
                HasLoaded = true;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or System.Text.Json.JsonException or NotSupportedException)
        {
            if (!_disposed)
            {
                Error = "Could not load nearby categories. You can still explore all categories.";
            }
        }
        catch (OperationCanceledException) when (_disposed)
        {
            return;
        }

        if (!_disposed)
        {
            await onUpdated();
        }
    }

    private static string Normalize(string text)
    {
        string decomposed = text.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new(decomposed.Length);
        foreach (char character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetime.Cancel();
        // A pending request retains its own token until the async method exits.
        _lifetime.Dispose();
    }
}
