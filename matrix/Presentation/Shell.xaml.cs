using matrix.Transitions.Matrix;
using Microsoft.Extensions.DependencyInjection;

namespace matrix.Presentation;

public sealed partial class Shell : UserControl, IContentControlProvider
{
    public Shell()
    {
        this.InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // App.Host is assigned only after NavigateAsync<Shell>() returns,
        // so we wait until the host is available before resolving services.
        var app = (App)Application.Current;
        while (app.Host == null)
        {
            await Task.Delay(50);
        }

        var transitionService = app.Host.Services.GetService<IMatrixTransitionService>();
        transitionService?.RegisterOverlay(MatrixOverlay, () => Splash.Content as FrameworkElement);
    }

    public ContentControl ContentControl => Splash;
}
