using Android.Graphics;
using Android.Graphics.Pdf;
using Android.OS;
using PdfEditor.Core.Services;

namespace PdfEditor;

internal sealed class AndroidPdfRenderService : IPdfPageRenderer
{
    public Task<byte[]> RenderPageAsync(string filePath, int pageIndex, int targetWidth) =>
        Task.Run(() =>
        {
            var file = new Java.IO.File(filePath);
            using var pfd = ParcelFileDescriptor.Open(file, ParcelFileMode.ReadOnly)
                ?? throw new InvalidOperationException($"Cannot open file: {filePath}");

            using var renderer = new PdfRenderer(pfd);
            using var page = renderer.OpenPage(pageIndex);

            int targetHeight = (int)((long)targetWidth * page.Height / page.Width);

            using var bitmap = Bitmap.CreateBitmap(targetWidth, targetHeight, Bitmap.Config.Argb8888!)
                ?? throw new InvalidOperationException("Failed to create bitmap.");

            bitmap.EraseColor(Android.Graphics.Color.White);
            page.Render(bitmap, null, null, PdfRenderMode.ForDisplay);

            using var ms = new MemoryStream();
            bitmap.Compress(Bitmap.CompressFormat.Png!, 100, ms);
            return ms.ToArray();
        });
}
