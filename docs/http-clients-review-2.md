# HTTP clients: second review

Status: implemented. Resolution is recorded below; the finding sections are kept as the rationale.
Previous review: [http-clients-review.md](http-clients-review.md). Usage: [http-clients-usage.md](http-clients-usage.md).
Current code: `Mirai.Space.Extensions.Http.Abstractions`, `.Http`, `.Http.MessagePack`.

## Resolution

- JSON moved into `Mirai.Space.Extensions.Http`; `Mirai.Space.Extensions.Http.Json` is deleted.
  `AddRequestService<TClient>` registers `JsonRequestSerializer` by default; `ConfigureJson()` is only for
  customization.
- Renamed to `RequestResponse` / `RequestResponse<T>` / `RequestException` / `IRequestErrorHandler` /
  `DefaultRequestErrorHandler`; no `Api*` HTTP types remain.
- `IRequestErrorHandler.HandleAsync(RequestException, ct)` only handles (may log or throw); the service
  creates the exception and throws it if the handler returns. `IRequestProblemDetailsReader` enriches the
  exception with `RequestProblemDetails`.
- `AddHttpExtensions` / `AddApiErrorHandler` removed. `RequestService` is a keyed transient resolved by DI;
  per-client overrides use `UseErrorHandler` / `UseSerializer` on `IHttpClientBuilder`.
- `ApiRequest` deleted; `RequestService` uses a private request factory.
- The `disposeRequest` flag and its `try/finally` were removed: every request is disposed by the service.
  Stream methods take a caller-owned `Stream destination` and return `RequestResponse` metadata.
- Content-only methods return the core task directly (no `async`/`await` wrapper). `IRequestService` has
  27 methods, including `PostResponseAsync<TResponse>`, `PostResponseAsync<TResponse, TBody>`,
  `PutResponseAsync<TResponse, TBody>`, `PatchResponseAsync<TResponse, TBody>`, `DeleteResponseAsync<T>`.

Verification: `dotnet build Mirai.Space.slnx -c Debug` = 0 errors; `dotnet test` = 72 passed.

## Findings

### 1. No default serializer

`AddRequestService<TClient>` resolves the serializer as:

```csharp
var serializer = sp.GetKeyedService<IRequestSerializer>(name)
    ?? sp.GetRequiredService<IRequestSerializer>();
```

If the host did not call `ConfigureJson()` / `UseMessagePack()` / `AddJsonSerializer()`, the last line throws
`InvalidOperationException`. The JSON package is optional, so "base" registration has no serializer.

Options:

- **A (recommended).** Make `Mirai.Space.Extensions.Http` the base package: move
  `JsonRequestSerializer`, `ProblemDetailsErrorHandler`, and their DI extensions into it, delete
  `Mirai.Space.Extensions.Http.Json`, and register
  `TryAddSingleton<IRequestSerializer, JsonRequestSerializer>()` inside `AddRequestService`.
  MessagePack stays optional and overrides via `UseMessagePack()`.
- **B.** Keep the three serializer packages, but require an explicit serializer and throw a clear
  message listing `ConfigureJson()` / `UseMessagePack()`. No default.
- **C.** Keep the packages and make core reference `.Http.Json`. This creates a cycle
  (`.Http.Json` already references `.Http`) and is not viable without an intermediate package.

### 2. `Api*` naming

Remaining `Api*` public types:

- `ApiResponse`, `ApiResponse<T>` (`Abstractions/ApiResponse.cs`)
- `ApiException` (`Abstractions/ApiException.cs`)
- `IApiErrorHandler` (`Abstractions/IApiErrorHandler.cs`)
- `DefaultApiErrorHandler` (`Mirai.Space.Extensions.Http/DefaultApiErrorHandler.cs`)

Already `Request*`: `IRequestService`, `IRequestSerializer`, `RequestProblemDetails`,
`JsonRequestSerializer`, `MessagePackRequestSerializer`.

Options:

- **A.** Full `Request*` family: `RequestResponse`, `RequestResponse<T>`, `RequestException`,
  `IRequestErrorHandler`/`IRequestErrorTranslator`, `DefaultRequestErrorHandler`.
