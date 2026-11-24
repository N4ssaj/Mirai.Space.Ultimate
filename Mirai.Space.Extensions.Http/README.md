# Mirai.Space.Extensions.Http

A small HTTP client layer for Mirai.Space services, built on `IHttpClientFactory`.

It keeps transport details out of API clients and gives you:

- typed and named clients registered through DI;
- JSON by default, MessagePack as an optional package;
- content-only calls and response-envelope calls;
- streaming to a destination or as `IAsyncEnumerable<T>`;
- one error hook that can inspect the raw `HttpResponseMessage`;
- no automatic `Accept` header — serializers only set the request `Content-Type`.

The library targets **.NET 10**.

## Projects

| Project | Purpose |
| --- | --- |
| `Mirai.Space.Extensions.Http.Abstractions` | Public contracts: `IRequestService`, `IRequestSerializer`, `IRequestErrorHandler`, `RequestResponse`, `RequestException` |
| `Mirai.Space.Extensions.Http` | Implementation, JSON serializer, DI extensions, high-level `IRequestService` extensions |
| `Mirai.Space.Extensions.Http.MessagePack` | Optional MessagePack serializer and `UseMessagePack()` |

Reference the projects directly in this repository. JSON support is part of the base package.

## Quick start

```csharp
using Microsoft.Extensions.DependencyInjection;
using Mirai.Space.Extensions.Http;
using Mirai.Space.Extensions.Http.Abstractions;
using Mirai.Space.Extensions.Http.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRequestService<GitHubClient>("github")
    .ConfigureHttpClient(client =>
    {
        client.BaseAddress = new Uri("https://api.github.com/");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mirai.Space/1.0");
    });

public sealed class GitHubClient(IRequestService requests)
{
    public Task<Repository?> GetRepositoryAsync(
        string owner,
        string name,
        CancellationToken ct = default) =>
        requests.GetAsync<Repository>($"repos/{owner}/{name}", ct);
}

public sealed record Repository(string Name, string FullName);
```

`AddRequestService<GitHubClient>("github")` registers the named `HttpClient`, a keyed `IRequestService`,
and a transient `GitHubClient`. The typed client is constructed through DI and receives its own keyed
service, so it is safe to inject into controllers, minimal APIs, or other services.

For a default client without a wrapper class:

```csharp
builder.Services.AddRequestService();

public sealed class AboutService(IRequestService requests)
{
    public Task<About?> GetAboutAsync(CancellationToken ct = default) =>
        requests.GetAsync<About>("about", ct);
}
```

### Registration overloads

| Call | Key | Also registers |
| --- | --- | --- |
| `AddRequestService()` | `""` (default) | unkeyed `IRequestService` alias |
| `AddRequestService("github")` | `"github"` | keyed `IRequestService` only |
| `AddRequestService<GitHubClient>()` | `typeof(GitHubClient).FullName` | keyed service + transient `GitHubClient` |
| `AddRequestService<GitHubClient>("github")` | `"github"` | keyed service + transient `GitHubClient` |

Every overload returns `IHttpClientBuilder`, so normal `IHttpClientFactory` configuration still works:
`ConfigureHttpClient`, `ConfigurePrimaryHttpMessageHandler`, `AddHttpMessageHandler`, resilience handlers,
and so on.

## Calling an API

There are two shapes:

- **content-only** methods return the deserialized body (`T?`) and skip response-header snapshots;
- **response-envelope** methods return `RequestResponse` / `RequestResponse<T>` with status and headers.

```csharp
var repository = await requests.GetAsync<Repository>("repos/dotnet/runtime", ct);

var response = await requests.GetResponseAsync<Repository>("repos/dotnet/runtime", ct);
Console.WriteLine(response.StatusCode);
Console.WriteLine(string.Join(", ", response.Headers["ETag"]));
var sameRepository = response.Content;
```

`RequestResponse` exposes case-insensitive, read-only `Headers` and `ContentHeaders` snapshots.
`RequestResponse<T>.Content` is the deserialized body.

High-level methods are extension methods in `Mirai.Space.Extensions.Http`, so the file needs
`using Mirai.Space.Extensions.Http;`.

### Method groups

