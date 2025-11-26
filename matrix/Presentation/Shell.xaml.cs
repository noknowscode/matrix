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
        // Wait for Host to be available (it's set after NavigateAsync completes)
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
