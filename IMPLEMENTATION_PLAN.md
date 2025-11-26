# Matrix Rain Transition - Uno Platform Implementation Plan

## Executive Summary

Build a reusable Matrix-style page transition using **SKCanvasElement** (not SKXamlCanvas) for optimal performance with the Skia renderer. The project already has the correct infrastructure with `SkiaRenderer` in UnoFeatures.

---

## Key Architecture Decision: SKCanvasElement vs SKXamlCanvas

| Aspect | SKCanvasElement | SKXamlCanvas |
|--------|-----------------|--------------|
| **Performance** | Uses internal Skia canvas - no buffer copying | Creates separate surface with buffer copying |
| **Hardware Accel** | Automatic if app uses OpenGL | Not yet supported on Skia targets |
| **Package** | `Uno.WinUI.Graphics2DSK` (auto-referenced with SkiaRenderer) | `SkiaSharp.Views.Uno.WinUI` |
| **Recommendation** | **Use this** | Avoid for this use case |

Since the project has `SkiaRenderer` in UnoFeatures, `SKCanvasElement` is the right choice.

---

## Recommended File Structure

```
matrix/
├── Transitions/
│   ├── Matrix/
│   │   ├── MatrixColumn.cs                 # Column state data
│   │   ├── MatrixRainRenderer.cs           # Skia rendering logic
│   │   ├── MatrixTransitionOptions.cs      # Configuration record
│   │   ├── MatrixTransitionOverlay.cs      # SKCanvasElement subclass
│   │   └── TransitionPhase.cs              # Enum for phases
│   └── Extensions/
│       └── NavigatorExtensions.cs          # Integration with Uno.Extensions.Navigation
```

---

## Component Specifications

### 1. MatrixColumn.cs (Data Container)

```csharp
namespace matrix.Transitions.Matrix;

/// <summary>
/// State for a single falling column of characters.
/// </summary>
public sealed class MatrixColumn
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Speed { get; set; }
    public int Length { get; set; }
    public int[] CharIndices { get; set; } = [];
    public float MutationTimer { get; set; }
    public bool IsActive { get; set; }
}
```

### 2. TransitionPhase.cs

```csharp
namespace matrix.Transitions.Matrix;

public enum TransitionPhase
{
    Idle,
    RainIn,    // Columns spawn, outgoing fades
    Peak,      // Full coverage
    RainOut,   // Rain falls off, incoming fades in
    Complete
}
```

### 3. MatrixTransitionOptions.cs

Use `record` for immutability and `with` expressions for WASM optimization.

```csharp
namespace matrix.Transitions.Matrix;

public record MatrixTransitionOptions
{
    public TimeSpan TotalDuration { get; init; } = TimeSpan.FromMilliseconds(1400);

    public float RainInRatio { get; init; } = 0.43f;
    public float PeakRatio { get; init; } = 0.21f;
    public float RainOutRatio { get; init; } = 0.36f;

    // Use SKColor from SkiaSharp
    public SKColor CharacterColor { get; init; } = new(0, 255, 70);
    public SKColor GlowColor { get; init; } = SKColors.White;

    public string CharacterSet { get; init; } =
        "アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲン" +
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public int ColumnSpacing { get; init; } = 14;
    public int MinTrailLength { get; init; } = 10;
    public int MaxTrailLength { get; init; } = 30;
    public float MinSpeed { get; init; } = 200f;
    public float MaxSpeed { get; init; } = 600f;
    public float MutationIntervalMs { get; init; } = 80f;
    public float FontSize { get; init; } = 14f;

    // Computed durations
    public TimeSpan RainInDuration => TotalDuration * RainInRatio;
    public TimeSpan PeakDuration => TotalDuration * PeakRatio;
    public TimeSpan RainOutDuration => TotalDuration * RainOutRatio;
}
```

### 4. MatrixRainRenderer.cs

Core rendering engine - pure logic, no UI dependencies.

