using Microsoft.AspNetCore.Components;

namespace GeoSearch.Web.Client.UI.Layout;

public sealed partial class MainLayout
{
    private readonly CancellationTokenSource _lifetime = new();
    private bool _logoutBusy;
    private string? _logoutError;

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private async Task LogoutAsync()
    {
        if (_logoutBusy)
        {
            return;
        }

        _logoutError = null;
        _logoutBusy = true;
        try
        {
            await Authentication.LogoutAsync(_lifetime.Token);
            Navigation.NavigateTo("/login", replace: true);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (HttpRequestException)
        {
            // Keep the session active if the server did not confirm family revocation.
            _logoutError = "Could not revoke this session. Please try again.";
        }
        finally
        {
            _logoutBusy = false;
        }
    }
}
