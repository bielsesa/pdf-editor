using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PdfEditor.ViewModels;

public sealed partial class PageItemViewModel : ObservableObject
{
    [ObservableProperty]
    public partial ImageSource? Thumbnail { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public int Index { get; set; }
    public int PageNumber => Index + 1;

    public IAsyncRelayCommand DeleteCommand { get; }
    public IAsyncRelayCommand MoveUpCommand { get; }
    public IAsyncRelayCommand MoveDownCommand { get; }
    public IAsyncRelayCommand SelectCommand { get; }

    internal PageItemViewModel(
        int index,
        Func<PageItemViewModel, Task> onDelete,
        Func<PageItemViewModel, Task> onMoveUp,
        Func<PageItemViewModel, Task> onMoveDown,
        Func<PageItemViewModel, Task> onSelect)
    {
        Index = index;
        DeleteCommand = new AsyncRelayCommand(() => onDelete(this));
        MoveUpCommand = new AsyncRelayCommand(() => onMoveUp(this));
        MoveDownCommand = new AsyncRelayCommand(() => onMoveDown(this));
        SelectCommand = new AsyncRelayCommand(() => onSelect(this));
    }
}
