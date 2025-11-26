using matrix.Transitions.Matrix;

namespace matrix.Presentation;

public partial class MainViewModel : ObservableObject
{
    private readonly INavigator _navigator;
    private readonly IMatrixTransitionService _matrixTransition;

    [ObservableProperty]
    private string? name;

    public MainViewModel(
        IStringLocalizer localizer,
        IOptions<AppConfig> appInfo,
        INavigator navigator,
        IMatrixTransitionService matrixTransition)
    {
        _navigator = navigator;
        _matrixTransition = matrixTransition;
        Title = "Main";
        Title += $" - {localizer["ApplicationName"]}";
        Title += $" - {appInfo?.Value?.Environment}";
        GoToSecond = new AsyncRelayCommand(GoToSecondView);
        GoToSecondSlow = new AsyncRelayCommand(GoToSecondViewSlow);
        LoopMatrix = new AsyncRelayCommand(LoopMatrixTransition);
    }

    public string? Title { get; }

    public ICommand GoToSecond { get; }
    public ICommand GoToSecondSlow { get; }
    public ICommand LoopMatrix { get; }

    private async Task GoToSecondView()
    {
        await _matrixTransition.NavigateWithMatrixAsync<SecondViewModel>(
            _navigator,
            this,
            data: new Entity(Name ?? "Neo"));
    }

    private async Task GoToSecondViewSlow()
    {
        await _matrixTransition.NavigateWithMatrixAsync<SecondViewModel>(
            _navigator,
            this,
            data: new Entity(Name ?? "Neo"),
            options: new MatrixTransitionOptions
            {
                TotalDuration = TimeSpan.FromSeconds(5),
                ColumnSpacing = 4,
                MinTrailLength = 20,
                MaxTrailLength = 50
            });
    }

    private async Task LoopMatrixTransition()
    {
        var options = new MatrixTransitionOptions
        {
            ColumnSpacing = 4,
            MinTrailLength = 20,
            MaxTrailLength = 50
        };

        // Runs continuously until cancelled
        await _matrixTransition.RunLoopAsync(options);
    }
}
