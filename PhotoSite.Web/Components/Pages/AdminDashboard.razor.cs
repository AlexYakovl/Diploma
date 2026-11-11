using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PhotoSite.Web.Components.Pages
{
    public partial class AdminDashboard
    {

    protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            var token = await JS.InvokeAsync<string>("localStorage.getItem", "jwt");

            if (string.IsNullOrEmpty(token))
            {
                Navigation.NavigateTo("/login");
            }
            StateHasChanged();
        }
    }
}
