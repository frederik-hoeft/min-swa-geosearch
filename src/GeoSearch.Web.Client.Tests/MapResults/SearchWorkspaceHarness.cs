using GeoSearch.Web.Client.UI.Components.Search;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace GeoSearch.Web.Client.Tests.MapResults;

public sealed class SearchWorkspaceHarness : ComponentBase
{
    [Inject]
    private SelectionProbe Probe { get; set; } = null!;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<SearchWorkspace>(0);
        builder.AddComponentReferenceCapture(1, component => Probe.Workspace = (SearchWorkspace)component);
        builder.CloseComponent();
    }
}
