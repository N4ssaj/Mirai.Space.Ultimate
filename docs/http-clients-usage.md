# HTTP clients: usage guide

Status: implemented in `Mirai.Space.Extensions.Http` / `.Http.MessagePack`.
Design rationale and open questions live in [http-clients-proposal.md](http-clients-proposal.md).

## Projects

| Project | Contains |
| --- | --- |
| `Mirai.Space.Extensions.Http.Abstractions` | `IRequestService`, `IRequestSerializer`, `IRequestErrorHandler`, `RequestResponse`, `RequestException` |
| `Mirai.Space.Extensions.Http` | `RequestService` (internal), `JsonRequestSerializer`, `DefaultRequestErrorHandler`, DI extensions |
| `Mirai.Space.Extensions.Http.MessagePack` | `MessagePackRequestSerializer`, `UseMessagePack()` |

JSON is part of the base package and is the default serializer. Contracts are public; implementations are
`internal` and reachable only through DI.

## Registration

```csharp
services.AddRequestService<AboutRequestService>()
    .ConfigureHttpClient(client =>
    {
        client.BaseAddress = new Uri("https://api.example.com/");
    });
```

`AddRequestService<TClient>()` returns `IHttpClientBuilder`, so standard `ConfigureHttpClient`,
`ConfigurePrimaryHttpMessageHandler`, `AddHttpMessageHandler`, and resilience handlers compose normally.
`TClient` is transient, matching the `IHttpClientFactory` typed-client lifetime. Do not register the
typed client as a singleton.

The default serializer is JSON (`JsonSerializerDefaults.Web`), so a client needs no serializer call. Use the
`ConfigureJson(...)` only to customize JSON or to pin JSON on a client whose global serializer was
replaced:

```csharp
builder.ConfigureJson(options => options.PropertyNamingPolicy = null);
builder.ConfigureJson(options => options.TypeInfoResolver = MyJsonContext.Default);
```

For MessagePack use `UseMessagePack()` (or `UseMessagePack(options)`); the request `Content-Type` becomes
`application/x-msgpack` and the default `Accept` should be set to the same value.

Different clients can use different serializers and error handlers: keyed registrations are scoped by the
named `HttpClient` (`ConfigureJson(options => ...)` on one builder, `UseMessagePack()` on another). The serializer
instance is created once per registration and reused for all requests of that named client.

Global `AddJsonSerializer`/`AddRequestErrorHandler` shortcuts are intentionally not exposed: JSON is already the
default, and configuration belongs to a concrete client through `ConfigureJson(...)`/`UseErrorHandler(...)`. To replace
the global fallback, register `IRequestSerializer`/`IRequestErrorHandler` in DI before the first client is resolved.

Per-client overrides:

```csharp
builder.UseSerializer<MySerializer>();
builder.UseSerializer(serializerInstance);
builder.UseErrorHandler<MyErrorHandler>();
builder.UseErrorHandler(errorHandlerInstance);
```

## Writing a service

A service depends on `IRequestService` and exposes concrete DTO methods; generic overloads remain available
for the rare case where the response type is decided by the caller.

```csharp
public sealed class AboutRequestService(IRequestService requests)
{
    public Task<AboutInfo?> GetAboutAsync(CancellationToken ct = default) =>
        requests.GetAsync<AboutInfo>("about", ct);

    public Task<RequestResponse<AboutInfo>> GetAboutResponseAsync(CancellationToken ct = default) =>
        requests.GetResponseAsync<AboutInfo>("about", ct);

    public Task<AboutCreated?> CreateAboutAsync(AboutUpdate body, CancellationToken ct = default) =>
        requests.PostAsync<AboutCreated, AboutUpdate>("about", body, ct);

    public Task<RequestResponse> DownloadAsync(Stream destination, CancellationToken ct = default) =>
        requests.DownloadAsync("about/stream", destination, ct);
}
```

`AboutRequestService` in `tests/Mirai.Space.Extensions.Http.Tests/About` is the reference
implementation: it exercises all 25 `IRequestService` methods against a real ASP.NET host.

### Method groups

- `GetAsync<T>` / `GetResponseAsync<T>` / `GetAsyncEnumerable<T>` / `DownloadAsync`
- `HeadAsync` / `OptionsAsync`
- `PostAsync`, `PostAsync<TBody>`, `PostAsync<TResponse>`, `PostAsync<TResponse, TBody>`,
  `PostResponseAsync<TResponse>`, `PostResponseAsync<TResponse, TBody>`,
  `PostToStreamAsync`, `PostToStreamAsync<TBody>`
