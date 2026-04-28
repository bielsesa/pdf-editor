using PdfEditor.Core.Services;

namespace PdfEditor.Infrastructure;

internal sealed class MauiFileSystemService : IFileSystemService
{
    public string CacheDirectory => FileSystem.Current.CacheDirectory;
}
