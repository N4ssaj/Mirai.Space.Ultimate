# HTTP clients: usage guide

Status: implemented in `Mirai.Space.Extensions.Http` / `.Http.MessagePack`.
Design rationale and open questions live in [http-clients-proposal.md](http-clients-proposal.md).

## Projects

| Project | Contains |
| --- | --- |
| `Mirai.Space.Extensions.Http.Abstractions` | `IRequestService`, `IRequestSerializer`, `IRequestErrorHandler`, `RequestResponse`, `RequestException` |
| `Mirai.Space.Extensions.Http` | `RequestService` (internal), `RequestServiceExtensions`, `JsonRequestSerializer`, `DefaultRequestErrorHandler`, DI extensions |
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

`AddRequestService<TClient>()` and `AddRequestService<TClient>(string name)` return `IHttpClientBuilder`, so
standard `ConfigureHttpClient`, `ConfigurePrimaryHttpMessageHandler`, `AddHttpMessageHandler`, and resilience
handlers compose normally. `TClient` is transient, matching the `IHttpClientFactory` typed-client lifetime.
Do not register the typed client as a singleton.

### Response completion

By default `AddRequestService` uses `HttpCompletionOption.ResponseHeadersRead`: `SendResponseAsync` completes as soon
as headers arrive, and `SendAsync<T>` reads the body from the network stream. A non-streaming `GetAsync<T>`/`SendAsync<T>`
still materializes the whole `T` (for a `List<T>` that means the whole list), but it does not additionally
buffer the whole body in memory. For very large collections use `GetAsyncEnumerable<T>`/`SendAsyncEnumerable<T>`
so elements are consumed one by one.

Per client:

```csharp
services.AddRequestService<BigResponseClient>()
    .ConfigureRequestService(options =>
    {
        options.CompletionOption = HttpCompletionOption.ResponseHeadersRead;
    });

// or after AddRequestService:
builder.ConfigureRequestService(options =>
{
    options.CompletionOption = HttpCompletionOption.ResponseContentRead;
});
```

`ResponseContentRead` makes `HttpClient` buffer the body before `SendResponseAsync`/`SendAsync` returns, which is useful for
small responses but doubles memory for large ones. The option applies to buffered sends only; streaming methods
(`GetToStreamAsync`, `PostToStreamAsync`, `PutToStreamAsync`, `PatchToStreamAsync`, `DeleteToStreamAsync`, `GetAsyncEnumerable`)
always use `ResponseHeadersRead`, so they never buffer the whole body.

With `ResponseHeadersRead`, `HttpClient.Timeout` covers only the headers; body reads are bounded by the
`CancellationToken` passed to the request.

`AddRequestService()` registers the default (unkeyed) `IRequestService`. `AddRequestService(string name)`
registers only a keyed `IRequestService`; the typed overloads also register the client. The parameterless
typed overload uses `typeof(TClient).FullName` as the key, while the named overload lets several clients
share one key or use a stable name:

```csharp
services.AddRequestService();                              // default IRequestService
services.AddRequestService("github");                      // keyed IRequestService named "github"
services.AddRequestService<GitHubClient>();                // keyed by typeof(GitHubClient).FullName + typed client
services.AddRequestService<GitHubClient>("github");        // keyed "github" + typed client
```

The default serializer is JSON (`JsonSerializerDefaults.Web`), so a client needs no serializer call.
`ConfigureJson(...)` only customizes the named JSON options; it does not select or replace the serializer:

```csharp
builder.ConfigureJson(options => options.PropertyNamingPolicy = null);
builder.ConfigureJson(options => options.TypeInfoResolver = MyJsonContext.Default);
```

The `Action<JsonSerializerOptions>` overload starts from `JsonSerializerDefaults.Web`.

For MessagePack use `UseMessagePack()` or `UseMessagePack(options => options.WithCompression(...))`; the
configure callback receives `MessagePackSerializerOptions.Standard` and returns the options to register. The
request `Content-Type` becomes `application/msgpack`; `Accept` is not set automatically.

Different clients can use different serializers and error handlers: keyed registrations are scoped by the
named `HttpClient` (`ConfigureJson(options => ...)` on one builder, `UseMessagePack()` on another).
`UseSerializer<TSerializer>()` and `UseErrorHandler<THandler>()` register transient instances, so each
resolution creates a new one.

`ConfigureJson(Action<JsonSerializerOptions>)` only configures named `JsonSerializerOptions` through
`Configure<JsonSerializerOptions>`. The default `JsonRequestSerializer` resolves those options when it is
created; there is no `IOptionsMonitor` and no singleton. `ConfigureJson` does not switch a client that already
uses another serializer back to JSON. `UseMessagePack(Func<MessagePackSerializerOptions, MessagePackSerializerOptions>)`
receives `MessagePackSerializerOptions.Standard` and registers the returned options with its serializer.

Global `AddJsonSerializer`/`AddRequestErrorHandler` shortcuts are intentionally not exposed: JSON is already the
default, and configuration belongs to a concrete client. Call `UseSerializer<T>()`/`UseErrorHandler<T>()` after
`AddRequestService` to replace that client's default.

Per-client overrides:

```csharp
builder.UseSerializer<MySerializer>();
builder.UseErrorHandler<MyErrorHandler>();
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

    public Task<RequestResponse> GetToStreamAsync(Stream destination, CancellationToken ct = default) =>
        requests.GetToStreamAsync("about/stream", destination, ct);
}
```

`AboutRequestService` in `tests/Mirai.Space.Extensions.Http.Tests/About` is the reference
implementation; it exercises the main `RequestServiceExtensions` methods against a real ASP.NET host.

### Method groups

