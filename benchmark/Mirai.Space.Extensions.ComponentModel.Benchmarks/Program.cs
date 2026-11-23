using System.ComponentModel;
using System.Diagnostics;
using Mirai.Space.Extensions.ComponentModel;

namespace Mirai.Space.Extensions.ComponentModel.Benchmarks;

internal static class Program
{
    private static long _sink;

    private static void Main()
    {
        var model = new BenchmarkModel
        {
            Id = 1,
            Name = "alpha",
            Value = 3.14,
            Flag = true,
            Timestamp = DateTime.UnixEpoch,
        };

        var provider = PolyTypeTypeDescriptionProvider.For<BenchmarkModel, BenchmarkWitness>();
        var polyTypeDescriptor = provider.GetTypeDescriptor(typeof(BenchmarkModel), null)
            ?? throw new InvalidOperationException("PolyType provider returned no descriptor.");
        var polyProperties = polyTypeDescriptor.GetProperties();
        var reflectionProperties = TypeDescriptor.GetProperties(typeof(BenchmarkModel));

        AssertSamePropertyNames(polyProperties, reflectionProperties);
        AssertRoundTrip(polyProperties, model);
        AssertRoundTrip(reflectionProperties, model);

        Console.WriteLine($"PolyType shape properties: {polyProperties.Count}, reflection properties: {reflectionProperties.Count}");
        Console.WriteLine();
        Console.WriteLine($"{"benchmark",-46} {"ns/op",10} {"B/op",10}");
        Console.WriteLine(new string('-', 68));

        const int metadataIterations = 200_000;
        Measure("GetProperties: reflection", metadataIterations, metadataIterations, count =>
        {
            for (var i = 0; i < count; i++)
            {
                _sink += TypeDescriptor.GetProperties(typeof(BenchmarkModel)).Count;
            }
        });

        Measure("GetProperties: PolyType", metadataIterations, metadataIterations, count =>
        {
            for (var i = 0; i < count; i++)
            {
                var descriptor = provider.GetTypeDescriptor(typeof(BenchmarkModel), null)!;
                _sink += descriptor.GetProperties().Count;
            }
        });

        const int accessIterations = 200_000;
        var operations = (long)accessIterations * reflectionProperties.Count;

        Measure("GetValue: reflection", operations, accessIterations, count =>
        {
            for (var i = 0; i < count; i++)
            {
                for (var propertyIndex = 0; propertyIndex < reflectionProperties.Count; propertyIndex++)
                {
                    Accumulate(reflectionProperties[propertyIndex].GetValue(model));
                }
            }
        });

        Measure("GetValue: PolyType", operations, accessIterations, count =>
        {
            for (var i = 0; i < count; i++)
            {
                for (var propertyIndex = 0; propertyIndex < polyProperties.Count; propertyIndex++)
                {
                    Accumulate(polyProperties[propertyIndex].GetValue(model));
                }
            }
        });

        Measure("SetValue: reflection", operations, accessIterations, count =>
        {
            for (var i = 0; i < count; i++)
            {
                for (var propertyIndex = 0; propertyIndex < reflectionProperties.Count; propertyIndex++)
                {
                    reflectionProperties[propertyIndex].SetValue(model, BenchmarkValues[propertyIndex]);
                }
            }
        });

        Measure("SetValue: PolyType", operations, accessIterations, count =>
        {
            for (var i = 0; i < count; i++)
            {
                for (var propertyIndex = 0; propertyIndex < polyProperties.Count; propertyIndex++)
                {
                    polyProperties[propertyIndex].SetValue(model, BenchmarkValues[propertyIndex]);
                }
            }
        });

        TypeDescriptor.AddProvider(provider, typeof(BenchmarkModel));
        try
        {
            Measure("GetProperties: PolyType via TypeDescriptor", metadataIterations, metadataIterations, count =>
            {
                for (var i = 0; i < count; i++)
                {
                    _sink += TypeDescriptor.GetProperties(typeof(BenchmarkModel)).Count;
                }
            });
        }
        finally
        {
            TypeDescriptor.RemoveProvider(provider, typeof(BenchmarkModel));
        }

        Console.WriteLine(new string('-', 68));
        Console.WriteLine($"sink: {_sink}");
    }

    private static readonly object?[] BenchmarkValues =
    [
        7,
        "beta",
        2.5,
        false,
        new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    ];

    private static void Measure(string name, long operations, int iterations, Action<int> action)
    {
        // Two full warm-up passes promote the benchmark methods out of tier-0 before measuring.
        action(iterations);
        action(iterations);

        var samples = new List<(double Nanoseconds, double Bytes)>(7);
        for (var round = 0; round < 7; round++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            action(iterations);
            var elapsed = Stopwatch.GetElapsedTime(started);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

            samples.Add((
                elapsed.TotalNanoseconds / operations,
                (double)allocated / operations));
        }

        samples.Sort((left, right) => left.Nanoseconds.CompareTo(right.Nanoseconds));
        var median = samples[samples.Count / 2];
        Console.WriteLine($"{name,-46} {median.Nanoseconds,10:F2} {median.Bytes,10:F2}");
    }

    private static void AssertSamePropertyNames(PropertyDescriptorCollection left, PropertyDescriptorCollection right)
    {
        if (left.Count != right.Count)
        {
            throw new InvalidOperationException($"Property count mismatch: {left.Count} vs {right.Count}.");
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index].Name, right[index].Name, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Property name mismatch at {index}: {left[index].Name} vs {right[index].Name}.");
            }
        }
    }

    private static void AssertRoundTrip(PropertyDescriptorCollection properties, BenchmarkModel model)
    {
        var name = properties[nameof(BenchmarkModel.Name)]
            ?? throw new InvalidOperationException("The Name property descriptor is missing.");
        name.SetValue(model, "round-trip");
        if (!string.Equals(name.GetValue(model) as string, "round-trip", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Round-trip failed for {name.Name}.");
        }
    }

    private static void Accumulate(object? value) => _sink += value switch
    {
        int integer => integer,
        string text => text.Length,
        double number => (long)number,
        bool flag => flag ? 1 : 0,
        DateTime timestamp => timestamp.Ticks,
        _ => 0,
    };
}
