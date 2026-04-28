namespace PdfEditor.Core.Models;

public sealed class PdfPageItem
{
    public int Index { get; init; }
    public int PageNumber => Index + 1;
}
