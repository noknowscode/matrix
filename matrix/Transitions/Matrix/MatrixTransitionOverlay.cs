using System.Diagnostics;
using Microsoft.UI.Xaml.Media.Animation;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace matrix.Transitions.Matrix;

public sealed class MatrixTransitionOverlay : SKCanvasElement
{
    private readonly MatrixRainRenderer _renderer = new();
    private readonly Stopwatch _stopwatch = new();
    private readonly DispatcherTimer _frameTimer = new();
    private TaskCompletionSource<bool>? _completionSource;
    private FrameworkElement? _outgoingElement;
    private FrameworkElement? _incomingElement;
    private MatrixTransitionOptions _options = new();
    private bool _isRunning;
    private CancellationTokenRegistration _cancellationRegistration;

    public MatrixTransitionOverlay()
    {
        Canvas.SetZIndex(this, int.MaxValue);
        IsHitTestVisible = true;
        Visibility = Visibility.Collapsed;

        _renderer.PhaseChanged += OnPhaseChanged;
        _renderer.TransitionCompleted += OnTransitionCompleted;

        PointerMoved += OnPointerMoved;
        PointerExited += OnPointerExited;

        // ~120fps cap.
        _frameTimer.Interval = TimeSpan.FromMilliseconds(8);
        _frameTimer.Tick += (_, _) => Invalidate();
    }

    private void OnPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        _renderer.SetCursorPosition((float)point.Position.X, (float)point.Position.Y);
    }

    private void OnPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _renderer.SetCursorPosition(-1000, -1000);
    }

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        if (!_isRunning || area.Width <= 0 || area.Height <= 0)
        {
            canvas.Clear(SKColors.Transparent);
            return;
        }

        float delta = (float)_stopwatch.Elapsed.TotalMilliseconds;
        _stopwatch.Restart();

        _renderer.UpdateSize((float)area.Width, (float)area.Height);
        _renderer.Update(delta);

        canvas.Clear(new SKColor(0, 0, 0, 200));
        _renderer.Render(canvas);

        if (!_isRunning)
        {
            _frameTimer.Stop();
        }
    }

    public async Task RunTransitionAsync(
        FrameworkElement? outgoing,
        FrameworkElement? incoming,
        MatrixTransitionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (_isRunning) return;

        _options = options ?? new MatrixTransitionOptions();
        _completionSource = new TaskCompletionSource<bool>();
        _outgoingElement = outgoing;
        _incomingElement = incoming;

#if __BROWSERWASM__
        _options = _options with
        {
            ColumnSpacing = 20,
            MaxTrailLength = 20,
            TotalDuration = TimeSpan.FromMilliseconds(1000)
        };
#endif

        if (_incomingElement != null)
        {
            _incomingElement.Opacity = 0;
        }

        _cancellationRegistration = cancellationToken.Register(() =>
        {
            _renderer.Cancel();
            _completionSource?.TrySetCanceled();
        });

        await StartRenderingAsync();

        try
        {
            await _completionSource.Task;
        }
        finally
        {
            _isRunning = false;
            Visibility = Visibility.Collapsed;
            _cancellationRegistration.Dispose();
        }
    }

    public async Task PrepareTransitionAsync(
        FrameworkElement? outgoing,
        MatrixTransitionOptions? options = null)
    {
        if (_isRunning) return;

        _options = options ?? new MatrixTransitionOptions();

#if __BROWSERWASM__
        _options = _options with
        {
            ColumnSpacing = 20,
            MaxTrailLength = 20,
            TotalDuration = TimeSpan.FromMilliseconds(1000)
        };
#endif

        _completionSource = new TaskCompletionSource<bool>();
        _outgoingElement = outgoing;
        _incomingElement = null;

        await StartRenderingAsync();
    }

    public async Task CompleteTransitionAsync(
        FrameworkElement? incoming,
        CancellationToken cancellationToken = default)
    {
        _incomingElement = incoming;

        if (_incomingElement != null)
        {
            _incomingElement.Opacity = 0;
        }

        _cancellationRegistration = cancellationToken.Register(() =>
        {
            _renderer.Cancel();
            _completionSource?.TrySetCanceled();
        });

        try
        {
            await _completionSource!.Task;
        }
        finally
        {
            _isRunning = false;
            Visibility = Visibility.Collapsed;
            _cancellationRegistration.Dispose();
        }
    }

    public async Task RunContinuousAsync(
        MatrixTransitionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (_isRunning) return;

        _options = options ?? new MatrixTransitionOptions();
        _outgoingElement = null;
        _incomingElement = null;

        var tcs = new TaskCompletionSource<bool>();
        _cancellationRegistration = cancellationToken.Register(() =>
        {
            _renderer.Cancel();
            tcs.TrySetResult(true);
        });

        await StartRenderingAsync(continuous: true);

        try
        {
            await tcs.Task;
        }
        finally
        {
            _isRunning = false;
            Visibility = Visibility.Collapsed;
            _cancellationRegistration.Dispose();
        }
    }

    private async Task StartRenderingAsync(bool continuous = false)
    {
        Visibility = Visibility.Visible;
        UpdateLayout();

        // Yield a frame so layout can resolve before measuring.
        await Task.Delay(16);

        var parent = Parent as FrameworkElement;
        var width = parent?.ActualWidth ?? ActualWidth;
        var height = parent?.ActualHeight ?? ActualHeight;

        if (width <= 0 || height <= 0)
        {
            width = 800;
            height = 600;
        }

        _renderer.Initialize((float)width, (float)height, _options);

        _isRunning = true;
        _stopwatch.Restart();
        _renderer.Start(continuous);
        _frameTimer.Start();
        Invalidate();
    }

    private void OnPhaseChanged(TransitionPhase phase)
    {
        // Loop mode (no in/out elements) has nothing to animate.
        if (_outgoingElement == null && _incomingElement == null) return;

        DispatcherQueue.TryEnqueue(() =>
        {
            switch (phase)
            {
                case TransitionPhase.RainIn:
                    AnimateOpacity(_outgoingElement, 1, 0, _options.RainInDuration);
                    break;

                case TransitionPhase.RainOut:
                    AnimateOpacity(_incomingElement, 0, 1, _options.RainOutDuration);
                    break;
            }
        });
    }

    private void OnTransitionCompleted()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_outgoingElement != null) _outgoingElement.Opacity = 0;
            if (_incomingElement != null) _incomingElement.Opacity = 1;
            _completionSource?.TrySetResult(true);
        });
    }

    private static void AnimateOpacity(FrameworkElement? element, double from, double to, TimeSpan duration)
    {
        if (element == null) return;

        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(duration),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        Storyboard.SetTarget(animation, element);
        Storyboard.SetTargetProperty(animation, "Opacity");
        storyboard.Begin();
    }
}
