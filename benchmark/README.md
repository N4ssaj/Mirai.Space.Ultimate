# ComponentModel benchmarks

Compares the reflection-based `TypeDescriptor` path with `PolyTypeTypeDescriptionProvider`.

The scenario mirrors what an item-control grid does:

- resolve a property collection for a row type (`TypeDescriptor.GetProperties`);
- read every property of a row through the resolved `PropertyDescriptor`s;
- write every property of a row through the same descriptors.

The benchmark uses `Stopwatch` and `GC.GetAllocatedBytesForCurrentThread`; each case runs two
full warm-up passes and seven measured rounds, and the table prints the median. Per-operation
numbers divide by the number of logical operations (property reads/writes, or metadata lookups).

## Run

```powershell
dotnet run --project benchmark/Mirai.Space.Extensions.ComponentModel.Benchmarks -c Release
```

## Sample result

Machine: Windows, .NET 10, Release. Numbers are indicative; compare ratios, not absolutes.

| Benchmark | Reflection | PolyType | Ratio |
| --- | ---: | ---: | ---: |
| `GetProperties`, direct provider | ~92 ns/op | ~9.5 ns/op | ~10x faster |
| `GetProperties`, via `TypeDescriptor` | ~91 ns/op | ~131 ns/op | ~0.7x |
| `GetValue` | ~22 ns/op | ~16 ns/op | ~1.4x faster |
| `SetValue` | ~68 ns/op, 32 B/op | ~41 ns/op, 0 B/op | ~1.7x faster |

`GetValue` allocates the same ~19 B/op on both paths because value-type properties are boxed
when returned as `object`. `SetValue` on the reflection path allocates an arguments array per
call; the PolyType descriptor writes through a generated setter delegate.

The `TypeDescriptor`-dispatch row matters: registering the provider is the normal integration
path, and the global `TypeDescriptor` machinery costs more than the direct provider call. The
metadata win is therefore mostly one-time (column setup), while per-cell reads and writes keep
their advantage.

## Caveats

- The benchmark measures the `System.ComponentModel` contract only. A real grid adds layout,
  templates, and visual-tree work, so the end-to-end gain is smaller than the per-call ratios.
- The benchmark keeps descriptors warm, like a grid that caches columns. First-call shape
  generation and descriptor construction are not measured.
- Reflection here means `TypeDescriptor`/`PropertyDescriptor` over `PropertyInfo`, not raw
  `PropertyInfo.GetValue`.
- The same row instance is reused, so row-object cache effects are not measured.
