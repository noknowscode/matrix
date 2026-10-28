using System.Reflection;
using SkiaSharp;

namespace matrix.Transitions.Matrix;

public sealed class MatrixRainRenderer : IDisposable
{
    private MatrixTransitionOptions _options = new();
    private float _screenWidth;
    private float _screenHeight;
    private float _charHeight;

    private readonly List<MatrixColumn> _columns = [];
    private TransitionPhase _phase = TransitionPhase.Idle;
    private float _elapsedMs;
    private readonly Random _random = new();

    private float _cursorX = -1000;
    private float _cursorY = -1000;
    private const float LogoSize = 350f;

    private SKBitmap? _logoMask;

    private SKPaint? _headPaint;
    private SKPaint? _trailPaint;
    private SKFont? _font;
    private SKTypeface? _typeface;

    // 8-neighborhood sample offsets for border detection. Static to avoid per-call array allocation.
    private static readonly int[] BorderSampleDx = [-1, 0, 1, -1, 1, -1, 0, 1];
    private static readonly int[] BorderSampleDy = [-1, -1, -1, 0, 0, 1, 1, 1];

    // Cached single-char strings for the active character set; rebuilt only when the set changes.
    // Eliminates per-frame char.ToString() allocations in Render().
    private string[]? _charStrings;
    private string? _cachedCharacterSet;

    public event Action<TransitionPhase>? PhaseChanged;
    public event Action? TransitionCompleted;

    public TransitionPhase Phase => _phase;
    public bool IsContinuousMode { get; private set; }

    public void Initialize(float width, float height, MatrixTransitionOptions options)
    {
        _screenWidth = width;
        _screenHeight = height;
        _options = options;

        if (_typeface == null)
        {
            // Prefer fonts with good Katakana support; fall back to monospace.
            _typeface = SKTypeface.FromFamilyName("MS Gothic", SKFontStyle.Bold)
                ?? SKTypeface.FromFamilyName("Hiragino Kaku Gothic Pro", SKFontStyle.Bold)
                ?? SKTypeface.FromFamilyName("Noto Sans JP", SKFontStyle.Bold)
                ?? SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold)
                ?? SKTypeface.Default;

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

            LoadLogoMask();
        }