| Verb | Content | Envelope | Streaming |
| --- | --- | --- | --- |
| `GET` | `GetAsync<TResponse>` | `GetResponseAsync`, `GetResponseAsync<TResponse>` | `GetToStreamAsync`, `GetAsyncEnumerable<T>` |
| `HEAD` | — | `HeadResponseAsync` | — |
| `OPTIONS` | — | `OptionsResponseAsync` | — |
| `POST` | `PostAsync`, `PostAsync<TBody>`, `PostAsync<TResponse>`, `PostAsync<TResponse, TBody>` | `PostResponseAsync`, `PostResponseAsync<TResponse>`, `PostResponseAsync<TResponse, TBody>` | `PostToStreamAsync`, `PostToStreamAsync<TBody>` |
| `PUT` | `PutAsync`, `PutAsync<TBody>`, `PutAsync<TResponse>`, `PutAsync<TResponse, TBody>` | `PutResponseAsync`, `PutResponseAsync<TResponse>`, `PutResponseAsync<TResponse, TBody>` | `PutToStreamAsync`, `PutToStreamAsync<TBody>` |
| `PATCH` | `PatchAsync`, `PatchAsync<TBody>`, `PatchAsync<TResponse>`, `PatchAsync<TResponse, TBody>` | `PatchResponseAsync`, `PatchResponseAsync<TResponse>`, `PatchResponseAsync<TResponse, TBody>` | `PatchToStreamAsync`, `PatchToStreamAsync<TBody>` |
| `DELETE` | `DeleteAsync`, `DeleteAsync<TResponse>`, `DeleteAsync<TBody>`, `DeleteAsync<TResponse, TBody>` | `DeleteResponseAsync`, `DeleteResponseAsync<TResponse>`, `DeleteResponseAsync<TResponse, TBody>` | `DeleteToStreamAsync`, `DeleteToStreamAsync<TBody>` |

All methods accept `CancellationToken ct = default`.

### Low-level send methods

`IRequestService` also exposes the transport directly:

| Method | Result |
| --- | --- |
| `CreateContent<TBody>(body)` | `HttpContent` with the serializer's `Content-Type` |
| `SendAsync(request)` | `Task`, no body read |
| `SendAsync<TResponse>(request)` | `Task<TResponse?>` |
| `SendResponseAsync(request)` | `Task<RequestResponse>` |
| `SendResponseAsync<TResponse>(request)` | `Task<RequestResponse<TResponse>>` |
| `SendToStreamAsync(request, destination)` | `Task<RequestResponse>`, copies the body to `destination` |
| `SendAsyncEnumerable<T>(request)` | `IAsyncEnumerable<T>` |

The request passed to these methods is **caller-owned**: the service never disposes it. Dispose it after the
call. Do not reuse a request after passing it to `IRequestService`. The high-level methods above create and
dispose their own requests.

## Configuration

### Completion and streaming

The default `RequestServiceOptions.CompletionOption` is `HttpCompletionOption.ResponseHeadersRead`.
`SendResponseAsync` completes when headers arrive; `GetAsync<T>` still materializes the whole `T`.

```csharp
builder.Services.AddRequestService<BigResponseClient>()
    .ConfigureRequestService(options =>
    {
        options.CompletionOption = HttpCompletionOption.ResponseContentRead;
    });
```

`CompletionOption` applies to buffered sends only. Streaming methods always use
`ResponseHeadersRead` and never buffer the whole body:

- `GetToStreamAsync`
- `PostToStreamAsync`
- `PutToStreamAsync`
- `PatchToStreamAsync`
- `DeleteToStreamAsync`
- `GetAsyncEnumerable<T>`

With `ResponseHeadersRead`, `HttpClient.Timeout` covers headers only; body reads are bounded by the
`CancellationToken` passed to the request.

### JSON

JSON is the default serializer, configured with `JsonSerializerDefaults.Web`:
camelCase output, case-insensitive input, and numbers read from strings.

```csharp
builder.Services.AddRequestService<MyClient>()
    .ConfigureJson(options =>
    {
        options.PropertyNamingPolicy = null;
        options.TypeInfoResolver = MyJsonContext.Default;
    });
```

`ConfigureJson` only configures the named `JsonSerializerOptions`. It does **not** select or replace the
serializer. The default JSON serializer resolves those options when it is created; there is no
`IOptionsMonitor` and no options singleton.

Do not mutate the options instance after the serializer has used it — System.Text.Json freezes options
on first use.

### MessagePack

Add a reference to `Mirai.Space.Extensions.Http.MessagePack` and replace the serializer for a client:

