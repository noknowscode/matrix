# Matrix Rain Page Transition for Uno Platform

## Overview

A reusable, shareable page transition effect inspired by *The Matrix* digital rain. When navigating between pages, cascading green characters overlay the outgoing page, reach full coverage, then fall away to reveal the incoming page.

**Target Platforms**: All Uno Platform targets (Windows, iOS, Android, macOS, Linux, WebAssembly)

**Rendering Engine**: SkiaSharp (via `SKXamlCanvas`)

**Distribution Goal**: Single-folder drop-in or NuGet package

---

## Visual Specification

### Transition Phases

| Phase | Duration | Visual |
|-------|----------|--------|
| **RainIn** | ~600ms | Columns spawn and fall, outgoing page fades to 0% opacity |
| **Peak** | ~300ms | Full rain coverage, both pages hidden |
| **RainOut** | ~500ms | No new columns spawn, existing rain falls off bottom, incoming page fades to 100% |

**Total Duration**: ~1.4 seconds (configurable)

### Character Rendering

- **Character Set**: Half-width Katakana + digits + select Latin characters
- **Head Character**: Bright white/white-green with subtle glow
- **Trail Characters**: Fade from bright green → dark green → transparent over column length
- **Random Mutation**: Characters randomly swap while falling (every ~100ms per column)

### Column Behavior

- **Density**: One column per ~14–16 pixels of screen width
- **Speed Variance**: Each column falls at a random speed (creates depth)
- **Length Variance**: Trail length varies per column (10–30 characters)
- **Staggered Spawn**: Columns don't all start simultaneously; randomized initial delay

---

## Architecture

### File Structure

```
MatrixTransition/
├── MatrixColumn.cs              # State for a single falling column
├── MatrixRainRenderer.cs        # SkiaSharp rendering + update logic
├── MatrixTransitionOptions.cs   # Configuration record
├── MatrixTransitionOverlay.cs   # SKXamlCanvas control, orchestrates phases
└── FrameExtensions.cs           # Public API: NavigateWithMatrixAsync
```

### Dependency Graph

```
FrameExtensions
       │
       ▼
MatrixTransitionOverlay (SKXamlCanvas)
       │
       ├──▶ MatrixRainRenderer
       │           │
       │           ▼
       │    MatrixColumn[]
       │
       └──▶ MatrixTransitionOptions
```

---

## Component Specifications

### MatrixColumn.cs

Represents a single vertical stream of falling characters.

```csharp
public class MatrixColumn
{
    public float X { get; set; }              // Horizontal position
    public float Y { get; set; }              // Vertical position of head character
    public float Speed { get; set; }          // Pixels per second
    public int Length { get; set; }           // Number of characters in trail
    public int[] CharIndices { get; set; }    // Indices into character set
    public float MutationTimer { get; set; }  // Countdown to next character swap
    public bool IsActive { get; set; }        // False when fully off-screen
}
```

**Responsibilities**:
- Hold per-column state
- No logic; purely a data container

---

### MatrixRainRenderer.cs

Core rendering and simulation engine.

```csharp
public class MatrixRainRenderer
{
    // Configuration
    private MatrixTransitionOptions _options;
    private float _screenWidth;
    private float _screenHeight;
    
    // State
    private List<MatrixColumn> _columns;
    private TransitionPhase _phase;
    private float _elapsedMs;
    private Random _random;
    
    // SkiaSharp resources (cached)
    private SKPaint _headPaint;
    private SKPaint _trailPaint;
    private SKFont _font;
    private string _characterSet;
    
    // Public API
    public void Initialize(float width, float height, MatrixTransitionOptions options);
    public void Start();
    public void Cancel();
    public void Update(float deltaTimeMs);
    public void Render(SKCanvas canvas);
    
    // Events
    public event Action<TransitionPhase> PhaseChanged;
    public event Action TransitionCompleted;
}
```

**Key Methods**:

| Method | Purpose |
|--------|---------|
| `Initialize` | Set dimensions, create cached SKPaint objects, pre-calculate column positions |
| `Start` | Reset state, begin RainIn phase, spawn initial columns |
| `Cancel` | Immediately stop, raise TransitionCompleted |
| `Update` | Advance simulation: move columns, handle phase transitions, mutate characters |
| `Render` | Draw all active columns to SKCanvas |

**Rendering Approach**:

