using matrix.Transitions.Matrix;

namespace matrix.Presentation;

public partial class MainViewModel : ObservableObject
{
    private readonly INavigator _navigator;
    private readonly IMatrixTransitionService _matrixTransition;

    public MainViewModel(
        IStringLocalizer localizer,
        IOptions<AppConfig> appInfo,
        INavigator navigator,
        IMatrixTransitionService matrixTransition)
    {
        _navigator = navigator;
        _matrixTransition = matrixTransition;
        Title = $"Main - {localizer["ApplicationName"]} - {appInfo?.Value?.Environment}";
    }

    public string Title { get; }

    [RelayCommand]
    private Task GoToSecond() =>
        _matrixTransition.NavigateWithMatrixAsync<SecondViewModel>(
            _navigator,
            this,
            data: new Entity("Neo"));

    [RelayCommand]
    private Task GoToSecondSlow() =>
        _matrixTransition.NavigateWithMatrixAsync<SecondViewModel>(
            _navigator,
            this,
            data: new Entity("Neo"),
            options: new MatrixTransitionOptions
            {
                TotalDuration = TimeSpan.FromSeconds(5),
                ColumnSpacing = 4,
                MinTrailLength = 20,
                MaxTrailLength = 50
            });

    [RelayCommand]
    private Task LoopMatrix() =>
        _matrixTransition.RunLoopAsync(new MatrixTransitionOptions
        {
            ColumnSpacing = 4,
            MinTrailLength = 20,
            MaxTrailLength = 50
        });
}
