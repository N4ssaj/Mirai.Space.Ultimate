# HTTP clients review 3 — Abstractions and Http

Status: discussion only. No production code or test edits until the decisions below are approved.

Scope: `Mirai.Space.Extensions.Http.Abstractions` and `Mirai.Space.Extensions.Http`.
`Mirai.Space.Extensions.Http.MessagePack` is approved and out of scope.

## 1. Abstractions

### A1. `IRequestService` method order and regions — done

Regions added (m02228): GET, HEAD / OPTIONS, POST, PUT, PATCH, DELETE, Escape hatches. This intentionally
overrides the no-regions style rule because the interface is hard to scan otherwise.

The method order below is still the proposed one; the current file keeps the existing order inside the
regions. Reordering is cosmetic and can be applied on request.

| # | Method |
| --- | --- |
| 1 | `GetAsync<T>(url, ct)` |
| 2 | `GetResponseAsync<T>(url, ct)` |
| 3 | `DownloadAsync(url, destination, ct)` |
| 4 | `GetAsyncEnumerable<T>(url, ct)` |
| 5 | `HeadAsync(url, ct)` |
| 6 | `OptionsAsync(url, ct)` |
| 7 | `PostAsync<T>(url, ct)` |
| 8 | `PostAsync<TResponse, TBody>(url, body, ct)` |
| 9 | `PostAsync(url, ct)` |
| 10 | `PostAsync<TBody>(url, body, ct)` |
| 11 | `PostResponseAsync<TResponse>(url, ct)` |
| 12 | `PostResponseAsync<TResponse, TBody>(url, body, ct)` |
| 13 | `PostToStreamAsync(url, destination, ct)` |
| 14 | `PostToStreamAsync<TBody>(url, body, destination, ct)` |
| 15 | `PutAsync<TResponse, TBody>(url, body, ct)` |
| 16 | `PutAsync<TBody>(url, body, ct)` |
| 17 | `PutResponseAsync<TResponse, TBody>(url, body, ct)` |
| 18 | `PatchAsync<TResponse, TBody>(url, body, ct)` |
| 19 | `PatchAsync<TBody>(url, body, ct)` |
| 20 | `PatchResponseAsync<TResponse, TBody>(url, body, ct)` |
| 21 | `DeleteAsync(url, ct)` |
| 22 | `DeleteAsync<T>(url, ct)` |
| 23 | `DeleteResponseAsync<T>(url, ct)` |
| 24 | `SendAsync(request, ct)` |
| 25 | `SendAsync<T>(request, ct)` |

Within each verb: content-only, then content+metadata, then metadata-only, then stream. Escape hatches stay last.

### A2. `RequestResponse.TryGetHeader` — done

Method removed (m02228). The OPTIONS test now uses
`response.ContentHeaders.TryGetValue("Allow", out var allow)`.

### A3. `RequestProblemDetails` — deferred, removed for now

Decision (m02134): remove ProblemDetails parsing from the HTTP layer for now and revisit after the rest of
the API is settled. Deleted `RequestProblemDetails`, `IRequestProblemDetailsReader`,
`RequestProblemDetailsReader`, `RequestException.Problem`, and `AddProblemDetailsReader`; errors now carry
only the status line and request coordinates.

Original question (kept for the future):

Current shape is the standard five-field problem document:

```csharp
public sealed record RequestProblemDetails(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    string? Instance);
```

RFC 9457 also allows arbitrary extension members, and an API may return its own `Details` object. Questions:

- Do we need to preserve arbitrary extension members, or only the standard five fields?
- If yes, what shape: `IReadOnlyDictionary<string, JsonElement>? Extensions` (needs `System.Text.Json` in Abstractions), a typed `Details` string/object, or the raw JSON body?
- Is a nested custom `Details` more important than keeping Abstractions free of `System.Text.Json`?

Options considered:

1. Keep standard five fields; unknown members are dropped. No dependency change.
2. Add `IReadOnlyDictionary<string, JsonElement>? Extensions`. Abstractions gains `System.Text.Json`.
3. Add `string? RawJson` (or bring back `RequestException.ResponseBody`). Simple, but callers parse it themselves.
4. Move `RequestProblemDetails` to the Http implementation and expose only a non-generic problem marker in Abstractions. Breaks the current `RequestException.Problem` contract.

### A4. `RequestResponse` public constructor and mutable header arrays — done

Applied (m03307): header values are `IReadOnlyDictionary<string, IReadOnlyList<string>>`; the
constructor stores the caller's dictionaries without copying (no duplicate copy), and
`RequestResponseExtensions` performs the single copy from `HttpHeadersNonValidated`. The constructor
stays public — it takes plain status/headers and is the price of dropping `InternalsVisibleTo`.

