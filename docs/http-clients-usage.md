# HTTP clients: usage guide

Status: implemented in `Mirai.Space.Extensions.Http` / `.Http.Json` / `.Http.MessagePack`.
Design rationale and open questions live in [http-clients-proposal.md](http-clients-proposal.md).

## Projects

| Project | Contains |
| --- | --- |
| `Mirai.Space.Extensions.Http.Abstractions` | `IRequestService`, `IRequestSerializer`, `IApiErrorHandler`, `ApiResponse`, `ApiException`, `RequestProblemDetails`, `StreamFormat`-agnostic contracts |
| `Mirai.Space.Extensions.Http` | `RequestService` (internal), `ApiRequest` (internal), `DefaultApiErrorHandler`, DI extensions |
| `Mirai.Space.Extensions.Http.Json` | `JsonRequestSerializer`, `ProblemDetailsErrorHandler`, `UseJson()` / `AddJsonSerializer()` / `AddProblemDetailsErrorHandler()` |
| `Mirai.Space.Extensions.Http.MessagePack` | `MessagePackRequestSerializer`, `UseMessagePack()` / `AddMessagePackSerializer()` |

Contracts are public; implementations are `internal` and reachable only through DI.

## Registration

```csharp
services.AddRequestService<AboutRequestService>()
    .UseJson()
    .ConfigureHttpClient(client =>
    {
        client.BaseAddress = new Uri("https://api.example.com/");
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    });
```

`AddRequestService<TClient>()` returns `IHttpClientBuilder`, so standard `ConfigureHttpClient`,
`ConfigurePrimaryHttpMessageHandler`, `AddHttpMessageHandler`, and resilience handlers compose normally.
`TClient` is transient, matching the `IHttpClientFactory` typed-client lifetime. Do not register the
typed client as a singleton.

For MessagePack use `UseMessagePack()` (or `UseMessagePack(options)`); the request `Content-Type`
becomes `application/x-msgpack` and the default `Accept` should be set to the same value.

Different clients can use different serializers: registrations are keyed by the named `HttpClient`
(`UseJson()` on one builder, `UseMessagePack()` on another). The same serializer instance is reused for
all requests of that named client.

Standalone default error handling:

```csharp
services.AddHttpExtensions();                 // DefaultApiErrorHandler
services.AddProblemDetailsErrorHandler();     // JSON project, RequestProblemDetails
services.UseErrorHandler<MyErrorHandler>();   // per named client
services.AddApiErrorHandler<MyErrorHandler>();// global
```

## Writing a service

A service depends on `IRequestService` and exposes concrete DTO methods; generic overloads are for the
rare case where the response type is decided by the caller.

```csharp
public sealed class AboutRequestService(IRequestService requests)
{
    public Task<AboutInfo?> GetAboutAsync(CancellationToken ct = default) =>
        requests.GetAsync<AboutInfo>("about", ct);

    public Task<ApiResponse<AboutInfo>> GetAboutResponseAsync(CancellationToken ct = default) =>
        requests.GetResponseAsync<AboutInfo>("about", ct);

    public Task<AboutCreated?> CreateAboutAsync(AboutUpdate body, CancellationToken ct = default) =>
        requests.PostAsync<AboutCreated, AboutUpdate>("about", body, ct);

    public Task DownloadAsync(Stream destination, CancellationToken ct = default) =>
        requests.GetStreamAsync("about/stream", destination, ct);
}
```

`AboutRequestService` in `tests/Mirai.Space.Extensions.Http.Tests/Tests/About` is the reference
implementation: it exercises all 23 `IRequestService` methods against a real ASP.NET host.

### Method groups

- `GetAsync<T>` / `GetResponseAsync<T>` / `GetAsyncEnumerable<T>` / `GetStreamAsync`
- `HeadAsync` / `OptionsAsync`
- `PostAsync`, `PostAsync<TBody>`, `PostAsync<TResponse>`, `PostAsync<TResponse, TBody>`,
  `PostResponseAsync<TResponse, TBody>`, `PostStreamAsync`, `PostStreamAsync<TBody>`
- `PutAsync<TBody>`, `PutAsync<TResponse, TBody>`, `PatchAsync<TBody>`, `PatchAsync<TResponse, TBody>`
- `DeleteAsync`, `DeleteAsync<T>`
- escape hatches: `SendAsync`, `SendAsync<T>`, `SendStreamAsync`, `SendAsyncEnumerable<T>`

High-level methods own and dispose their `HttpRequestMessage`. Escape-hatch methods do not dispose the
caller's request.

## Streaming

Stream methods take a caller-owned destination and copy the response body into it. The service owns and
disposes the `HttpResponseMessage`; the destination is left open.

```csharp
await using var file = File.Create("about.bin");
var response = await requests.GetStreamAsync("about/stream", file, ct);
```

`GetAsyncEnumerable<T>` / `SendAsyncEnumerable<T>` return `IAsyncEnumerable<T>` and dispose the response
when enumeration completes or the iterator is disposed. Supported content types:

- `application/json` — JSON array (`[1,2,3]`); a `null` element is an error.
- `application/x-ndjson`, `application/jsonl`, `application/x-jsonlines` — one JSON value per line.
- `text/event-stream` — SSE `data:` events, one deserialized value per event.
- `application/x-msgpack` — a sequence of MessagePack structures; a single top-level MessagePack array is
  not streamable and fails with `MessagePackSerializationException`.

MessagePack + SSE is rejected with `NotSupportedException`: SSE is a UTF-8 text framing, raw MessagePack
cannot travel inside it without base64.

## Errors

Non-success responses go through `IApiErrorHandler.HandleAsync(response, ct)`, which returns an
`ApiException`; `RequestService` disposes the response and throws it.

```csharp
try
{
    var about = await requests.GetAsync<AboutInfo>("about", ct);
}
catch (ApiException exception)
{
    // exception.StatusCode / Method / RequestUri
    // exception.Problem when the body was application/problem+json
}
```

`ApiException` deliberately has no response body. Use `ProblemDetailsErrorHandler` (JSON project) to
populate `RequestProblemDetails`. Bodies larger than `maxBodyLength` (8192 by default) are not parsed;
the exception falls back to the status line instead of exposing a truncated problem document.

## Headers

`ApiResponse.Headers` and `ApiResponse.ContentHeaders` are case-insensitive read-only snapshots.
`TryGetHeader` checks both collections because .NET classifies some headers (for example `Allow`) as
content headers even when they arrive on an `OPTIONS` response:

```csharp
if (response.TryGetHeader("Allow", out var allow))
{
    // allow: string[]
}
```

## Serializer notes

- JSON uses `JsonSerializerDefaults.Web` by default; pass `JsonSerializerOptions` or a
  `JsonSerializerContext` via `UseJson(...)`. The serializer stores the options reference; configure it
  once and do not mutate it after first use (STJ freezes options).
- MessagePack uses `MessagePackSerializerOptions.Standard` by default. For source generation, pass an
  `IFormatterResolver` (generated by `[GeneratedMessagePackResolver]`) to
  `MessagePackRequestSerializer(IFormatterResolver)` or configure via DI.
- Serializers set the request `Content-Type`; they do not set `Accept`.

## Testing

`tests/Mirai.Space.Extensions.Http.Tests` is the only HTTP test project. It contains:

- unit tests for `RequestService` and serializers with `StubHandler`;
- the `About` suite, which starts a real minimal-API `WebApplication` and runs the same 23 tests twice:
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
- `ProblemDetailsErrorHandler` does not parse bodies over its size cap.
