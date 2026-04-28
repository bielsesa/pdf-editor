using PdfEditor.Core.Services;
using PdfSharp.Pdf;

namespace PdfEditor.Tests.Services;

public sealed class PdfDocumentServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IPdfDocumentService _service;

    public PdfDocumentServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"PdfEditorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _service = new PdfDocumentService(new FakeFileSystemService(_tempDir));
    }

    public void Dispose()
    {
        _service.Close();
        Directory.Delete(_tempDir, recursive: true);
    }

    // ── Open ─────────────────────────────────────────────────

    [Fact]
    public async Task Open_ValidPdf_ReportsCorrectPageCount()
    {
        var path = CreateTestPdf(pageCount: 3);
        await _service.OpenAsync(path);
        Assert.Equal(3, _service.PageCount);
    }

    [Fact]
    public async Task Open_SetsIsOpen()
    {
        var path = CreateTestPdf(pageCount: 1);
        await _service.OpenAsync(path);
        Assert.True(_service.IsOpen);
    }

    // ── DeletePages ───────────────────────────────────────────

    [Fact]
    public async Task DeletePages_RemovesSinglePage()
    {
        var path = CreateTestPdf(pageCount: 3);
        await _service.OpenAsync(path);

        _service.DeletePages([1]);

        Assert.Equal(2, _service.PageCount);
    }

    [Fact]
    public async Task DeletePages_RemovesMultiplePages()
    {
        var path = CreateTestPdf(pageCount: 5);
        await _service.OpenAsync(path);

        _service.DeletePages([0, 2, 4]);

        Assert.Equal(2, _service.PageCount);
    }

    [Fact]
    public async Task DeletePages_OutOfRange_Throws()
    {
        var path = CreateTestPdf(pageCount: 2);
        await _service.OpenAsync(path);

        Assert.Throws<ArgumentOutOfRangeException>(() => _service.DeletePages([5]));
    }

    // ── AddPagesFromFile ──────────────────────────────────────

    [Fact]
    public async Task AddPagesFromFile_AppendsAllPages()
    {
        var path = CreateTestPdf(pageCount: 2);
        var sourcePath = CreateTestPdf(pageCount: 3);
        await _service.OpenAsync(path);

        await _service.AddPagesFromFileAsync(sourcePath);

        Assert.Equal(5, _service.PageCount);
    }

    // ── MovePage ──────────────────────────────────────────────

    [Fact]
    public async Task MovePage_ValidIndices_ChangesPageCount()
    {
        var path = CreateTestPdf(pageCount: 3);
        await _service.OpenAsync(path);

        _service.MovePage(0, 2);

        Assert.Equal(3, _service.PageCount);
    }

    [Fact]
    public async Task MovePage_SameIndex_DoesNotThrow()
    {
        var path = CreateTestPdf(pageCount: 3);
        await _service.OpenAsync(path);

        var ex = Record.Exception(() => _service.MovePage(1, 1));
        Assert.Null(ex);
    }

    [Fact]
    public async Task MovePage_OutOfRange_Throws()
    {
        var path = CreateTestPdf(pageCount: 2);
        await _service.OpenAsync(path);

        Assert.Throws<ArgumentOutOfRangeException>(() => _service.MovePage(0, 10));
    }

    // ── Save ─────────────────────────────────────────────────

    [Fact]
    public async Task Save_WritesValidPdfToDisk()
    {
        var sourcePath = CreateTestPdf(pageCount: 2);
        await _service.OpenAsync(sourcePath);

        var destPath = Path.Combine(_tempDir, "saved.pdf");
        await _service.SaveAsync(destPath);

        Assert.True(File.Exists(destPath));
        Assert.True(new FileInfo(destPath).Length > 0);
    }

    // ── Helpers ───────────────────────────────────────────────

    private string CreateTestPdf(int pageCount)
    {
        var doc = new PdfDocument();
        for (int i = 0; i < pageCount; i++)
            doc.AddPage();

        var path = Path.Combine(_tempDir, $"test_{Guid.NewGuid():N}.pdf");
        doc.Save(path);
        doc.Close();
        return path;
    }

    private sealed class FakeFileSystemService : IFileSystemService
    {
        public string CacheDirectory { get; }
        public FakeFileSystemService(string dir) => CacheDirectory = dir;
    }
}