```csharp
namespace matrix.Transitions.Matrix;

public sealed class MatrixRainRenderer
{
    private MatrixTransitionOptions _options = new();
    private float _screenWidth;
    private float _screenHeight;
    private float _charHeight;

    private readonly List<MatrixColumn> _columns = [];
    private TransitionPhase _phase = TransitionPhase.Idle;
    private float _elapsedMs;
    private readonly Random _random = new();

    // Cached SkiaSharp resources
    private SKPaint? _headPaint;
    private SKPaint? _trailPaint;
    private SKFont? _font;
    private SKTypeface? _typeface;

    public event Action<TransitionPhase>? PhaseChanged;
    public event Action? TransitionCompleted;

    public TransitionPhase Phase => _phase;

    public void Initialize(float width, float height, MatrixTransitionOptions options)
    {
        _screenWidth = width;
        _screenHeight = height;
        _options = options;

        // Create cached paint objects
        _typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold);
        _font = new SKFont(_typeface, options.FontSize);
        _charHeight = options.FontSize * 1.2f;

        _headPaint = new SKPaint
        {
            Color = options.GlowColor,
            IsAntialias = true
        };

        _trailPaint = new SKPaint
        {
            Color = options.CharacterColor,
            IsAntialias = true
        };
    }

    public void Start()
    {
        _columns.Clear();
        _elapsedMs = 0;
        SetPhase(TransitionPhase.RainIn);
        SpawnInitialColumns();
    }

    public void Cancel()
    {
        SetPhase(TransitionPhase.Complete);
        TransitionCompleted?.Invoke();
    }

    public void Update(float deltaTimeMs)
    {
        if (_phase == TransitionPhase.Idle || _phase == TransitionPhase.Complete)
            return;

        _elapsedMs += deltaTimeMs;

        // Update phase based on elapsed time
        UpdatePhase();

        // Update columns
        foreach (var column in _columns)
        {
            if (!column.IsActive) continue;

            // Move column down
            column.Y += column.Speed * (deltaTimeMs / 1000f);

            // Mutate characters
            column.MutationTimer -= deltaTimeMs;
            if (column.MutationTimer <= 0)
            {
                MutateColumn(column);
                column.MutationTimer = _options.MutationIntervalMs;
            }

            // Check if column is off-screen
            float topOfTrail = column.Y - (column.Length * _charHeight);
            if (topOfTrail > _screenHeight)
            {
                column.IsActive = false;
            }
        }

        // Spawn new columns during RainIn phase
        if (_phase == TransitionPhase.RainIn)
        {
            TrySpawnColumn();
        }
    }

    public void Render(SKCanvas canvas)
    {
        if (_font == null || _headPaint == null || _trailPaint == null)
            return;

        foreach (var column in _columns.Where(c => c.IsActive))
        {
            for (int i = 0; i < column.Length; i++)
            {
                float charY = column.Y - (i * _charHeight);

                // Skip off-screen characters
                if (charY < -_charHeight || charY > _screenHeight + _charHeight)
                    continue;

                // Calculate opacity: head is brightest, trail fades
                float alpha = i == 0 ? 1f : Math.Max(0, 1f - (i / (float)column.Length));

                // Head gets glow color, trail gets primary color
                var paint = i == 0 ? _headPaint : _trailPaint;
                var baseColor = i == 0 ? _options.GlowColor : _options.CharacterColor;
                paint.Color = baseColor.WithAlpha((byte)(alpha * 255));

                int charIndex = column.CharIndices[i % column.CharIndices.Length];
                char c = _options.CharacterSet[charIndex];
                canvas.DrawText(c.ToString(), column.X, charY, _font, paint);
            }
        }
    }

    public void Dispose()
    {
        _headPaint?.Dispose();
        _trailPaint?.Dispose();
        _font?.Dispose();
        _typeface?.Dispose();
    }

    private void SetPhase(TransitionPhase phase)
    {
        if (_phase != phase)
        {
            _phase = phase;
            PhaseChanged?.Invoke(phase);
        }
    }

    private void UpdatePhase()
    {
        float rainInEnd = (float)_options.RainInDuration.TotalMilliseconds;
        float peakEnd = rainInEnd + (float)_options.PeakDuration.TotalMilliseconds;
        float totalEnd = (float)_options.TotalDuration.TotalMilliseconds;

        if (_elapsedMs < rainInEnd)
        {
            SetPhase(TransitionPhase.RainIn);
        }
        else if (_elapsedMs < peakEnd)
        {
            SetPhase(TransitionPhase.Peak);
        }
        else if (_elapsedMs < totalEnd)
        {
            SetPhase(TransitionPhase.RainOut);
        }
        else
        {
            SetPhase(TransitionPhase.Complete);
            TransitionCompleted?.Invoke();
        }
    }

    private void SpawnInitialColumns()
    {
        int columnCount = (int)(_screenWidth / _options.ColumnSpacing);
        for (int i = 0; i < columnCount; i++)
        {
            // Stagger spawn with random delay
            if (_random.NextDouble() < 0.3)
            {
                SpawnColumnAt(i * _options.ColumnSpacing);
            }
        }
    }

    private void TrySpawnColumn()
    {
        // Randomly spawn columns during RainIn
        if (_random.NextDouble() < 0.1)
        {
            float x = _random.Next(0, (int)_screenWidth);
            x = (float)Math.Round(x / _options.ColumnSpacing) * _options.ColumnSpacing;
            SpawnColumnAt(x);
        }
    }

    private void SpawnColumnAt(float x)
    {
        int length = _random.Next(_options.MinTrailLength, _options.MaxTrailLength + 1);
        var charIndices = new int[length];
        for (int i = 0; i < length; i++)
        {
            charIndices[i] = _random.Next(0, _options.CharacterSet.Length);
        }

        _columns.Add(new MatrixColumn
        {
            X = x,
            Y = -_charHeight * _random.Next(0, 10), // Start above screen
            Speed = _options.MinSpeed + (float)(_random.NextDouble() * (_options.MaxSpeed - _options.MinSpeed)),
            Length = length,
            CharIndices = charIndices,
            MutationTimer = _options.MutationIntervalMs,
            IsActive = true
        });
    }

    private void MutateColumn(MatrixColumn column)
    {
        // Randomly change 1-2 characters
        int mutations = _random.Next(1, 3);
        for (int i = 0; i < mutations; i++)
        {
            int idx = _random.Next(0, column.CharIndices.Length);
            column.CharIndices[idx] = _random.Next(0, _options.CharacterSet.Length);
        }
    }
}
```

