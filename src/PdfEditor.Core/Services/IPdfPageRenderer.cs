namespace PdfEditor.Core.Services;

public interface IPdfPageRenderer
{
    /// <summary>
    /// Renders a single PDF page to a PNG byte array.
    /// </summary>
    Task<byte[]> RenderPageAsync(string filePath, int pageIndex, int targetWidth);
}
