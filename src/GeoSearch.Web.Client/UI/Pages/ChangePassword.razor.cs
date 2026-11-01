using MudBlazor;

namespace GeoSearch.Web.Client.UI.Pages;

public sealed partial class ChangePassword
{
    private readonly CancellationTokenSource _lifetime = new();
    private MudForm? _form;
    private string _currentPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string? _error;
    private bool _busy;

    public void Dispose()
    {
        ClearPasswords();
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private async Task ChangeAsync()
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

            bool changed = await Authentication.ChangePasswordAsync(_currentPassword, _newPassword, _lifetime.Token);
            if (changed)
            {
                Navigation.NavigateTo("/", replace: true);
            }
            else
            {
                _error = "Password change rejected. Check your current password and password requirements.";
            }
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
            ClearPasswords();
            _busy = false;
        }
    }

    private void ClearPasswords()
    {
        _currentPassword = string.Empty;
        _newPassword = string.Empty;
    }
}
