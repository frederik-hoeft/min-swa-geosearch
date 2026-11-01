using Microsoft.AspNetCore.Components;

namespace GeoSearch.Web.Client.UI.Components;

public sealed partial class RedirectToLogin
{
    protected override void OnInitialized()
    {
        string currentPath = new Uri(Navigation.Uri).AbsolutePath;
        if (!string.Equals(currentPath, "/login", StringComparison.OrdinalIgnoreCase))
        {
            Navigation.NavigateTo("/login", replace: true);
        }
    }
}