        if (!ReferenceEquals(_cachedCharacterSet, options.CharacterSet))
        {
            var set = options.CharacterSet;
            var strings = new string[set.Length];
            for (int i = 0; i < set.Length; i++)
            {
                strings[i] = set[i].ToString();
            }
            _charStrings = strings;
            _cachedCharacterSet = set;
        }
    }

    private void LoadLogoMask()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.Contains("Uno-logo", StringComparison.OrdinalIgnoreCase));

            if (resourceName != null)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    _logoMask = SKBitmap.Decode(stream);
                }
            }
        }
        catch
        {
            // Logo mask is optional - scatter falls back to no deflection.
        }
    }

    public void UpdateSize(float width, float height)
    {
        _screenWidth = width;
        _screenHeight = height;
    }

    public void SetCursorPosition(float x, float y)
    {
        _cursorX = x;
        _cursorY = y;
    }

    public void Start(bool continuous = false)
    {
        _columns.Clear();
        _elapsedMs = 0;
        IsContinuousMode = continuous;
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

        if (!IsContinuousMode)
        {
            UpdatePhase();
        }

        foreach (var column in _columns)
        {
            if (!column.IsActive) continue;

            column.Y += column.Speed * (deltaTimeMs / 1000f);

            column.MutationTimer -= deltaTimeMs;
            if (column.MutationTimer <= 0)
            {
                MutateColumn(column);
                column.MutationTimer = _options.MutationIntervalMs;
            }

            float topOfTrail = column.Y - (column.Length * _charHeight);
            if (topOfTrail > _screenHeight)
            {
                if (IsContinuousMode)
                {
                    RespawnColumn(column);
                }
                else
                {
                    column.IsActive = false;
                }
            }
        }

        if (_phase == TransitionPhase.RainIn || _phase == TransitionPhase.Peak || IsContinuousMode)
        {
            TrySpawnColumn();
        }
    }

    public void Render(SKCanvas canvas)
    {
        if (_font == null || _headPaint == null || _trailPaint == null || _charStrings == null)
            return;

        var charStrings = _charStrings;

        // Manual loop avoids Where() enumerator allocation each frame.
        for (int cIdx = 0; cIdx < _columns.Count; cIdx++)
        {
            var column = _columns[cIdx];
            if (!column.IsActive) continue;

            for (int i = 0; i < column.Length; i++)
            {
                float charY = column.Y - (i * _charHeight);

                if (charY < -_charHeight || charY > _screenHeight + _charHeight)
                    continue;

                float alpha = i == 0 ? 1f : Math.Max(0, 1f - (i / (float)column.Length));

                var paint = i == 0 ? _headPaint : _trailPaint;
                var baseColor = i == 0 ? _options.GlowColor : _options.CharacterColor;
                paint.Color = baseColor.WithAlpha((byte)(alpha * 255));

                var (offsetX, offsetY) = CalculateCharacterScatter(column.X, charY);

                int charIndex = column.CharIndices[i % column.CharIndices.Length];
                canvas.DrawText(charStrings[charIndex], column.X + offsetX, charY + offsetY, _font, paint);
            }
        }
    }

    public void Dispose()
    {
        _headPaint?.Dispose();
        _trailPaint?.Dispose();
        _font?.Dispose();
        _typeface?.Dispose();
        _logoMask?.Dispose();
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
            if (_random.NextDouble() < 0.4)
            {
                SpawnColumnAt(i * _options.ColumnSpacing);
            }
        }
    }

    private void TrySpawnColumn()
    {
        if (_random.NextDouble() < 0.15)
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
            Y = -_charHeight * _random.Next(0, 15),
            Speed = _options.MinSpeed + (float)(_random.NextDouble() * (_options.MaxSpeed - _options.MinSpeed)),
            Length = length,
            CharIndices = charIndices,
            MutationTimer = _options.MutationIntervalMs,
            IsActive = true
        });
    }

    private void MutateColumn(MatrixColumn column)
    {
        int mutations = _random.Next(1, 3);
        for (int i = 0; i < mutations; i++)
        {
            int idx = _random.Next(0, column.CharIndices.Length);
            column.CharIndices[idx] = _random.Next(0, _options.CharacterSet.Length);
        }
    }

    private void RespawnColumn(MatrixColumn column)
    {
        column.Y = -_charHeight * _random.Next(5, 20);
        column.Speed = _options.MinSpeed + (float)(_random.NextDouble() * (_options.MaxSpeed - _options.MinSpeed));
        column.Length = _random.Next(_options.MinTrailLength, _options.MaxTrailLength + 1);

        var charIndices = new int[column.Length];
        for (int i = 0; i < column.Length; i++)
        {
            charIndices[i] = _random.Next(0, _options.CharacterSet.Length);
        }
        column.CharIndices = charIndices;
    }

    private (float offsetX, float offsetY) CalculateCharacterScatter(float charX, float charY)
    {
        if (_logoMask == null)
            return (0f, 0f);

        float dx = charX - _cursorX;
        float dy = charY - _cursorY;

        float halfSize = LogoSize / 2f;
        const float margin = 30f;
        if (MathF.Abs(dx) > halfSize + margin || MathF.Abs(dy) > halfSize + margin)
            return (0f, 0f);

        float u = (dx + halfSize) / LogoSize;
        float v = (dy + halfSize) / LogoSize;

        int px = Math.Clamp((int)(u * _logoMask.Width), 0, _logoMask.Width - 1);
        int py = Math.Clamp((int)(v * _logoMask.Height), 0, _logoMask.Height - 1);

        const int borderWidth = 12;
        var (isNearBorder, normalX, normalY) = CheckNearBorder(px, py, borderWidth);

        if (!isNearBorder)
            return (0f, 0f);

        float scale = LogoSize / _logoMask.Width;

        const float pushStrength = 25f;
        float pushX = normalX * pushStrength * scale;
        float pushY = normalY * pushStrength * scale;

        // Slide along the border tangent that points downward, producing a "dripping" effect.
        float tangentX = -normalY;
        float tangentY = normalX;
        if (tangentY < 0)
        {
            tangentX = -tangentX;
            tangentY = -tangentY;
        }

        const float slideStrength = 15f;
        pushX += tangentX * slideStrength * scale;
        pushY += tangentY * slideStrength * scale;

        return (pushX, pushY);
    }

    private bool IsLogoPixel(int px, int py)
    {
        if (_logoMask == null || px < 0 || py < 0 ||
            px >= _logoMask.Width || py >= _logoMask.Height)
            return false;

        var pixel = _logoMask.GetPixel(px, py);
        return pixel.Alpha > 20 &&
            (pixel.Red < 240 || pixel.Green < 240 || pixel.Blue < 240);
    }

    private (bool isNearBorder, float normalX, float normalY) CheckNearBorder(int px, int py, int borderWidth)
    {
        bool isInside = IsLogoPixel(px, py);

        float normalX = 0, normalY = 0;
        bool foundEdge = false;

        for (int checkDist = 1; checkDist <= borderWidth; checkDist++)
        {
            for (int i = 0; i < 8; i++)
            {
                int checkX = px + BorderSampleDx[i] * checkDist;
                int checkY = py + BorderSampleDy[i] * checkDist;

                bool checkInside = IsLogoPixel(checkX, checkY);

                if (checkInside != isInside)
                {
                    // Normal points from logo toward outside.
                    if (isInside)
                    {
                        normalX += BorderSampleDx[i];
                        normalY += BorderSampleDy[i];
                    }
                    else
                    {
                        normalX -= BorderSampleDx[i];
                        normalY -= BorderSampleDy[i];
                    }
                    foundEdge = true;
                }
            }

            if (foundEdge)
                break;
        }

        if (!foundEdge)
            return (false, 0, 0);

        float len = MathF.Sqrt(normalX * normalX + normalY * normalY);
        if (len > 0.01f)
        {
            normalX /= len;
            normalY /= len;
        }

        return (true, normalX, normalY);
    }
}