```csharp
public void Render(SKCanvas canvas)
{
    canvas.Clear(SKColors.Transparent);
    
    foreach (var column in _columns.Where(c => c.IsActive))
    {
        for (int i = 0; i < column.Length; i++)
        {
            float charY = column.Y - (i * _charHeight);
            
            // Skip off-screen characters
            if (charY < -_charHeight || charY > _screenHeight) continue;
            
            // Calculate opacity: head is 100%, trail fades
            float alpha = i == 0 ? 1f : 1f - (i / (float)column.Length);
            
            // Head gets glow color, trail gets primary color
            var paint = i == 0 ? _headPaint : _trailPaint;
            paint.Color = paint.Color.WithAlpha((byte)(alpha * 255));
            
            char c = _characterSet[column.CharIndices[i]];
            canvas.DrawText(c.ToString(), column.X, charY, _font, paint);
        }
    }
}
```

---

### MatrixTransitionOptions.cs

Immutable configuration record.

```csharp
public record MatrixTransitionOptions
{
    public TimeSpan TotalDuration { get; init; } = TimeSpan.FromMilliseconds(1400);
    
    public float RainInRatio { get; init; } = 0.43f;    // 600ms of 1400ms
    public float PeakRatio { get; init; } = 0.21f;      // 300ms of 1400ms
    public float RainOutRatio { get; init; } = 0.36f;   // 500ms of 1400ms
    
    public SKColor CharacterColor { get; init; } = new SKColor(0, 255, 70);
    public SKColor GlowColor { get; init; } = SKColors.White;
    
    public string CharacterSet { get; init; } = 
        "アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲン" +
        "0123456789" +
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    
    public int ColumnSpacing { get; init; } = 14;       // Pixels between columns
    public int MinTrailLength { get; init; } = 10;
    public int MaxTrailLength { get; init; } = 30;
    public float MinSpeed { get; init; } = 200f;        // Pixels per second
    public float MaxSpeed { get; init; } = 600f;
    public float MutationIntervalMs { get; init; } = 80f;
    public float FontSize { get; init; } = 14f;
}
```

---

### MatrixTransitionOverlay.cs

XAML control that hosts the SkiaSharp canvas and manages page visibility.

```csharp
public sealed class MatrixTransitionOverlay : Grid
{
    private SKXamlCanvas _canvas;
    private MatrixRainRenderer _renderer;
    private Stopwatch _stopwatch;
    private FrameworkElement _outgoingElement;
    private FrameworkElement _incomingElement;
    private TaskCompletionSource<bool> _completionSource;
    
    // Public API
    public Task RunTransitionAsync(
        FrameworkElement outgoing,
        FrameworkElement incoming,
        MatrixTransitionOptions options,
        CancellationToken cancellationToken);
}
```

**Transition Orchestration**:

```csharp
public async Task RunTransitionAsync(...)
{
    _completionSource = new TaskCompletionSource<bool>();
    
    // Setup
    _outgoingElement = outgoing;
    _incomingElement = incoming;
    _incomingElement.Opacity = 0;
    
    _renderer.Initialize((float)ActualWidth, (float)ActualHeight, options);
    _renderer.PhaseChanged += OnPhaseChanged;
    _renderer.TransitionCompleted += OnTransitionCompleted;
    
    // Handle cancellation
    cancellationToken.Register(() => 
    {
        _renderer.Cancel();
        _completionSource.TrySetCanceled();
    });
    
    // Start animation loop
    _renderer.Start();
    _stopwatch = Stopwatch.StartNew();
    CompositionTarget.Rendering += OnFrame;
    
    await _completionSource.Task;
    
    // Cleanup
    CompositionTarget.Rendering -= OnFrame;
    _canvas.Invalidate(); // Final clear
}

private void OnFrame(object sender, object e)
{
    float delta = (float)_stopwatch.Elapsed.TotalMilliseconds;
    _stopwatch.Restart();
    
    _renderer.Update(delta);
    _canvas.Invalidate();
}

private void OnPhaseChanged(TransitionPhase phase)
{
    switch (phase)
    {
        case TransitionPhase.RainIn:
            // Animate outgoing opacity 1 → 0
            AnimateOpacity(_outgoingElement, 1, 0, _options.RainInDuration);
            break;
            
        case TransitionPhase.RainOut:
            // Animate incoming opacity 0 → 1
            AnimateOpacity(_incomingElement, 0, 1, _options.RainOutDuration);
            break;
    }
}

private void OnTransitionCompleted()
{
    _outgoingElement.Opacity = 0;
    _incomingElement.Opacity = 1;
    _completionSource.TrySetResult(true);
}
```

