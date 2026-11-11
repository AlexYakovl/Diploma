using Microsoft.AspNetCore.Components;
using PhotoSite.Web.Components.Services;

namespace PhotoSite.Web.Components.Pages
{
    public partial class Portfolio : ComponentBase
    {
        private List<CategoryListItemDto> _categories = new();
        private bool _loading = true;

        protected override async Task OnInitializedAsync()
        {
            _categories = await Api.GetCategoriesAsync();
            _loading = false;
        }

        private void GoToCategory(int id) => Nav.NavigateTo("/portfolio/category/" + id);
    }
}