- `GetAsync<TResponse>` / `GetResponseAsync` / `GetResponseAsync<TResponse>` / `GetAsyncEnumerable<T>` / `GetToStreamAsync`
- `HeadResponseAsync` / `OptionsResponseAsync`
- `PostAsync`, `PostAsync<TBody>`, `PostAsync<TResponse>`, `PostAsync<TResponse, TBody>`,
  `PostResponseAsync`, `PostResponseAsync<TResponse>`, `PostResponseAsync<TResponse, TBody>`,
  `PostToStreamAsync`, `PostToStreamAsync<TBody>`
- `PutAsync`, `PutAsync<TBody>`, `PutAsync<TResponse>`, `PutAsync<TResponse, TBody>`,
  `PutResponseAsync`, `PutResponseAsync<TResponse>`, `PutResponseAsync<TResponse, TBody>`,
  `PutToStreamAsync`, `PutToStreamAsync<TBody>`
- `PatchAsync`, `PatchAsync<TBody>`, `PatchAsync<TResponse>`, `PatchAsync<TResponse, TBody>`,
  `PatchResponseAsync`, `PatchResponseAsync<TResponse>`, `PatchResponseAsync<TResponse, TBody>`,
  `PatchToStreamAsync`, `PatchToStreamAsync<TBody>`
- `DeleteAsync`, `DeleteAsync<TResponse>`, `DeleteAsync<TBody>`, `DeleteAsync<TResponse, TBody>`,
  `DeleteResponseAsync`, `DeleteResponseAsync<TResponse>`, `DeleteResponseAsync<TResponse, TBody>`,
  `DeleteToStreamAsync`, `DeleteToStreamAsync<TBody>`
- escape hatches: `SendAsync`, `SendAsync<TResponse>`, `SendResponseAsync`, `SendResponseAsync<TResponse>`

`IRequestService` contains the transport plus the non-generic `*ResponseAsync(string)` overloads:
`CreateContent<TBody>`, `SendAsync`, `SendAsync<TResponse>`, `SendResponseAsync`, `SendResponseAsync<TResponse>`,
`SendToStreamAsync`, `SendAsyncEnumerable<T>`. The remaining high-level methods above are extension methods in
`Mirai.Space.Extensions.Http` (`RequestServiceExtensions`), so the consuming file needs
`using Mirai.Space.Extensions.Http;` (the same namespace as the DI registration).
`SendAsync` reads only the payload and does not clone response headers; `SendResponseAsync`/`SendResponseAsync<T>`
return `RequestResponse` and therefore snapshot them.

The extensions create and dispose their own `HttpRequestMessage`. The escape hatches take a caller-owned
request and never dispose it: dispose it yourself after `SendAsync`/`SendAsync<T>`/`SendResponseAsync`/`SendResponseAsync<T>`/`SendToStreamAsync` completes.
Do not reuse a request after passing it to `IRequestService`.

## Streaming

Stream methods take a caller-owned destination and copy the response body into it. The service owns and
disposes the `HttpResponseMessage`; the destination is left open.

```csharp
await using var file = File.Create("about.bin");
var response = await requests.GetToStreamAsync("about/stream", file, ct);
```

`GetAsyncEnumerable<T>` returns `IAsyncEnumerable<T>` and disposes the response when enumeration completes or
the iterator is disposed. Supported content types:

- `application/json` — JSON array (`[1,2,3]`); `null` elements are skipped (the stream contract has no null items).
- `application/x-ndjson`, `application/jsonl`, `application/x-jsonlines` — one JSON value per line; blank lines and `null` values are skipped.
- `text/event-stream` — SSE `data:` events, one deserialized value per event; a null/empty payload is a protocol error.
- `application/msgpack` — a sequence of MessagePack structures; empty frames are skipped, and a single top-level MessagePack array is
  not streamable and fails with `MessagePackSerializationException`.

MessagePack + SSE is not supported: SSE is a UTF-8 text framing, so the MessagePack parser fails with
`MessagePackSerializationException` on the `data:` lines.

## Errors

`RequestService` calls `IRequestErrorHandler.HandleAsync(HttpResponseMessage, ct)` for every non-success
response and then throws `RequestException`. The handler receives the raw response: it may read the body,
parse ProblemDetails, throw a custom `RequestException`/domain exception, or return normally. Returning
does not suppress the failure: the service throws its default `RequestException` afterwards. A handler that
throws replaces that exception (its exception escapes). The response is disposed after the call, so do not
store it.

```csharp
public sealed class ProblemDetailsErrorHandler : IRequestErrorHandler
{
    public async ValueTask HandleAsync(HttpResponseMessage response, CancellationToken ct = default)
    {
        if (response.Content.Headers.ContentType?.MediaType is "application/problem+json")
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(ct);
            throw new RequestException(
                problem?.Title ?? "HTTP error",
                response.StatusCode,
                response.RequestMessage?.Method,
                response.RequestMessage?.RequestUri);
        }
    }
}
```

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

`RequestException` carries the status line and request coordinates only. The default handler does not read the
error body or response headers; a custom `IRequestErrorHandler` can read them from the response and throw a
more specific exception. ProblemDetails parsing is not built in; the handler above is the extension point.

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
- Serializers set the request `Content-Type`; `AddRequestService` does not set `Accept` automatically.
  Set `Accept` per request, or configure `HttpClient.DefaultRequestHeaders` explicitly in `ConfigureHttpClient`.

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
- No per-request timeout parameter: `HttpClient.Timeout` is per named client, and with the default
  `ResponseHeadersRead` it covers only the headers; cancel body reads through `CancellationToken`.
- A single top-level MessagePack array cannot be streamed; use a sequence of structures.
- ProblemDetails parsing is not built in; `RequestException` does not expose the error body. Use a custom
  `IRequestErrorHandler` to parse the response and throw a richer exception.
