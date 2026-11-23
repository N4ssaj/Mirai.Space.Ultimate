# Mirai.Space.Extensions.Clone

> Experimental API: the surface may change while the strategy and generator rules settle.

Shallow and deep graph cloning with three resolution paths:

1. an incremental source-generated plan for `[Cloneable]` types;
2. a PolyType plan when the type exposes `[GenerateShape]`;
3. a cached Expression/reflection fallback for everything else.

## Usage

```csharp
var shallow = Clone.Shallow(order);   // new root, shared nested references
var deep = Clone.Deep(order);         // new object graph
var deepAgain = order.DeepClone();    // C# 14 extension block
```

`Clone.Shallow` / `Clone.Deep` are the explicit entry points. Prefer them over a
global `Clone()` extension: `ICloneable`, `Array.Clone`, and hand-written methods
would otherwise collide.

## Attributes

```csharp
[Cloneable]
public sealed class Order
{
    public Address? ShippingAddress { get; set; }

    [CloneIgnore]
    public Address? CachedAddress { get; set; }
}
```

The generator emits a module initializer that registers a `ClonePlan<Order>`.
It only does so when all declared state is accessible and writable; otherwise the
runtime falls back automatically. Init-only properties, readonly fields, private
state, base classes, generics, and interfaces are intentionally left to the
fallback paths.

`[Cloneable(GenerateCloneMethod = true)]` additionally emits instance
`Clone()` (shallow) and `DeepClone()` (deep) methods. The type must be `partial`
and must not already declare members with those names; otherwise the generator
reports `MSCLONE003` and only the plan is registered.

## Semantics

- Shallow: `MemberwiseClone` for objects; arrays and common collections get a new
  container with the same element/key/value references.
- Deep: `MemberwiseClone`, then every writable member is cloned through the same
  context; cycles are preserved through a reference map.
- Immutable values (primitives, enums, `string`, `DateTime`, `Guid`, `Uri`,
  delegates, tasks, streams, locks, etc.) are returned as-is.
- Arrays, `IDictionary`, `IList`, and generic `ICollection<T>`/`ISet<T>` are
  recreated and their elements cloned.
- Read-only collections that cannot be recreated share their backing state; this
  is a deliberate fallback rather than a partially cloned container.
- Structs are copied; reference members inside them are cloned when the plan can
  reach them. Cycles that pass only through structs are not tracked.

## Strategies

`Clone.GetStrategy<T>()` reports the selected path for diagnostics and tests:
`SourceGenerated`, `PolyType`, `Expression`, or `RuntimeDispatch` for
interfaces/abstract types/collections.
