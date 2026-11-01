using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using System.ComponentModel.DataAnnotations;

namespace GeoSearch.Web.Client.UI.Pages;

public sealed partial class Login
{
    private readonly CancellationTokenSource _lifetime = new();
    private static readonly EmailAddressAttribute s_emailAttribute = new();
    private MudForm? _form;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string? _error;
    private bool _busy;

    private static Func<string?, string?> EmailValidation => value => s_emailAttribute.IsValid(value) ? null : "Enter a valid email address.";

    protected async override Task OnInitializedAsync()
    {
        AuthenticationState state = await AuthenticationState.GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated == true)
        {
            Navigation.NavigateTo("/", replace: true);
        }
    }

    public void Dispose()
    {
        _password = string.Empty;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private async Task SignInAsync()
    {
        if (_busy || _form is null)
        {
            return;
        }

        _error = null;
        _busy = true;
        try
        {
            await _form.ValidateAsync();
            if (!_form.IsValid)
            {
                return;
            }

            bool success = await Authentication.LoginAsync(_email.Trim(), _password, _lifetime.Token);
            if (!success)
            {
                _error = "Sign-in failed. Check your credentials and try again.";
                return;
            }

            Navigation.NavigateTo("/", replace: true);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (HttpRequestException)
        {
            _error = "The authentication service is unavailable.";
        }
        finally
        {
            _password = string.Empty;
            _busy = false;
        }
    }
}
