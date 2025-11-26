using Uno.Extensions.Navigation;

namespace matrix.Transitions.Matrix;

/// <summary>
/// Service to trigger Matrix rain transitions during navigation.
/// </summary>
public interface IMatrixTransitionService
{
    /// <summary>
    /// Registers the overlay control and content getter from Shell.
    /// </summary>
    void RegisterOverlay(MatrixTransitionOverlay overlay, Func<FrameworkElement?> contentGetter);

    /// <summary>
    /// Navigate forward with Matrix transition.
    /// </summary>
    Task NavigateWithMatrixAsync<TViewModel>(
        INavigator navigator,
        object? sender = null,
        object? data = null,
        MatrixTransitionOptions? options = null);

    /// <summary>
    /// Navigate back with Matrix transition.
    /// </summary>
    Task GoBackWithMatrixAsync(
        INavigator navigator,
        object? sender = null,
        MatrixTransitionOptions? options = null);

    /// <summary>
    /// Run matrix effect in a loop (no navigation).
    /// </summary>
    Task RunLoopAsync(MatrixTransitionOptions? options = null);

    /// <summary>
    /// Gets whether a transition is currently running.
    /// </summary>
    bool IsTransitioning { get; }
}

public sealed class MatrixTransitionService : IMatrixTransitionService
{
    private MatrixTransitionOverlay? _overlay;
    private Func<FrameworkElement?>? _contentGetter;
    private CancellationTokenSource? _cts;

    public bool IsTransitioning { get; private set; }

    public void RegisterOverlay(MatrixTransitionOverlay overlay, Func<FrameworkElement?> contentGetter)
    {
        _overlay = overlay;
        _contentGetter = contentGetter;
    }

    public async Task NavigateWithMatrixAsync<TViewModel>(
        INavigator navigator,
        object? sender = null,
        object? data = null,
        MatrixTransitionOptions? options = null)
    {
        if (_overlay == null || _contentGetter == null || IsTransitioning)
        {
            await navigator.NavigateViewModelAsync<TViewModel>(sender!, data: data);
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        var outgoing = _contentGetter();

        // Start overlay BEFORE navigation to hide the flash
        IsTransitioning = true;
        await _overlay.PrepareTransitionAsync(outgoing, options);

        // Perform navigation (page is hidden by overlay)
        await navigator.NavigateViewModelAsync<TViewModel>(sender!, data: data);

        // Small delay to ensure new content is rendered
        await Task.Delay(16);

        var incoming = _contentGetter();
        if (incoming != null) incoming.Opacity = 0;

        try
        {
            await _overlay.CompleteTransitionAsync(incoming, _cts.Token);
        }
        catch (TaskCanceledException)
        {
            if (incoming != null) incoming.Opacity = 1;
        }
        finally
        {
            IsTransitioning = false;
        }
    }

    public async Task GoBackWithMatrixAsync(
        INavigator navigator,
        object? sender = null,
        MatrixTransitionOptions? options = null)
    {
        if (_overlay == null || _contentGetter == null || IsTransitioning)
        {
            await navigator.NavigateBackAsync(sender!);
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        var outgoing = _contentGetter();

        // Start overlay BEFORE navigation to hide the flash
        IsTransitioning = true;
        await _overlay.PrepareTransitionAsync(outgoing, options);

        await navigator.NavigateBackAsync(sender!);

        await Task.Delay(16);

        var incoming = _contentGetter();
        if (incoming != null) incoming.Opacity = 0;

        try
        {
            await _overlay.CompleteTransitionAsync(incoming, _cts.Token);
        }
        catch (TaskCanceledException)
        {
            if (incoming != null) incoming.Opacity = 1;
        }
        finally
        {
            IsTransitioning = false;
        }
    }

    public async Task RunLoopAsync(MatrixTransitionOptions? options = null)
    {
        if (_overlay == null || IsTransitioning) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        IsTransitioning = true;
        try
        {
            await _overlay.RunContinuousAsync(options, _cts.Token);
        }
        catch (TaskCanceledException)
        {
            // Ignore
        }
        finally
        {
            IsTransitioning = false;
        }
    }

    public void StopLoop()
    {
        _cts?.Cancel();
    }
}
