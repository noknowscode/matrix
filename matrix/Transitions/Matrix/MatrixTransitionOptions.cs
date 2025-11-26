using SkiaSharp;

namespace matrix.Transitions.Matrix;

public record MatrixTransitionOptions
{
    public TimeSpan TotalDuration { get; init; } = TimeSpan.FromMilliseconds(1400);

    public float RainInRatio { get; init; } = 0.43f;
    public float PeakRatio { get; init; } = 0.21f;
    public float RainOutRatio { get; init; } = 0.36f;

    public SKColor CharacterColor { get; init; } = new(0, 255, 70);
    public SKColor GlowColor { get; init; } = SKColors.White;

    // Katakana + digits + Latin - using standard Unicode that most fonts support
    public string CharacterSet { get; init; } =
        "アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲン" +
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public int ColumnSpacing { get; init; } = 6;
    public int MinTrailLength { get; init; } = 15;
    public int MaxTrailLength { get; init; } = 45;
    public float MinSpeed { get; init; } = 200f;
    public float MaxSpeed { get; init; } = 600f;
    public float MutationIntervalMs { get; init; } = 80f;
    public float FontSize { get; init; } = 14f;

    public TimeSpan RainInDuration => TotalDuration * RainInRatio;
    public TimeSpan PeakDuration => TotalDuration * PeakRatio;
    public TimeSpan RainOutDuration => TotalDuration * RainOutRatio;
}