- `PutAsync<TBody>`, `PutAsync<TResponse, TBody>`, `PutResponseAsync<TResponse, TBody>`
- `PatchAsync<TBody>`, `PatchAsync<TResponse, TBody>`, `PatchResponseAsync<TResponse, TBody>`
- `DeleteAsync`, `DeleteAsync<T>`, `DeleteResponseAsync<T>`
- escape hatches: `SendAsync`, `SendAsync<T>`

High-level methods dispose the `HttpRequestMessage` they create. Escape hatches take a caller-owned request and
do not dispose it: dispose it yourself after `SendAsync`/`SendAsync<T>` completes. Do not reuse a request after
passing it to `IRequestService`.

## Streaming

Stream methods take a caller-owned destination and copy the response body into it. The service owns and
disposes the `HttpResponseMessage`; the destination is left open.

```csharp
await using var file = File.Create("about.bin");
var response = await requests.DownloadAsync("about/stream", file, ct);
```

`GetAsyncEnumerable<T>` returns `IAsyncEnumerable<T>` and disposes the response when enumeration completes or
the iterator is disposed. Supported content types:

- `application/json` — JSON array (`[1,2,3]`); a `null` element is an error.
- `application/x-ndjson`, `application/jsonl`, `application/x-jsonlines` — one JSON value per line.
- `text/event-stream` — SSE `data:` events, one deserialized value per event.
- `application/x-msgpack` — a sequence of MessagePack structures; a single top-level MessagePack array is
  not streamable and fails with `MessagePackSerializationException`.

MessagePack + SSE is not supported: SSE is a UTF-8 text framing, so the MessagePack parser fails with
`MessagePackSerializationException` on the `data:` lines.

## Errors

`RequestService` creates a `RequestException` for every non-success response and calls
`IRequestErrorHandler.HandleAsync(exception, ct)`. A handler may log, transform, or throw; if it returns
normally, `RequestService` throws the same exception.

```csharp
try
{
    var about = await requests.GetAsync<AboutInfo>("about", ct);
}
catch (RequestException exception)
{
    // exception.StatusCode / Method / RequestUri
}
```

`RequestException` deliberately has no response body. ProblemDetails parsing is deferred until the rest
of the API is settled; `RequestException` carries only the status line and request coordinates.

## Headers

`RequestResponse.Headers` and `RequestResponse.ContentHeaders` are case-insensitive read-only snapshots.
.NET classifies some headers (for example `Allow` on an `OPTIONS` response) as content headers, so check
both collections:

```csharp
if (response.ContentHeaders.TryGetValue("Allow", out var allow))
{
    // allow: IReadOnlyList<string>
}
```

## Serializer notes

- JSON uses `JsonSerializerDefaults.Web` by default; customize it via `ConfigureJson(...)`, including
  a `JsonSerializerContext` as `options.TypeInfoResolver`. The serializer stores the options reference;
  configure it once and do not mutate it after first use (STJ freezes options).
- MessagePack uses `MessagePackSerializerOptions.Standard` by default. For source generation, pass an
  `IFormatterResolver` (generated by `[GeneratedMessagePackResolver]`) to
  `MessagePackRequestSerializer(IFormatterResolver)` or configure via DI.
- Serializers set the request `Content-Type`; `AddRequestService` sets `Accept` from the resolved serializer.

## Testing

`tests/Mirai.Space.Extensions.Http.Tests` is the only HTTP test project. It contains:

- unit tests for `RequestService` and serializers with `StubHandler`;
- the `About` suite, which starts a real minimal-API `WebApplication` and runs the same 27 tests twice:
  JSON and MessagePack (`[Theory]` with `json` / `msgpack`).

Pattern:

```csharp
await using var server = await AboutTestServer.StartAsync(ct);
using var provider = CreateProvider(server, "msgpack");
var about = provider.GetRequiredService<AboutRequestService>();
var result = await about.GetAboutAsync(ct);
```

The test project has `FrameworkReference Microsoft.AspNetCore.App`; no separate server project is needed.

## Current limitations

- No authorization/authentication pipeline yet; it was removed from v1.
- `Accept` is not set automatically.
- A single top-level MessagePack array cannot be streamed; use a sequence of structures.
- ProblemDetails parsing is not implemented yet; `RequestException` has no body or problem document.