### A5. JSON streaming null contract — open

`JsonRequestSerializer.ReadJsonArrayAsync` throws `JsonException` for a `null` array element. For `T` that is nullable this is arguably too strict; the alternative is `IAsyncEnumerable<T?>` or silently skipping nulls (rejected earlier).
No change proposed until the contract decision is confirmed.

## 2. Http

### H1. Request disposal is hidden inside `SendCoreAsync` — user complaint

**Applied (m02270, refined m02450):** every public method now creates and disposes the request at its own
declaration site (`using var request = CreateRequest(...)`), and the core methods no longer contain
`using (request)`. Escape hatches take the caller's message and bind it with `using var request = message`
(no block statement). The `disposeRequest` flag and `try/finally` are gone.

Current:

```csharp
private async Task<RequestResponse> SendCoreAsync(HttpRequestMessage request, CancellationToken ct)
{
    ArgumentNullException.ThrowIfNull(request);
    using (request) { ... }
}
```

The public method declares/creates the request, then a different private method disposes it — ownership is not visible at the declaration site. Proposal:

- High-level methods create the request in a local and own it:

```csharp
public async Task<T?> GetAsync<T>(string url, CancellationToken ct = default)
{
    using var request = CreateRequest(HttpMethod.Get, url);
    return await SendAndReadAsync<T>(request, ct).ConfigureAwait(false);
}
```

- Core methods never dispose the request; they only send/read/snapshot.
- Escape hatches never dispose a caller-provided request (caller owns it).
- To avoid 27 copies of `using var request`, each verb gets one private helper that declares/disposes it, or the core accepts `Func<HttpRequestMessage>` and creates the request itself (then disposal is also at the declaration site, inside the core). Prefer the explicit `using` in each public method/helper.

This also removes the last reason for a `disposeRequest` flag.

### H2. `SendResponseAsync` disposes the response in `catch` — user complaint

**Applied (m02270, refined m02450):** the method is now `SendCoreAsync` — pure send, no `catch`, no
`RequestMessage` mutation. Each core method declares `using var response = await SendCoreAsync(...)` and
then calls `ThrowIfErrorAsync(response, request, ct)`, so a throw disposes the response via `using`;
`response.ToRequestException(request)` takes the original request for method/URI.

Current:

```csharp
try
{
    response.RequestMessage ??= request;
    await ThrowIfErrorAsync(response, ct);
    return response;
}
catch
{
    response.Dispose();
    throw;
}
```

Proposal: make `SendResponseAsync` pure transport and move error handling to the caller, where the response is already in a `using`:

```csharp
private async ValueTask<HttpResponseMessage> SendForResponseAsync(
    HttpRequestMessage request,
    CancellationToken ct)
{
    var response = await _httpClient
        .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
        .ConfigureAwait(false);

    response.RequestMessage ??= request;
    return response;
}
```

```csharp
private async Task<RequestResponse> SendAndSnapshotAsync(
    HttpRequestMessage request,
    CancellationToken ct)
{
    using var response = await SendForResponseAsync(request, ct).ConfigureAwait(false);
    await ThrowIfErrorAsync(response, ct).ConfigureAwait(false);
    return RequestResponseFactory.FromMessage(response);
}
```

If `ThrowIfErrorAsync` throws, the `using` disposes the response; no `catch`/manual `Dispose` remains. All five core methods follow the same shape.

### H3. Core method names are unclear — user complaint

**Applied (m02270, refined m02450):** raw send is `SendCoreAsync`, `ReadAsync<T>` is `ReadContentAsync<T>`;
the remaining core names are `SendResponseCoreAsync`, `SendResponseCoreAsync<T>`,
`SendContentCoreAsync<T>`, `SendStreamCoreAsync`, `SendAsyncEnumerableCoreAsync<T>`.

Current names and actual meaning:

| Current | Meaning |
| --- | --- |
| `SendResponseAsync` | sends and returns the raw `HttpResponseMessage` (headers only, no body read) |
| `SendCoreAsync` | sends, reads nothing, returns `RequestResponse` metadata |
| `SendCoreAsync<T>` | sends, deserializes content, returns `RequestResponse<T>` |
| `SendContentCoreAsync<T>` | sends, deserializes content, returns only `T?` |
| `SendStreamCoreAsync` | sends, copies body into `destination`, returns metadata |
| `SendAsyncEnumerableCoreAsync<T>` | sends, streams deserialized items, returns metadata via iterator |
| `ReadAsync<T>` | checks for empty body, deserializes content |

