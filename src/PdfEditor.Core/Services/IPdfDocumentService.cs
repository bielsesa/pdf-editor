namespace PdfEditor.Core.Services;

public interface IPdfDocumentService
{
    int PageCount { get; }
    bool IsOpen { get; }
    string? CurrentFilePath { get; }

    Task OpenAsync(string filePath);
    Task<string> FlushToTempAsync();
    Task SaveAsync(string destinationPath);

    void DeletePages(IReadOnlyList<int> pageIndices);
    Task AddPagesFromFileAsync(string sourceFilePath);
    void MovePage(int fromIndex, int toIndex);

    void Close();
}