- **B.** Rename only the error/exception types, keep `ApiResponse` (it is the API's response DTO).
- **C.** `Http*`: `HttpResponse` collides conceptually with `HttpResponseMessage`; not recommended.

### 3. Error handler semantics

`IApiErrorHandler.HandleAsync(HttpResponseMessage, ct)` returns `ApiException`, and
`RequestService.ThrowIfErrorAsync` throws it:

```csharp
var exception = await _errorHandler.HandleAsync(response, ct);
throw exception;
```

The name says "handle", but the implementation only translates an HTTP response into an exception;
the actual throwing happens in `RequestService`. That is the confusion in the review.

Options:

- **A (recommended).** Name it for what it does: `IRequestErrorTranslator.TranslateAsync(...)` returns
  `RequestException`; `RequestService` throws. Implementations: `DefaultRequestErrorTranslator`
  (status line) and `ProblemDetailsErrorTranslator` (parses `application/problem+json`).
- **B.** Keep `IRequestErrorHandler`, but let it throw: `HandleAsync` returns `ValueTask`; the service
  just awaits it. Tests then assert thrown exceptions. Loses the pure return value and differs from the
  current testable design.
- **C.** Keep the current name/return shape, document that "handler" means "exception factory".

### 4. `AddHttpExtensions`, `new RequestService`, per-service override

- `AddHttpExtensions()` only registers `DefaultApiErrorHandler`; it has no callers other than
  `AddRequestService` itself.
- `RequestService` is constructed with `new` inside the `TClient` factory, not resolved from DI.
- Per-service override already exists in the form of keyed `UseErrorHandler<THandler>()`, but it is
  easy to miss and not tested.

Proposal:

- Delete `AddHttpExtensions()`.
- In `AddRequestService<TClient>`:
  - `services.TryAddSingleton<IRequestErrorHandler, DefaultRequestErrorHandler>();` (or
    `ProblemDetailsErrorTranslator` in the merged base package);
  - `services.AddKeyedTransient<IRequestService>(name, (sp, _) => ...);`
  - `services.TryAddTransient<TClient>(sp => ActivatorUtilities.CreateInstance<TClient>(
        sp, sp.GetRequiredKeyedService<IRequestService>(name)));`
- Add `UseErrorHandler(IRequestErrorHandler instance)` analog of `UseSerializer(instance)`.
- Test that a keyed handler wins over the global one.

### 5. `ApiRequest`

`ApiRequest` is an internal static class with seven `HttpRequestMessage` factories. It is only used by
`RequestService`; the review says it is no longer needed.

Proposal: delete `ApiRequest.cs`, add a private static `CreateRequest(HttpMethod, string,
HttpContent?)` in `RequestService`. This keeps one construction point without a public/internal type.

### 6. Named argument, `disposeRequest`, try/finally

Current call sites use a named argument before a positional one:

```csharp
SendCoreAsync(request, disposeRequest: true, ct);
```

This is a style violation (and confusing to read). The flag exists only to distinguish high-level
methods (which create and own the request) from escape hatches (which borrow the caller's request).

Current core methods:

- `SendCoreAsync(request, ct)` -> delegates with `disposeRequest: true`;
- `SendCoreAsync(request, bool disposeRequest, ct)`;
- `SendCoreAsync<T>(...)` (same pair);
- `SendForStreamCoreAsync(..., bool disposeRequest, ...)`;
- `SendAsyncEnumerableCoreAsync<T>(..., bool disposeRequest, ...)`.

After moving streams to a caller-owned `destination`, the stream `try/finally` only disposes the
request; it adds no value over explicit ownership.

Options:

- **A (recommended).** Remove the boolean. Core methods borrow the request and never dispose it.
  High-level methods own their request with `using var request = CreateRequest(...)` and
  `await` the core call. Streams and enumerables own the request in their iterator/wrapper.
  Cost: metadata/void high-level methods become `async` again, but the ownership rule is explicit
  and there is no flag/named argument.
- **B.** Keep one flag but move it to the end: `SendCoreAsync(request, ct, ownsRequest: false)`.
  Named argument at the end is stylistically acceptable, but the flag remains.
- **C.** Split into `SendOwnedAsync` / `SendBorrowedAsync` families. No flag, but 8 core methods.

### 7. Unnecessary `await` in content-only wrappers

Current pattern in `RequestService.Get.cs`, `.Post.cs`, `.PutPatch.cs`:

```csharp
public async Task<TResponse?> PatchAsync<TResponse, TBody>(string url, TBody body, CancellationToken ct = default) =>
    (await SendCoreAsync<TResponse>(ApiRequest.Patch(url, CreateContent(body)), ct).ConfigureAwait(false)).Content;
```

The `async`/`await` only unwraps `.Content`. Add a core `SendContentAsync<T>` that performs
`SendResponseAsync` + `ReadAsync` and returns `Task<T?>`; content-only methods then return it directly
without an async state machine. `SendCoreAsync<T>` stays for `*ResponseAsync` variants.

### 8. Escape hatches `SendToStreamAsync` / `SendAsyncEnumerable<T>`

These exist for a custom `HttpRequestMessage` when the URL-based methods cannot express the request
(multipart, custom headers, non-JSON body, POST-streaming). They are the stream/enumerable equivalent of
`SendAsync`/`SendAsync<T>`. If `ApiRequest` and custom request scenarios are not needed, they are dead
surface.

Options:

- **A (recommended).** Keep `SendAsync` and `SendAsync<T>` only; drop `SendToStreamAsync` and
  `SendAsyncEnumerable<T>`. High-level `DownloadAsync`, `PostToStreamAsync`, `GetAsyncEnumerable` cover
  the common cases.
- **B.** Keep all four; document them as advanced escape hatches.
- **C.** Drop all four; `IRequestService` is URL-only.

### 9. Method-surface standardisation

Current `IRequestService` (`Abstractions/IRequestService.cs`) matrix:

| Verb | No body/content | Content `T?` | Metadata `ApiResponse<T>` | Stream | Enumerable |
| --- | --- | --- | --- | --- | --- |
| GET | - | `GetAsync<T>` | `GetResponseAsync<T>` | `DownloadAsync` | `GetAsyncEnumerable<T>` |
| HEAD | - | - | `HeadAsync` (`ApiResponse`) | - | - |
| OPTIONS | - | - | `OptionsAsync` (`ApiResponse`) | - | - |
| POST | `PostAsync`, `PostAsync<TBody>` | `PostAsync<T>`, `PostAsync<TResponse,TBody>` | `PostResponseAsync<TResponse,TBody>` | `PostToStreamAsync`, `PostToStreamAsync<TBody>` | - |
| PUT | `PutAsync<TBody>` | `PutAsync<TResponse,TBody>` | **missing** | - | - |
| PATCH | `PatchAsync<TBody>` | `PatchAsync<TResponse,TBody>` | **missing** | - | - |
| DELETE | `DeleteAsync` | `DeleteAsync<T>` | **missing** | - | - |
| Send | `SendAsync` (`ApiResponse`) | - | `SendAsync<T>` (`ApiResponse<T>`) | `SendToStreamAsync` | `SendAsyncEnumerable<T>` |

Gaps and inconsistencies:

1. POST without body has no metadata variant: add `PostResponseAsync<TResponse>(url, ct)`.
2. PUT/PATCH have no metadata variant: add `PutResponseAsync<TResponse,TBody>`,
   `PatchResponseAsync<TResponse,TBody>`.
3. DELETE has no metadata variant: add `DeleteResponseAsync<T>(url, ct)`.
4. `SendAsync<T>` returns metadata (`ApiResponse<T>`), while `GetAsync<T>`/`PostAsync<T>` return
   content. This is an escape-hatch asymmetry; either document it or add `SendContentAsync<T>`.
5. `SendAsyncEnumerable<T>` is named differently from `GetAsyncEnumerable<T>`; rename to
   `SendEnumerableAsync<T>` if kept.
6. `PostResponseAsync` name is inconsistent with `GetResponseAsync` only in prefix; `*ResponseAsync`
   is the right convention across verbs.
7. `PostAsync<TBody>` vs `PostAsync<TResponse,TBody>` is distinguishable by arity but easy to
   misread; document the "content vs void" meaning.
8. No PUT/PATCH/DELETE stream variants; not required unless the API needs them.

### 10. Other observations

- `AddApiErrorHandler<THandler>` uses `AddSingleton`, so repeated calls add multiple descriptors and
  `GetRequiredService` returns the last one. Prefer `Replace`/`RemoveAll` or explicit last-wins
  documentation.
- `UseSerializer`/`UseErrorHandler` use `TryAddKeyedSingleton` (first wins). This is deliberate but
  should be documented next to the methods.
- `JsonRequestSerializer` stores the caller's `JsonSerializerOptions` reference; documented.
- `ProblemDetailsErrorHandler` size cap returns `null` (no partial parse); documented.
- `ApiResponse` is a non-abstract public class with an internal ctor; fine, but if renamed, update
  `ApiResponse<T>` inheritance and every test.
- `RequestService` constructor is internal; after keyed DI registration this stays internal.
- `DefaultApiErrorHandler` includes method/URI from `response.RequestMessage`; `RequestService` sets
  `response.RequestMessage ??= request`, so custom handlers are covered.
- `HttpCompletionOption.ResponseHeadersRead` is used for all requests; for small buffered JSON this is
  correct but not optimal. Not part of this review.

## Proposed target shape

Projects:

- `Mirai.Space.Extensions.Http.Abstractions` — contracts and DTOs.
- `Mirai.Space.Extensions.Http` — base: `RequestService`, JSON serializer, ProblemDetails translator,
  default error translator, DI. (JSON package merged.)
- `Mirai.Space.Extensions.Http.MessagePack` — optional.

Public names (if option A is chosen):

- `RequestResponse`, `RequestResponse<T>`, `RequestException`;
- `IRequestErrorTranslator.TranslateAsync`, `DefaultRequestErrorTranslator`,
  `ProblemDetailsErrorTranslator`;
- `IRequestService`, `IRequestSerializer` unchanged.

DI:

```csharp
services.AddRequestService<AboutRequestService>()
    .ConfigureJson()                 // optional, default is already JSON
    .UseErrorTranslator<MyTranslator>(); // per named client, optional
```

Default registrations inside `AddRequestService`:

- `TryAddSingleton<IRequestSerializer, JsonRequestSerializer>()`;
- `TryAddSingleton<IRequestErrorTranslator, ProblemDetailsErrorTranslator>()` (or default);
- keyed `IRequestService` for the client name;
- transient `TClient` from the keyed service.

Core send methods:

- `SendAsync(HttpRequestMessage, ct)` -> `RequestResponse`;
- `SendAsync<T>(HttpRequestMessage, ct)` -> `RequestResponse<T>`;
- `SendContentAsync<T>(HttpRequestMessage, ct)` -> `T?`;
- `SendToStreamAsync(HttpRequestMessage, Stream, ct)` -> `RequestResponse`;
- `SendEnumerableAsync<T>(HttpRequestMessage, ct)` -> `IAsyncEnumerable<T>`;
- ownership handled by the caller (high-level `using var request`, escape hatches borrow).

## Resolved decisions

1. **Merge JSON into the base package.** `Mirai.Space.Extensions.Http` becomes the base package with
   `JsonRequestSerializer`, `RequestProblemDetailsReader`, default error handling, and DI. The
   `Mirai.Space.Extensions.Http.Json` project is deleted. MessagePack remains optional.
2. **Full `Request*` naming.** `RequestResponse`/`RequestResponse<T>`, `RequestException`,
   `IRequestErrorHandler`/`DefaultRequestErrorHandler`; no `Api*` public types remain.
3. **Handler handles an exception, it does not create it.** The service creates `RequestException`
   from the response; an optional `IRequestProblemDetailsReader` enriches it with
   `RequestProblemDetails`; `IRequestErrorHandler.HandleAsync(RequestException, CancellationToken)`
   performs side effects and may rethrow. The service always throws the exception afterwards.
   `DefaultRequestErrorHandler` is a no-op hook; `RequestProblemDetailsReader` is the default reader.
4. **Keep all four escape hatches.** `SendAsync`, `SendAsync<T>`, `SendToStreamAsync`,
   `SendAsyncEnumerable<T>`.
5. **Always dispose the request.** There is no `disposeRequest` parameter and no ownership split.
   High-level methods and escape hatches both hand the request to the service, which disposes it.
   Callers must not reuse a request after passing it to `IRequestService`.
6. **Add all four metadata variants.** `PostResponseAsync<TResponse>`, `PutResponseAsync`,
   `PatchResponseAsync`, `DeleteResponseAsync<T>`.

## Questions to resolve before coding

None; decisions above are final for this pass.
