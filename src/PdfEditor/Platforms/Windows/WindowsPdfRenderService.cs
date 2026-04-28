using PdfEditor.Core.Services;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace PdfEditor;

internal sealed class WindowsPdfRenderService : IPdfPageRenderer
{
    public async Task<byte[]> RenderPageAsync(string filePath, int pageIndex, int targetWidth)
    {
        var storageFile = await StorageFile.GetFileFromPathAsync(filePath);
        var pdfDoc = await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(storageFile);

        using var page = pdfDoc.GetPage((uint)pageIndex);
        double scale = targetWidth / page.Size.Width;
        var targetHeight = (uint)(page.Size.Height * scale);

        using var renderStream = new InMemoryRandomAccessStream();
        await page.RenderToStreamAsync(renderStream, new Windows.Data.Pdf.PdfPageRenderOptions
        {
            DestinationWidth = (uint)targetWidth,
            DestinationHeight = targetHeight
        });

        // Re-encode as PNG so MAUI's ImageSource can consume it on any thread.
        renderStream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(renderStream);
        var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

        using var pngStream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, pngStream);
        encoder.SetSoftwareBitmap(softwareBitmap);
        await encoder.FlushAsync();

        pngStream.Seek(0);
        var reader = new DataReader(pngStream);
        await reader.LoadAsync((uint)pngStream.Size);
        var bytes = new byte[pngStream.Size];
        reader.ReadBytes(bytes);
        return bytes;
    }
}
