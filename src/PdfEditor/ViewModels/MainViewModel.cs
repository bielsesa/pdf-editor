using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditor.Core.Services;

namespace PdfEditor.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private const int ThumbnailWidth = 150;
    private const int ViewerWidth = 1200;

    private readonly IPdfDocumentService _documentService;
    private readonly IPdfPageRenderer _renderService;
    private int _selectedIndex = -1;
    private string? _currentTempPath;

    public ObservableCollection<PageItemViewModel> Pages { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(
        nameof(SaveCommand),
        nameof(SaveAsCommand),
        nameof(AddPagesCommand))]
    public partial bool IsDocumentOpen { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial ImageSource? CurrentPageImage { get; set; }

    [ObservableProperty]
    public partial string CurrentPageInfo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DocumentTitle { get; set; } = "PDF Editor";

    [ObservableProperty]
    public partial bool IsPanelVisible { get; set; } = true;

    public MainViewModel(IPdfDocumentService documentService, IPdfPageRenderer renderService)
    {
        _documentService = documentService;
        _renderService = renderService;
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select a PDF file",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, [".pdf"] },
                { DevicePlatform.Android, ["application/pdf"] }
            })
        });

        if (result is null) return;

        IsBusy = true;
        try
        {
            await _documentService.OpenAsync(result.FullPath);
            DocumentTitle = Path.GetFileName(result.FullPath);
            IsDocumentOpen = true;
            _selectedIndex = 0;
            await RefreshAllAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Could not open file", ex.Message);
            IsDocumentOpen = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(IsDocumentOpen))]
    private async Task SaveAsync()
    {
        if (_documentService.CurrentFilePath is null) return;
        await SaveToPathAsync(_documentService.CurrentFilePath);
    }

    [RelayCommand(CanExecute = nameof(IsDocumentOpen))]
    private async Task SaveAsAsync()
    {
        var path = await PickSavePathAsync();
        if (path is null) return;
        await SaveToPathAsync(path);
    }

    [RelayCommand(CanExecute = nameof(IsDocumentOpen))]
    private async Task AddPagesAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select a PDF file to append",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, [".pdf"] },
                { DevicePlatform.Android, ["application/pdf"] }
            })
        });

        if (result is null) return;

        IsBusy = true;
        try
        {
            await _documentService.AddPagesFromFileAsync(result.FullPath);
            await RefreshAllAsync(keepSelection: true);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Could not add pages", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void TogglePanel() => IsPanelVisible = !IsPanelVisible;

    // ── Page-level operations ─────────────────────────────────

    private async Task DeletePageAsync(PageItemViewModel page)
    {
        IsBusy = true;
        try
        {
            _documentService.DeletePages([page.Index]);

            if (_selectedIndex >= _documentService.PageCount)
                _selectedIndex = _documentService.PageCount - 1;

            await RefreshAllAsync(keepSelection: true);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Could not delete page", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task MovePageUpAsync(PageItemViewModel page)
    {
        if (page.Index == 0) return;
        await MovePageAsync(page.Index, page.Index - 1);
    }

    private async Task MovePageDownAsync(PageItemViewModel page)
    {
        if (page.Index >= _documentService.PageCount - 1) return;
        await MovePageAsync(page.Index, page.Index + 1);
    }

    private async Task MovePageAsync(int from, int to)
    {
        IsBusy = true;
        try
        {
            _documentService.MovePage(from, to);

            if (_selectedIndex == from)
                _selectedIndex = to;

            await RefreshAllAsync(keepSelection: true);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Could not move page", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SelectPageAsync(PageItemViewModel page)
    {
        if (_currentTempPath is null) return;
        _selectedIndex = page.Index;

        foreach (var p in Pages)
            p.IsSelected = false;
        page.IsSelected = true;

        UpdatePageInfo();
        await RenderMainViewerAsync(page.Index);
    }

    // ── Refresh helpers ───────────────────────────────────────

    private async Task RefreshAllAsync(bool keepSelection = false)
    {
        _currentTempPath = await _documentService.FlushToTempAsync();
        var count = _documentService.PageCount;

        if (!keepSelection || _selectedIndex < 0)
            _selectedIndex = 0;
        _selectedIndex = Math.Clamp(_selectedIndex, 0, Math.Max(count - 1, 0));

        Pages.Clear();
        for (int i = 0; i < count; i++)
        {
            var vm = CreatePageItemViewModel(i);
            vm.IsSelected = i == _selectedIndex;
            Pages.Add(vm);
        }

        UpdatePageInfo();
        await RenderThumbnailsAsync();

        if (count > 0)
            await RenderMainViewerAsync(_selectedIndex);
        else
            CurrentPageImage = null;
    }

    private async Task RenderThumbnailsAsync()
    {
        if (_currentTempPath is null) return;
        var tempPath = _currentTempPath;

        for (int i = 0; i < Pages.Count; i++)
        {
            var vm = Pages[i];
            try
            {
                var bytes = await _renderService.RenderPageAsync(tempPath, i, ThumbnailWidth);
                vm.Thumbnail = ImageSource.FromStream(() => new MemoryStream(bytes));
            }
            catch
            {
                // Leave thumbnail null; the page card is still functional.
            }
        }
    }

    private async Task RenderMainViewerAsync(int pageIndex)
    {
        if (_currentTempPath is null || pageIndex < 0) return;
        try
        {
            var bytes = await _renderService.RenderPageAsync(_currentTempPath, pageIndex, ViewerWidth);
            CurrentPageImage = ImageSource.FromStream(() => new MemoryStream(bytes));
        }
        catch
        {
            CurrentPageImage = null;
        }
    }

    private PageItemViewModel CreatePageItemViewModel(int index) =>
        new(index,
            onDelete: DeletePageAsync,
            onMoveUp: MovePageUpAsync,
            onMoveDown: MovePageDownAsync,
            onSelect: SelectPageAsync);

    private void UpdatePageInfo()
    {
        var count = _documentService.PageCount;
        CurrentPageInfo = count > 0
            ? $"Page {_selectedIndex + 1} / {count}"
            : string.Empty;
    }

    // ── File save helpers ─────────────────────────────────────

    private async Task SaveToPathAsync(string path)
    {
        IsBusy = true;
        try
        {
            await _documentService.SaveAsync(path);
            DocumentTitle = Path.GetFileName(path);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Could not save file", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static async Task<string?> PickSavePathAsync()
    {
#if WINDOWS
        var picker = new Windows.Storage.Pickers.FileSavePicker();
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("PDF Document", [".pdf"]);
        picker.SuggestedFileName = "document";

        var hwnd = ((MauiWinUIWindow)Application.Current!.Windows[0].Handler!.PlatformView!).WindowHandle;
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
#else
        // Android: write to app data directory; user can share/export from there.
        var dir = FileSystem.Current.AppDataDirectory;
        return Path.Combine(dir, $"document_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
#endif
    }

    private static async Task ShowErrorAsync(string title, string message)
    {
        if (Application.Current?.Windows.Count > 0)
            await Application.Current.Windows[0].Page!.DisplayAlertAsync(title, message, "OK");
    }
}
