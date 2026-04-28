namespace PdfEditor;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        // InitializeComponent() must run before any page is created so that
        // App.xaml's merged resource dictionaries (Colors.xaml, Styles.xaml)
        // are available when pages inflate their XAML.
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new Window(_services.GetRequiredService<MainPage>()) { Title = "PDF Editor" };
}
