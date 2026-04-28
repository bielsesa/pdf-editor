using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace PdfEditor.Core.Services;

/// <summary>
/// Manages the current PDF document as a temp file on disk.
/// Each edit opens the file in Import mode, rebuilds a new PdfDocument,
/// and saves it once — avoiding PdfSharp's "save-once" restriction on
/// document instances and its Modify-mode incompatibilities with certain PDFs.
/// </summary>
public sealed class PdfDocumentService : IPdfDocumentService, IDisposable
{
    private readonly IFileSystemService _fileSystem;
    private string? _statePath;
    private int _pageCount;
    private bool _disposed;

    public int PageCount => _pageCount;
    public bool IsOpen => _statePath is not null;
    public string? CurrentFilePath { get; private set; }

    public PdfDocumentService(IFileSystemService fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public Task OpenAsync(string filePath)
    {
        CloseDocument();

        _statePath = TempPath();
        File.Copy(filePath, _statePath, overwrite: true);
        _pageCount = ReadPageCount(_statePath);
        CurrentFilePath = filePath;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns the path to the current state file.
    /// The file is always up to date — no save required.
    /// </summary>
    public Task<string> FlushToTempAsync()
    {
        EnsureOpen();
        return Task.FromResult(_statePath!);
    }

    public Task SaveAsync(string destinationPath)
    {
        EnsureOpen();
        File.Copy(_statePath!, destinationPath, overwrite: true);
        CurrentFilePath = destinationPath;
        return Task.CompletedTask;
    }

    public void DeletePages(IReadOnlyList<int> pageIndices)
    {
        EnsureOpen();

        foreach (var idx in pageIndices)
            if (idx < 0 || idx >= _pageCount)
                throw new ArgumentOutOfRangeException(nameof(pageIndices), $"Page index {idx} is out of range.");

        var toDelete = new HashSet<int>(pageIndices);

        RebuildState((newDoc, importDoc) =>
        {
            for (int i = 0; i < importDoc.PageCount; i++)
                if (!toDelete.Contains(i))
                    newDoc.AddPage(importDoc.Pages[i]);
        });

        _pageCount -= pageIndices.Count;
    }

    public Task AddPagesFromFileAsync(string sourceFilePath)
    {
        EnsureOpen();

        var newStatePath = TempPath();

        using var currentDoc = PdfReader.Open(_statePath!, PdfDocumentOpenMode.Import);
        using var sourceDoc = PdfReader.Open(sourceFilePath, PdfDocumentOpenMode.Import);

        int added = sourceDoc.PageCount;

        var newDoc = new PdfDocument();
        foreach (var page in currentDoc.Pages) newDoc.AddPage(page);
        foreach (var page in sourceDoc.Pages) newDoc.AddPage(page);

        newDoc.Save(newStatePath);
        newDoc.Close();

        ReplaceStatePath(newStatePath);
        _pageCount += added;

        return Task.CompletedTask;
    }

    public void MovePage(int fromIndex, int toIndex)
    {
        EnsureOpen();

        if (fromIndex < 0 || fromIndex >= _pageCount)
            throw new ArgumentOutOfRangeException(nameof(fromIndex));
        if (toIndex < 0 || toIndex >= _pageCount)
            throw new ArgumentOutOfRangeException(nameof(toIndex));
        if (fromIndex == toIndex)
            return;

        var indices = Enumerable.Range(0, _pageCount).ToList();
        var moving = indices[fromIndex];
        indices.RemoveAt(fromIndex);
        indices.Insert(toIndex, moving);

        RebuildState((newDoc, importDoc) =>
        {
            foreach (var idx in indices)
                newDoc.AddPage(importDoc.Pages[idx]);
        });
    }

    public void Close() => CloseDocument();

    // ── Helpers ───────────────────────────────────────────────

    /// <summary>
    /// Opens the current state in Import mode, builds a new document via
    /// <paramref name="build"/>, saves it, and replaces the state file.
    /// Each document instance is saved exactly once.
    /// </summary>
    private void RebuildState(Action<PdfDocument, PdfDocument> build)
    {
        var newStatePath = TempPath();

        using var importDoc = PdfReader.Open(_statePath!, PdfDocumentOpenMode.Import);
        var newDoc = new PdfDocument();
        build(newDoc, importDoc);
        newDoc.Save(newStatePath);
        newDoc.Close();

        ReplaceStatePath(newStatePath);
    }

    private void ReplaceStatePath(string newPath)
    {
        DeleteIfExists(_statePath);
        _statePath = newPath;
    }

    private static int ReadPageCount(string path)
    {
        using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        return doc.PageCount;
    }

    private void EnsureOpen()
    {
        if (_statePath is null)
            throw new InvalidOperationException("No document is open.");
    }

    private void CloseDocument()
    {
        DeleteIfExists(_statePath);
        _statePath = null;
        _pageCount = 0;
        CurrentFilePath = null;
    }

    private string TempPath() =>
        Path.Combine(_fileSystem.CacheDirectory, $"pdfstate_{Guid.NewGuid():N}.pdf");

    private static void DeleteIfExists(string? path)
    {
        if (path is not null && File.Exists(path))
            try { File.Delete(path); } catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        CloseDocument();
        _disposed = true;
    }
}
