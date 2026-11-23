using System.Text;
using MessagePack;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mirai.Space.Extensions.Http.Tests.About;

internal sealed class AboutTestServer : IAsyncDisposable
{
    private const string StreamPayload = "about-stream";
    private const string MessagePackContentType = "application/x-msgpack";

    private readonly WebApplication _app;

    private AboutTestServer(WebApplication app, string baseAddress)
    {
        _app = app;
        BaseAddress = baseAddress;
    }

    public string BaseAddress { get; }

    public static async Task<AboutTestServer> StartAsync(CancellationToken ct = default)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        var app = builder.Build();

        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Test"] = "about";
            await next();
        });

        app.MapGet("/about", (HttpContext context) =>
            WriteAsync(context, new AboutInfo
            {
                Name = "Mirai.Space",
                Version = "1.0",
                Description = "test"
            }));

        app.MapGet("/about/tags", async (HttpContext context) =>
        {
            var tags = new[] { "alpha", "beta" };
            if (IsMessagePack(context.Request))
            {
                context.Response.ContentType = MessagePackContentType;
                foreach (var tag in tags)
                {
                    await context.Response.Body.WriteAsync(MessagePackSerializer.Serialize(tag), context.RequestAborted);
                }

                return;
            }

            await context.Response.WriteAsJsonAsync(tags, context.RequestAborted);
        });

        app.MapGet("/about/stream", () =>
            Results.Bytes(Encoding.UTF8.GetBytes(StreamPayload), "application/octet-stream"));

        app.MapMethods("/about", ["HEAD"], () => Results.Ok());
        app.MapMethods("/about", ["OPTIONS"], (HttpContext context) =>
        {
            context.Response.Headers["Allow"] = "GET, POST, PUT, PATCH, DELETE, OPTIONS";
            return Results.Ok();
        });

        app.MapPost("/about", async (HttpContext context) =>
        {
            var update = await ReadUpdateAsync(context.Request);
            await WriteAsync(context, new AboutCreated
            {
                Id = Guid.NewGuid(),
                Name = update?.Name ?? "empty"
            });
        });

        app.MapPost("/about/stream", () =>
            Results.Bytes(Encoding.UTF8.GetBytes(StreamPayload), "application/octet-stream"));

        app.MapPut("/about", async (HttpContext context) =>
        {
            var update = await ReadUpdateAsync(context.Request);
            await WriteAsync(context, new AboutInfo
            {
                Name = update!.Name,
                Version = "2.0",
                Description = update.Description
            });
        });

        app.MapPatch("/about", async (HttpContext context) =>
        {
            var update = await ReadUpdateAsync(context.Request);
            await WriteAsync(context, new AboutInfo
            {
                Name = update!.Name,
                Version = "2.0",
                Description = update.Description
            });
        });

        app.MapDelete("/about", (HttpContext context) =>
            WriteAsync(context, new AboutInfo
            {
                Name = "deleted",
                Version = "1.0",
                Description = "deleted"
            }));

        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync(ct);

        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new AboutTestServer(app, address.TrimEnd('/') + "/");
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    private static bool IsMessagePack(HttpRequest request) =>
        request.Headers.Accept.Any(value =>
            value?.Contains(MessagePackContentType, StringComparison.OrdinalIgnoreCase) == true);

    private static Task WriteAsync<T>(HttpContext context, T value)
    {
        if (!IsMessagePack(context.Request))
        {
            return context.Response.WriteAsJsonAsync(value, context.RequestAborted);
        }

        context.Response.ContentType = MessagePackContentType;
        return context.Response.Body
            .WriteAsync(MessagePackSerializer.Serialize(value), context.RequestAborted)
            .AsTask();
    }

    private static async Task<AboutUpdate?> ReadUpdateAsync(HttpRequest request)
    {
        if (request.ContentLength is not > 0)
        {
            return null;
        }

        if (!IsMessagePack(request))
        {
            return await request.ReadFromJsonAsync<AboutUpdate>(request.HttpContext.RequestAborted);
        }

        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, request.HttpContext.RequestAborted);
        buffer.Position = 0;
        return await MessagePackSerializer.DeserializeAsync<AboutUpdate>(
            buffer,
            cancellationToken: request.HttpContext.RequestAborted);
    }
}
