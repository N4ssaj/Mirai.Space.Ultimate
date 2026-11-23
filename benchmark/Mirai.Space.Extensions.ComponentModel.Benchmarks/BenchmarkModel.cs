using PolyType;

namespace Mirai.Space.Extensions.ComponentModel.Benchmarks;

public sealed class BenchmarkModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public double Value { get; set; }

    public bool Flag { get; set; }

    public DateTime Timestamp { get; set; }
}

[GenerateShapeFor<BenchmarkModel>]
internal partial class BenchmarkWitness;
