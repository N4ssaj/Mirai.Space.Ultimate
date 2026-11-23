# GOTCHAS

Append-only. Format: date | version | repro | verified fix. Never record an unverified guess.

## 2026-11-23 | .NET 10 / C# 14 | sync-over-async disposal

**NEVER write** `disposable.DisposeAsync().AsTask().GetAwaiter().GetResult();` — not in `Dispose()`,
not in a `finally`, not "just this once".

Repro: a synchronous `Dispose()` that blocks on async disposal stalls the calling thread (UI thread
deadlock, thread-pool starvation) and turns an idempotent async contract into a fragile one.

Fix: keep disposal async all the way down — `await disposable.DisposeAsync().ConfigureAwait(false)`.
When a type must expose both interfaces, the sync path disposes only `IDisposable` items and throws
through `ThrowHelper` (with the message to call `DisposeAsync`) when it meets an
`IAsyncDisposable`-only item. Reference implementation:
`Mirai.Space.Extensions.MVVM\LifeTime\AsyncLifeTime.cs`.

## 2026-11-23 | .NET 9+ | `System.Threading.Lock`, not `object` gates

Use `private readonly Lock _gate = new();` with `lock (_gate)`; do not introduce new
`private readonly object _gate = new();` fields. `Lock` is the dedicated gateway type
(`EnterScope()`, cheaper fast path) and reads `IsDisposed` through `Volatile.Read` instead of
locking on the hot path.

## 2026-11-23 | .NET 10 / C# 14 | `[DoesNotReturn]` does not satisfy definite assignment

A `ThrowHelper` method marked `[DoesNotReturn]` only updates nullable flow state; the compiler still
reports CS0161/CS0165 when a branch ends in the call. Repro: `if (x is not string s) { Throw(); } s.Length;`
with `[DoesNotReturn] static void Throw()`. Fix: follow each ThrowHelper call in a value-returning
branch with an explicit `return`/`return default!`.

## 2026-11-23 | .NET 10 | `TypeDescriptor.AddProvider` does not set the provider parent

`TypeDescriptor.AddProvider` only prepends a `TypeDescriptionNode`; it never assigns the provider's
protected parent. A `TypeDescriptionProvider` created without a parent therefore returns its empty
descriptor for types it has no shape/metadata for, hiding reflection metadata. Fix: default the parent
to `TypeDescriptor.GetProvider(typeof(object))` in the convenience constructor and keep an explicit
`parent: null` overload for a deliberately isolated provider. Reference:
`Experimental\Mirai.Space.Extensions.ComponentModel\Descriptors\PolyTypeTypeDescriptionProvider.cs`.

## 2026-11-23 | PolyType 2.0.0-rc.4 | events and methods are opt-in in shapes

`ITypeShape.Events` (and `.Methods`) stays empty unless the type is annotated with
`[TypeShape(IncludeMethods = MethodShapeFlags.PublicInstance)]` (or an individual `[EventShape]` is
applied). Repro: a class with `public event EventHandler? Changed` plus `[GenerateShapeFor<T>]` produced
`Events.Count == 0` for both the source-generated and `ReflectionTypeShapeProvider.Default` shapes.
Consequence: a `TypeDescriptor` replacement must fall back to the parent descriptor when a shape has no
events, otherwise registering the provider hides the type's events.

## 2026-11-23 | PolyType 2.0.0-rc.4 | positional-record init properties have no shape setter

`IPropertyShape.HasSetter` is `false` for positional-record init properties even in a
`[GenerateShape]` shape. Repro: `[GenerateShape] partial record Rec(string Name, Address Address);`
printed `Name: get=True set=False`, `Address: get=True set=False`. A clone plan built only from
properties with setters would therefore silently shallow-share them. Fix: reject the PolyType plan
when any readable shape property lacks a setter and fall through to the field-based Expression plan,
which assigns the readonly backing fields through `FieldInfo.SetValue`.

## 2026-11-23 | .NET 10 | `Expression.Assign` cannot write a readonly field

Repro: `Expression.Assign(Expression.Field(...), ...)` for a `private readonly` field throws
`ArgumentException: Expression must be writeable (Parameter 'left')`. `FieldInfo.SetValue` on the same
field succeeds, and `Expression.Call` to an `init`-only property setter also succeeds. Fix: use
reflection for readonly fields in the Expression fallback; use setters only for init-only properties.
