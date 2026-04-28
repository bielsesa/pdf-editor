using Microsoft.Extensions.Logging;
using PdfEditor.Core.Services;
using PdfEditor.Infrastructure;
using PdfEditor.ViewModels;

namespace PdfEditor;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        var services = builder.Services;

        services.AddSingleton<IFileSystemService, MauiFileSystemService>();
        services.AddSingleton<IPdfDocumentService, PdfDocumentService>();

#if WINDOWS
        services.AddSingleton<IPdfPageRenderer, WindowsPdfRenderService>();
#elif ANDROID
        services.AddSingleton<IPdfPageRenderer, AndroidPdfRenderService>();
#endif

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