---

### FrameExtensions.cs

Public extension method for Frame navigation.

```csharp
public static class FrameExtensions
{
    private static MatrixTransitionOverlay _overlay;
    private static CancellationTokenSource _cts;
    
    public static async Task NavigateWithMatrixAsync(
        this Frame frame,
        Type pageType,
        object parameter = null,
        MatrixTransitionOptions options = null)
    {
        options ??= new MatrixTransitionOptions();
        
        // Cancel any in-progress transition
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        
        // Capture outgoing page
        var outgoing = frame.Content as FrameworkElement;
        
        // Navigate (page is now in tree but hidden)
        frame.Navigate(pageType, parameter);
        var incoming = frame.Content as FrameworkElement;
        incoming.Opacity = 0;
        
        // Ensure overlay is in visual tree
        EnsureOverlay(frame);
        
        try
        {
            await _overlay.RunTransitionAsync(
                outgoing, 
                incoming, 
                options, 
                _cts.Token);
        }
        catch (TaskCanceledException)
        {
            // Transition was interrupted; incoming page already in place
            incoming.Opacity = 1;
        }
    }
    
    private static void EnsureOverlay(Frame frame)
    {
        // Find root container, inject overlay if not present
        // Implementation depends on app structure
    }
}
```

---

## Usage Examples

### Basic

```csharp
await RootFrame.NavigateWithMatrixAsync(typeof(SettingsPage));
```

### With Parameter

```csharp
await RootFrame.NavigateWithMatrixAsync(typeof(DetailPage), selectedItem.Id);
```

### Custom Colors

```csharp
await RootFrame.NavigateWithMatrixAsync(typeof(CyberpunkPage), null, 
    new MatrixTransitionOptions 
    { 
        CharacterColor = new SKColor(0, 200, 255),  // Cyan
        GlowColor = SKColors.White
    });
```

### Faster Transition

```csharp
await RootFrame.NavigateWithMatrixAsync(typeof(QuickPage), null, 
    new MatrixTransitionOptions 
    { 
        TotalDuration = TimeSpan.FromMilliseconds(800)
    });
```

---

## Platform Considerations

| Platform | Notes |
|----------|-------|
| **Windows** | Full support via SkiaSharp.Views.WinUI |
| **WebAssembly** | Works; may need reduced column density for performance |
| **iOS / Android** | Full support; test on lower-end devices |
| **macOS / Linux** | Full support via SkiaSharp GTK/AppKit |

### Performance Targets

- **Desktop**: 60fps, up to 150 columns
- **Mobile**: 60fps, reduce to ~80–100 columns if needed
- **WASM**: 30–60fps depending on browser; consider reducing column count or trail length

### WASM-Specific Optimizations

```csharp
#if __WASM__
    options = options with 
    { 
        ColumnSpacing = 20,      // Fewer columns
        MaxTrailLength = 20,     // Shorter trails
        TotalDuration = TimeSpan.FromMilliseconds(1000)  // Faster
    };
#endif
```

---

## Future Enhancements (Out of Scope for V1)

- **Page dissolution**: Outgoing page bitmap breaks apart into characters
- **Depth layers**: Multiple rain layers at different speeds/opacity for parallax
- **Audio**: Optional subtle rain sound effect
- **Theme presets**: Matrix green, Tron cyan, monochrome white
- **Reverse mode**: Rain rises instead of falls
- **Custom fonts**: Allow developer-supplied typeface

---

## Distribution Strategy

### Option A: Source Files

Distribute as a folder developers copy into their solution. Minimal friction, maximum transparency.

### Option B: NuGet Package

```
Uno.Community.MatrixTransition
```

Dependencies: `SkiaSharp.Views.Uno.WinUI` (or equivalent for Uno 5+)

### Licensing

MIT — keep it simple for community adoption.

---

## Open Questions

1. **Overlay injection**: How should the overlay be injected into the visual tree? Options:
   - Require developer to add `<MatrixTransitionOverlay>` to root layout
   - Auto-inject into Frame's parent container
   - Use a popup/flyout layer

2. **Back navigation**: Should `Frame.GoBack()` also support this transition, or only forward navigation?

3. **Testing approach**: Unit tests for renderer logic; manual/visual tests for actual transition?
