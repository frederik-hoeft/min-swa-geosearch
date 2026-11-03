using GeoSearch.Web.Shared.Auth;

namespace GeoSearch.Web.Client.UI.Pages;

public sealed partial class Index
{
    private readonly CancellationTokenSource _lifetime = new();
    private string? _connectionError;

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    protected async override Task OnInitializedAsync()
    {
        try
        {
            string? token = await Authentication.GetAccessTokenAsync(_lifetime.Token);
            if (token is null)
            {
                Navigation.NavigateTo("/login", replace: true);
                return;
            }

            AccountResponse? account = await AuthApi.GetAccountAsync(token, _lifetime.Token);
            if (account is null)
            {
                string? refreshed = await Authentication.RefreshAfterUnauthorizedAsync(token, _lifetime.Token);
                if (refreshed is not null)
                {
                    account = await AuthApi.GetAccountAsync(refreshed, _lifetime.Token);
                }
            }

            if (account is null)
            {
                AuthenticationState.Logout();
                Navigation.NavigateTo("/login", replace: true);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (HttpRequestException)
        {
            // The server may be temporarily unreachable without revoking the session.
            _connectionError = "The API is currently unreachable. Account verification could not be completed.";
        }
    }
}
