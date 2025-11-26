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

    private SKPaint? _headPaint;
    private SKPaint? _trailPaint;
    private SKFont? _font;
    private SKTypeface? _typeface;

    public event Action<TransitionPhase>? PhaseChanged;
    public event Action? TransitionCompleted;

    public TransitionPhase Phase => _phase;
    public bool IsContinuousMode { get; private set; }

    public void Initialize(float width, float height, MatrixTransitionOptions options)
    {
        _screenWidth = width;
        _screenHeight = height;
        _options = options;

        // Only create resources if not already created
        if (_typeface == null)
        {
            // Prefer fonts with good Katakana support, fallback to monospace
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
        }
    }

    public void UpdateSize(float width, float height)
    {
        _screenWidth = width;
        _screenHeight = height;
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
                    // Respawn at top instead of deactivating
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
        if (_font == null || _headPaint == null || _trailPaint == null)
            return;

        foreach (var column in _columns.Where(c => c.IsActive))
        {
            for (int i = 0; i < column.Length; i++)
            {
                float charY = column.Y - (i * _charHeight);

                if (charY < -_charHeight || charY > _screenHeight + _charHeight)
                    continue;

                float alpha = i == 0 ? 1f : Math.Max(0, 1f - (i / (float)column.Length));

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
}
