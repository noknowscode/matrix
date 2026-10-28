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
    }

    public Entity Entity { get; }

    [RelayCommand]
    private Task GoBack() => _matrixTransition.GoBackWithMatrixAsync(_navigator, this);
}