### 5. MatrixTransitionOverlay.cs

Subclass of `SKCanvasElement` - the key Uno Platform integration point.

```csharp
namespace matrix.Transitions.Matrix;

public sealed class MatrixTransitionOverlay : SKCanvasElement
{
    private readonly MatrixRainRenderer _renderer = new();
    private readonly Stopwatch _stopwatch = new();
    private TaskCompletionSource<bool>? _completionSource;
    private FrameworkElement? _outgoingElement;
    private FrameworkElement? _incomingElement;
    private MatrixTransitionOptions _options = new();
    private bool _isRunning;

    public MatrixTransitionOverlay()
    {
        // Ensure we're on top
        Canvas.SetZIndex(this, int.MaxValue);

        _renderer.PhaseChanged += OnPhaseChanged;
        _renderer.TransitionCompleted += OnTransitionCompleted;
    }

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        if (!_isRunning) return;

        // Update simulation
        float delta = (float)_stopwatch.Elapsed.TotalMilliseconds;
        _stopwatch.Restart();

        _renderer.Update(delta);
        _renderer.Render(canvas);

        // Request next frame
        if (_isRunning)
        {
            Invalidate();
        }
    }

    public async Task RunTransitionAsync(
        FrameworkElement? outgoing,
        FrameworkElement? incoming,
        MatrixTransitionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _options = options ?? new MatrixTransitionOptions();
        _completionSource = new TaskCompletionSource<bool>();
        _outgoingElement = outgoing;
        _incomingElement = incoming;

        // Apply WASM optimizations
        #if __WASM__
        _options = _options with
        {
            ColumnSpacing = 20,
            MaxTrailLength = 20,
            TotalDuration = TimeSpan.FromMilliseconds(1000)
        };
        #endif

        // Hide incoming page initially
        if (_incomingElement != null)
        {
            _incomingElement.Opacity = 0;
        }

        // Initialize renderer
        _renderer.Initialize((float)ActualWidth, (float)ActualHeight, _options);

        // Handle cancellation
        cancellationToken.Register(() =>
        {
            _renderer.Cancel();
            _completionSource?.TrySetCanceled();
        });

        // Start animation
        _isRunning = true;
        _stopwatch.Restart();
        _renderer.Start();
        Invalidate(); // Trigger first render

        try
        {
            await _completionSource.Task;
        }
        finally
        {
            _isRunning = false;
            Invalidate(); // Clear canvas
        }
    }

    private void OnPhaseChanged(TransitionPhase phase)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (phase)
            {
                case TransitionPhase.RainIn:
                    // Fade out outgoing page
                    AnimateOpacity(_outgoingElement, 1, 0, _options.RainInDuration);
                    break;

                case TransitionPhase.RainOut:
                    // Fade in incoming page
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
```

### 6. NavigatorExtensions.cs

Integration with **Uno.Extensions.Navigation** (the project uses this, not raw Frame).