```csharp
using MessagePack;
using Mirai.Space.Extensions.Http.MessagePack.Extensions;

builder.Services.AddRequestService<MessageClient>("msg")
    .UseMessagePack(options => options.WithCompression(MessagePackCompression.Lz4BlockArray));
```

`UseMessagePack()` uses `MessagePackSerializerOptions.Standard`. The callback receives `Standard` and
returns the options to register. Requests use `Content-Type: application/msgpack`.

JSON and MessagePack clients can coexist; serializer registrations are scoped to the named client.

### Custom serializer and error handler

```csharp
builder.Services.AddRequestService<MyClient>()
    .UseSerializer<MySerializer>()
    .UseErrorHandler<MyErrorHandler>();
```

`UseSerializer<T>()` must implement `IRequestSerializer`; `UseErrorHandler<T>()` must implement
`IRequestErrorHandler`. Both are transient and the last registration for a given named client wins.

## Responses and headers

`RequestResponse` and `RequestResponse<T>` carry `StatusCode`, `Headers`, and `ContentHeaders`.
Headers are copied into case-insensitive read-only dictionaries when the response is created.

.NET treats some headers as content headers — check both collections:

```csharp
if (response.ContentHeaders.TryGetValue("Allow", out var allow))
{
    foreach (var value in allow)
    {
        Console.WriteLine(value);
    }
}
```

The library does **not** set `Accept` automatically. Set it per request or on the client:

```csharp
using System.Net.Http.Headers;

using var request = new HttpRequestMessage(HttpMethod.Get, "repos/dotnet/runtime");
request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
var repository = await requests.SendAsync<Repository>(request, ct);
```

## Streaming

Stream methods copy the response body into a caller-owned destination. The destination is left open;
the service owns and disposes the `HttpResponseMessage`.

```csharp
await using var destination = File.Create("payload.bin");
var response = await requests.GetToStreamAsync("files/42", destination, ct);
```

`GetAsyncEnumerable<T>` consumes the body as a sequence:

```csharp
await foreach (var item in requests.GetAsyncEnumerable<Event>("events", ct))
{
    Console.WriteLine(item);
}
```

Supported stream formats:

| Content type | Shape |
| --- | --- |
| `application/json` | JSON array (`[1,2,3]`); `null` elements are skipped |
| `application/x-ndjson`, `application/jsonl`, `application/x-jsonlines` | one JSON value per line; blank and `null` values are skipped |
| `text/event-stream` | SSE events; a null/empty payload is a protocol error |
| `application/msgpack` | a sequence of MessagePack structures; empty frames are skipped |

A single top-level MessagePack array is not streamable — send a sequence of structures instead.
SSE with MessagePack is not supported; SSE is UTF-8 text framing.

## Errors

For every non-success response, `RequestService` calls `IRequestErrorHandler.HandleAsync` with the raw
`HttpResponseMessage`, then throws `RequestException`.

The handler can:

- read status, headers, and body;
- parse ProblemDetails or another error contract;
- throw a custom `RequestException` or domain exception to replace the default failure.

It **cannot** suppress the failure. If it returns normally, the service throws its default
`RequestException`. The response is disposed after the call, so do not store it.

```csharp
using System.Net;
using Mirai.Space.Extensions.Http.Abstractions;

public sealed class RateLimitErrorHandler : IRequestErrorHandler
{
    public ValueTask HandleAsync(HttpResponseMessage response, CancellationToken ct = default)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new RequestException(
                "Rate limited",
                response.StatusCode,
                response.RequestMessage?.Method,
                response.RequestMessage?.RequestUri);
        }

        return ValueTask.CompletedTask;
    }
}
```

```csharp
builder.Services.AddRequestService<MyClient>()
    .UseErrorHandler<RateLimitErrorHandler>();
```

`RequestException` carries the status line and request coordinates: `StatusCode`, `Method`, `RequestUri`.
The default handler does not read the error body; a custom handler can.

## Documentation and tests

- [Usage guide](../docs/http-clients-usage.md)
- [Design proposal and decisions](../docs/http-clients-proposal.md)
- [HTTP tests](../tests/Mirai.Space.Extensions.Http.Tests)

## Current limitations

- No authorization/authentication pipeline — configure handlers or defaults at the `HttpClient` level.
- No per-request timeout parameter: use the named client's `Timeout` and a `CancellationToken`.
- ProblemDetails parsing is not built in; use an `IRequestErrorHandler`.
- `GetAsyncEnumerable<T>` returns only items, without status or response-header metadata.
- A single top-level MessagePack array cannot be streamed.
