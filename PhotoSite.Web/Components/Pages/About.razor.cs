using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using PhotoSite.Web.Components.Services;

namespace PhotoSite.Web.Components.Pages
{
    public partial class About
    {
        [Inject] private ApiService Api { get; set; } = default!;
        [Inject] private AuthStateService Auth { get; set; } = default!;
        [Inject] private ISnackbar Snackbar { get; set; } = default!;

        private bool _isAdmin;
        private List<AboutPhotoListItem> _photos = new();
        private bool _aboutSliderUploading;
        private int? _aboutDeletingPhotoId;
        private ElementReference _aboutSliderInputRef;

        private DotNetObjectReference<About>? _aboutSliderProgressRef;
        private double _aboutSliderUploadPct;
        private string _aboutSliderUploadLabel = "";

        protected bool AboutSliderBusy =>
            _aboutSliderUploading || _aboutDeletingPhotoId.HasValue;
        private string _sliderKey = "0";
        private string _uikitKey = "";

        private record StepItem(string Title, string Text);
        private record FaqItem(string Q, string A);

        private readonly List<string> _youGet = new()
        {
            "Короткий видео-бэкстейдж с телефона или камеры",
            "Ссылка на галерею для скачивания",
            "Лёгкая ретушь крупных портретов",
            "От 50 кадров за час съёмки",
        };

        private readonly List<StepItem> _bookingSteps = new()
        {
            new("1) Обсуждаем идею", "Концепция, локация и одежда — я помогу с рекомендациями по образам и цветам."),
            new("2) Фиксируем дату", "Бронирование по предоплате 1500 ₽, после этого я держу дату за вами."),
            new("3) Снимаем", "На съёмке помогу с позированием и поддержу, чтобы вам было комфортно."),
        };

        private readonly List<FaqItem> _faqs = new()
        {
            new("Как забронировать съёмку?",
                "Напишите мне — обсудим идею, место и удобную дату. Бронирование фиксируется предоплатой (1500 ₽)."),
            new("Где проходит съёмка?",
                "Уличная локация, фотостудия или ваша квартира — подберём лучший вариант под формат, концепцию и количество людей."),
            new("Кто бронирует студию?",
                "По желанию: студию могу забронировать я или вы. Аренда студии оплачивается отдельно и не входит в стоимость моей работы."),
            new("Поможешь с позированием?",
                "Да. Я подскажу, как встать/куда смотреть/что делать руками — и мы спокойно придём к результату, который вам понравится."),
            new("Будет ли ретушь?",
                "Я делаю лёгкую ретушь крупных портретов и сохраняю естественность — без пластики «до неузнаваемости»."),
        };

        private string FullPhotoUrl(AboutPhotoListItem p) => Api.GetAboutPhotoFileUrl(p.Id);

        protected override async Task OnInitializedAsync()
        {
            // Только HttpClient (без JS) — безопасно при статическом prerendering.
            await LoadPhotosAsync();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                // localStorage + JWT — только через JS, после гидрации.
                _isAdmin = await Auth.IsAdminAsync(JS);
                StateHasChanged();
            }

            if (_photos.Count == 0)
            {
                _uikitKey = "";
                return;
            }
            if (string.Equals(_uikitKey, _sliderKey, StringComparison.Ordinal))
                return;
            _uikitKey = _sliderKey;
            try
            {
                await JS.InvokeVoidAsync("photosite.uikitUpdate");
            }
            catch
            {
                // ignore (navigation)
            }
        }

        private async Task LoadPhotosAsync()
        {
            _photos = await Api.GetAboutPhotosAsync();
            _sliderKey = string.Join("-", _photos.Select(x => x.Id)) + "-" + _photos.Count;
        }

        private async Task PickAboutSliderFilesAsync()
        {
            if (!_isAdmin || AboutSliderBusy)
                return;
            await JS.InvokeVoidAsync("photosite.clickById", "aboutSliderUpload");
        }

        [JSInvokable]
        public void OnAboutSliderBatchProgress(long loaded, long total, int fileIndex, int fileCount, string fileName)
        {
            var n = Math.Max(1, fileCount);
            var slice = 100.0 / n;
            double withinFile;
            if (total > 0)
                withinFile = (double)loaded / total;
            else
                withinFile = loaded > 0 ? 0.35 : 0;

            _aboutSliderUploadPct = Math.Clamp(fileIndex * slice + withinFile * slice, 0, 100);
            _aboutSliderUploadLabel = $"Слайдер: файл {fileIndex + 1}/{fileCount} · {_aboutSliderUploadPct:0}%";
            _ = InvokeAsync(StateHasChanged);
        }

        private async Task AboutSliderNativeChanged()
        {
            if (!_isAdmin || AboutSliderBusy)
                return;

            _aboutSliderUploading = true;
            _aboutSliderUploadPct = 0;
            _aboutSliderUploadLabel = "Подготовка…";
            await InvokeAsync(StateHasChanged);

            _aboutSliderProgressRef = DotNetObjectReference.Create(this);
            try
            {
                var token = await JS.InvokeAsync<string?>("localStorage.getItem", "jwt") ?? "";
                var batch = await Api.UploadAboutSliderBatchFromBrowserAsync(
                    JS,
                    _aboutSliderInputRef,
                    token,
                    _aboutSliderProgressRef,
                    nameof(OnAboutSliderBatchProgress));

                if (batch.Total == 0)
                    return;

                if (batch.Ok)
                    Snackbar.Add(batch.Total == 1 ? "Фотография добавлена в слайдер." : $"Добавлено фотографий: {batch.Uploaded}.", Severity.Success);
                else if (batch.Uploaded > 0)
                    Snackbar.Add($"Загружено {batch.Uploaded} из {batch.Total}.", Severity.Info);
                else
                    Snackbar.Add("Не удалось загрузить фотографии.", Severity.Error);

                await LoadPhotosAsync();
                _uikitKey = "";
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Ошибка: {ex.Message}", Severity.Error);
            }
            finally
            {
                _aboutSliderProgressRef?.Dispose();
                _aboutSliderProgressRef = null;
                _aboutSliderUploadPct = 0;
                _aboutSliderUploadLabel = "";
                try
                {
                    await Api.ClearBrowserFileInputAsync(JS, _aboutSliderInputRef);
                }
                catch
                {
                    /* ignore */
                }

                _aboutSliderUploading = false;
                StateHasChanged();
            }
        }

        private async Task DeleteAboutPhoto(AboutPhotoListItem item)
        {
            if (!_isAdmin || AboutSliderBusy)
                return;

            _aboutDeletingPhotoId = item.Id;
            try
            {
                if (await Api.DeleteAboutSliderPhotoAsync(item.Id, JS))
                {
                    Snackbar.Add("Фотография удалена.", Severity.Info);
                    await LoadPhotosAsync();
                    _uikitKey = "";
                }
                else
                    Snackbar.Add("Не удалось удалить.", Severity.Error);
            }
            finally
            {
                _aboutDeletingPhotoId = null;
                StateHasChanged();
            }
        }
    }
}
