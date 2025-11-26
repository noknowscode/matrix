using matrix.Transitions.Matrix;

namespace matrix.Presentation;

public partial class SecondViewModel : ObservableObject
{
    private readonly INavigator _navigator;
    private readonly IMatrixTransitionService _matrixTransition;

    public SecondViewModel(
        Entity entity,
        INavigator navigator,
        IMatrixTransitionService matrixTransition)
    {
        Entity = entity;
        _navigator = navigator;
        _matrixTransition = matrixTransition;
        GoBack = new AsyncRelayCommand(GoBackView);
    }

    public Entity Entity { get; }

    public ICommand GoBack { get; }

    private async Task GoBackView()
    {
        await _matrixTransition.GoBackWithMatrixAsync(_navigator, this);
    }
}