```csharp
namespace matrix.Transitions.Extensions;

public static class NavigatorExtensions
{
    private static MatrixTransitionOverlay? _overlay;
    private static CancellationTokenSource? _cts;

    /// <summary>
    /// Navigate with Matrix rain transition effect.
    /// </summary>
    public static async Task NavigateWithMatrixAsync(
        this INavigator navigator,
        string route,
        object? data = null,
        MatrixTransitionOptions? options = null)
    {
        // Cancel any in-progress transition
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        // Get current content for outgoing animation
        var outgoing = GetCurrentContent(navigator);

        // Ensure overlay exists in visual tree
        EnsureOverlay(navigator);

        // Navigate (content changes but is hidden)
        var response = await navigator.NavigateRouteAsync(navigator, route, data: data);
        var incoming = GetCurrentContent(navigator);

        if (incoming != null)
        {
            incoming.Opacity = 0;
        }

        try
        {
            if (_overlay != null)
            {
                await _overlay.RunTransitionAsync(outgoing, incoming, options, _cts.Token);
            }
        }
        catch (TaskCanceledException)
        {
            // Ensure incoming is visible if cancelled
            if (incoming != null) incoming.Opacity = 1;
        }
    }

    private static FrameworkElement? GetCurrentContent(INavigator navigator)
    {
        // Implementation depends on Shell structure
        // This is a placeholder - actual implementation needs to find the Frame content
        return null;
    }

    private static void EnsureOverlay(INavigator navigator)
    {
        // Find root Grid and add overlay if not present
        // Implementation depends on Shell structure
    }
}
```

---

## Integration with Existing Shell

The current Shell uses `ExtendedSplashScreen`. Modify to include an overlay host:

```xml
<UserControl x:Class="matrix.Presentation.Shell"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:utu="using:Uno.Toolkit.UI"
    xmlns:matrix="using:matrix.Transitions.Matrix">

    <Grid>
        <Border Background="{ThemeResource BackgroundBrush}">
            <utu:ExtendedSplashScreen x:Name="Splash"
                HorizontalAlignment="Stretch"
                VerticalAlignment="Stretch"
                HorizontalContentAlignment="Stretch"
                VerticalContentAlignment="Stretch">
                <!-- ... existing template ... -->
            </utu:ExtendedSplashScreen>
        </Border>

        <!-- Matrix transition overlay - always on top -->
        <matrix:MatrixTransitionOverlay x:Name="MatrixOverlay"
            HorizontalAlignment="Stretch"
            VerticalAlignment="Stretch"
            IsHitTestVisible="False" />
    </Grid>
</UserControl>
```

---

## Uno Platform Best Practices Applied

| Practice | Implementation |
|----------|----------------|
| **Use SKCanvasElement** | Leverages internal Skia canvas, avoids buffer copying |
| **GPU-bound animations** | Opacity is GPU-accelerated per Uno docs |
| **Storyboard for opacity** | Uses WinUI Storyboard API for page fades |
| **DispatcherQueue** | Thread-safe UI updates from renderer callbacks |
| **Platform conditionals** | WASM optimizations via `#if __WASM__` |
| **Resource caching** | SKPaint/SKFont created once in Initialize |
| **Dispose pattern** | Proper cleanup of SkiaSharp resources |
| **Record types** | Immutable options with `with` expressions |

---

## Platform-Specific Considerations

### WebAssembly
```csharp
#if __WASM__
options = options with
{
    ColumnSpacing = 20,      // Fewer columns
    MaxTrailLength = 20,     // Shorter trails
    TotalDuration = TimeSpan.FromMilliseconds(1000)
};
#endif
```

### Mobile (iOS/Android)
- Already using Skia renderer - full support
- Consider reducing column density on older devices
- Touch interaction disabled on overlay via `IsHitTestVisible="False"`

### Desktop (Windows/macOS/Linux)
- Full 60fps support
- No modifications needed

---

## Required NuGet Packages

Already satisfied by UnoFeatures:
- `Uno.WinUI.Graphics2DSK` - Auto-included with `SkiaRenderer`
- `SkiaSharp` - Transitive dependency

No additional packages needed.

---

## Performance Targets

| Platform | Target FPS | Max Columns |
|----------|-----------|-------------|
| Desktop | 60 | 150 |
| Mobile | 60 | 100 |
| WASM | 30-60 | 60 |

---

## Unresolved Questions

1. **Overlay injection strategy**: Should the overlay be:
   - Declared in Shell.xaml (recommended - explicit)
   - Auto-injected via attached property
   - Created dynamically per navigation

2. **Back navigation support**: Should `GoBack()` also trigger Matrix transition? The brief mentions this as an open question.

3. **Navigator integration depth**: The project uses `Uno.Extensions.Navigation` with region-based routing. Need to determine:
   - How to intercept navigation to capture outgoing content
   - Whether to extend `INavigator` or create wrapper methods
   - How to access the Frame content from Shell

4. **Font for Katakana**: Should we:
   - Use system fonts (Consolas fallback)
   - Bundle a specific monospace font with Katakana support
   - Use Material Icons font as fallback

5. **Testing approach**:
   - Unit tests for `MatrixRainRenderer` logic
   - Visual/manual tests for actual rendering
   - Performance benchmarks per platform