Proposed rename (wording can change):

| Current | Proposed |
| --- | --- |
| `SendResponseAsync` | `SendForResponseAsync` |
| `SendCoreAsync` | `SendAndSnapshotAsync` |
| `SendCoreAsync<T>` | `SendAndReadResponseAsync<T>` |
| `SendContentCoreAsync<T>` | `SendAndReadAsync<T>` |
| `SendStreamCoreAsync` | `SendAndCopyAsync` |
| `SendAsyncEnumerableCoreAsync<T>` | `SendAndStreamAsync<T>` |
| `ReadAsync<T>` | `ReadContentAsync<T>` |

### H4. `Accept` is never set — finding

`ConfigureJson` / `UseMessagePack` set the serializer for `Content-Type`, but not `Accept`. A MessagePack client therefore asks for JSON unless the caller sets `Accept` manually, while the server may honor `Accept` and return JSON.

Options:

1. In each `Use*`, call `builder.ConfigureHttpClient(c => c.DefaultRequestHeaders.Accept.Add(...))` with the serializer's media type (`application/json` / `application/x-msgpack`).
2. Add a `ContentType`-driven default `Accept` on `AddRequestService` (JSON), overridden per client.
3. Leave to the caller and document.

Prefer 1 plus a JSON default in `AddRequestService`; needs confirmation that no API intentionally returns a different format than it accepts.

### H5. `HttpClient.Timeout` does not cover body reads — finding

Every send uses `HttpCompletionOption.ResponseHeadersRead`; according to the .NET docs the `HttpClient.Timeout` then applies to headers only. A stalled body read can hang forever unless the caller passes a `CancellationToken` that fires.

Options:

1. Use `ResponseContentRead` for non-streaming methods and `ResponseHeadersRead` only for stream/enumerable. Timeout then covers the whole buffered response.
2. Keep `ResponseHeadersRead` everywhere and require callers to pass a per-request timeout token; document.
3. Add an optional per-request timeout parameter to `IRequestService` (big API change).

Prefer 1: split `SendForResponseAsync` by completion option, or pass the option into it.

### H6. DI semantics — resolved

- `AddJsonSerializer`, `AddRequestErrorHandler`: removed; JSON/error handling defaults are registered by
  `AddRequestService`, global fallback replacement is a direct DI registration.
- `UseSerializer`, `UseErrorHandler`: `TryAddKeyedSingleton` (first wins per named client).
- `AddProblemDetailsReader`: removed with ProblemDetails.

### H7. Lazy async enumerable can leak the request — applied

High-level `GetAsyncEnumerable` creates and disposes the request inside the iterator; there is no generic
`SendAsyncEnumerable<T>` escape hatch anymore (m03307).

### H8. Empty chunked body is treated as an error — applied

`ReadResponseAsync<T>` returns `default` for 204/205, `ContentLength == 0`, and a seekable zero-length content stream (covers a buffered empty chunked body). Tested by `GetAsync_EmptyUnknownLengthBody_ReturnsDefault`.

### H9. Stream method names — applied

Chosen names: `DownloadAsync`, `PostToStreamAsync`, `PostToStreamAsync<TBody>`; all copy the body into a
caller-supplied destination and return `RequestResponse` metadata. The generic `SendToStreamAsync` escape
hatch was removed (m03307).

### H10. Header snapshot values are mutable — applied

`RequestResponse` stores the caller's dictionaries without copying; the single copy from
`HttpHeadersNonValidated` is done by `RequestResponseExtensions.CloneHeaders`. Header values are
`IReadOnlyList<string>` so consumers cannot mutate them.

## 3. Test impact

- ProblemDetails removal (m02134): `DiTests` now asserts `RequestException.StatusCode` only.
- `TryGetHeader` removal (m02228): `AboutRequestServiceTests` uses `ContentHeaders.TryGetValue`.
- No other tests depend on the removed surfaces.

## 4. Decisions needed

1. `IRequestService` order — confirm the table in A1 (regions are already added).
2. `RequestProblemDetails` — deferred and removed for now (m02134); revisit after the rest of the API.
3. `RequestResponse` construction — keep public ctor or introduce a plain-data factory.
4. `SendForResponseAsync` / `SendAndSnapshotAsync` / `ReadContentAsync` naming — approve or propose different names.
5. Disposal model — high-level methods own the request; core never disposes; escape hatches caller-owned.
6. `Accept` — set per serializer or leave to callers.
7. `ResponseContentRead` for non-streaming methods — yes/no.
8. Empty chunked body — return `default`, peek, or document as invalid.
9. Stream method names — which naming family.
