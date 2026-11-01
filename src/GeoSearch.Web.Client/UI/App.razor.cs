using GeoSearch.Web.Client.Auth;
using Microsoft.AspNetCore.Components;

namespace GeoSearch.Web.Client.UI;

public sealed partial class App
{
    private bool _initialized;

    protected async override Task OnInitializedAsync()
    {
        try
        {
            await Authentication.InitializeAsync();
        }
        catch (HttpRequestException)
        {
            // Transient API outages must not prevent access to the login page.
        }
        finally
        {
            _initialized = true;
        }
    }
}
