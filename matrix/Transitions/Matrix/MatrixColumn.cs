namespace matrix.Transitions.Matrix;

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
