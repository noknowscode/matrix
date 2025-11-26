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
