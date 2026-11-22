using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Mirai.Space.Extensions.Http.Abstractions;
using Mirai.Space.Extensions.Http.Extensions;
using Mirai.Space.Extensions.Http.Json.Extensions;
using Mirai.Space.Extensions.Http.MessagePack.Extensions;
using Xunit;

namespace Mirai.Space.Extensions.Http.Tests.About;

public sealed class AboutRequestServiceTests
{
    [Theory]
    [InlineData("json")]
    [InlineData("msgpack")]
    public async Task GetAbout_DeserializesBody(string format)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var server = await AboutTestServer.StartAsync(ct);
        using var provider = CreateProvider(server, format);
        var about = provider.GetRequiredService<AboutRequestService>();

        var result = await about.GetAboutAsync(ct);

        Assert.NotNull(result);
        Assert.Equal("Mirai.Space", result.Name);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("msgpack")]
    public async Task GetAboutResponse_ExposesStatusAndHeaders(string format)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var server = await AboutTestServer.StartAsync(ct);
        using var provider = CreateProvider(server, format);
        var about = provider.GetRequiredService<AboutRequestService>();

        var response = await about.GetAboutResponseAsync(ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("about", response.Headers["X-Test"].Single());
        Assert.NotNull(response.Content);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("msgpack")]
    public async Task DownloadAbout_CopiesBodyIntoDestination(string format)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var server = await AboutTestServer.StartAsync(ct);
        using var provider = CreateProvider(server, format);
        var about = provider.GetRequiredService<AboutRequestService>();
        using var destination = new MemoryStream();

        var response = await about.DownloadAboutAsync(destination, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("about-stream", Encoding.UTF8.GetString(destination.ToArray()));
    }

    [Theory]
    [InlineData("json")]
    [InlineData("msgpack")]
    public async Task GetTags_StreamsItems(string format)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var server = await AboutTestServer.StartAsync(ct);
        using var provider = CreateProvider(server, format);
        var about = provider.GetRequiredService<AboutRequestService>();
        var tags = new List<string>();

        await foreach (var tag in about.GetTagsAsync(ct))
        {
            tags.Add(tag);
        }

        Assert.Equal(new[] { "alpha", "beta" }, tags);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("msgpack")]
    public async Task HeadAbout_ReturnsResponse(string format)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var server = await AboutTestServer.StartAsync(ct);
        using var provider = CreateProvider(server, format);
        var about = provider.GetRequiredService<AboutRequestService>();

        var response = await about.HeadAboutAsync(ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.ContainsKey("X-Test"));
    }

    [Theory]
    [InlineData("json")]
    [InlineData("msgpack")]
    public async Task OptionsAbout_ReturnsAllowHeader(string format)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var server = await AboutTestServer.StartAsync(ct);
        using var provider = CreateProvider(server, format);
        var about = provider.GetRequiredService<AboutRequestService>();

        var response = await about.OptionsAboutAsync(ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.TryGetHeader("Allow", out var allow));
        Assert.Contains("POST", allow.Single());
    }

    [Theory]
    [InlineData("json")]
    [InlineData("msgpack")]
    public async Task CreateAboutWithoutBody_ReturnsResponse(string format)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var server = await AboutTestServer.StartAsync(ct);
        using var provider = CreateProvider(server, format);
        var about = provider.GetRequiredService<AboutRequestService>();

        var result = await about.CreateAboutAsync<AboutCreated>(ct);

        Assert.NotNull(result);
        Assert.Equal("empty", result.Name);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("msgpack")]
    public async Task CreateAboutWithBody_ReturnsResponse(string format)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var server = await AboutTestServer.StartAsync(ct);
        using var provider = CreateProvider(server, format);
        var about = provider.GetRequiredService<AboutRequestService>();

        var result = await about.CreateAboutAsync<AboutCreated, AboutUpdate>(
            new AboutUpdate { Name = "new", Description = "created" }, ct);

        Assert.NotNull(result);
        Assert.Equal("new", result.Name);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("msgpack")]
    public async Task CreateAboutWithoutBody_Completes(string format)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var server = await AboutTestServer.StartAsync(ct);
        using var provider = CreateProvider(server, format);
        var about = provider.GetRequiredService<AboutRequestService>();

        await about.CreateAboutAsync(ct);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("msgpack")]
    public async Task CreateAboutWithBody_Completes(string format)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var server = await AboutTestServer.StartAsync(ct);
        using var provider = CreateProvider(server, format);
        var about = provider.GetRequiredService<AboutRequestService>();

        await about.CreateAboutAsync<AboutUpdate>(
            new AboutUpdate { Name = "new", Description = "created" }, ct);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("msgpack")]
    public async Task CreateAboutResponse_ReturnsEnvelope(string format)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var server = await AboutTestServer.StartAsync(ct);
        using var provider = CreateProvider(server, format);
        var about = provider.GetRequiredService<AboutRequestService>();

        var response = await about.CreateAboutResponseAsync<AboutCreated, AboutUpdate>(
            new AboutUpdate { Name = "new", Description = "created" }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content);
        Assert.Equal("new", response.Content.Name);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("msgpack")]
    public async Task CreateAboutStream_CopiesBodyIntoDestination(string format)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var server = await AboutTestServer.StartAsync(ct);
        using var provider = CreateProvider(server, format);
        var about = provider.GetRequiredService<AboutRequestService>();
        using var destination = new MemoryStream();

        var response = await about.CreateAboutStreamAsync(destination, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("about-stream", Encoding.UTF8.GetString(destination.ToArray()));
    }

    [Theory]
    [InlineData("json")]
    [InlineData("msgpack")]
    public async Task CreateAboutStreamWithBody_CopiesBodyIntoDestination(string format)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var server = await AboutTestServer.StartAsync(ct);
        using var provider = CreateProvider(server, format);
        var about = provider.GetRequiredService<AboutRequestService>();
        using var destination = new MemoryStream();

        var response = await about.CreateAboutStreamAsync(
            new AboutUpdate { Name = "new", Description = "created" }, destination, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("about-stream", Encoding.UTF8.GetString(destination.ToArray()));
    }

    [Theory]
    [InlineData("json")]
    [InlineData("msgpack")]
    public async Task ReplaceAbout_ReturnsUpdatedBody(string format)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var server = await AboutTestServer.StartAsync(ct);
        using var provider = CreateProvider(server, format);
        var about = provider.GetRequiredService<AboutRequestService>();

        var result = await about.ReplaceAboutAsync(
            new AboutUpdate { Name = "replaced", Description = "done" }, ct);

        Assert.NotNull(result);
        Assert.Equal("replaced", result.Name);
        Assert.Equal("2.0", result.Version);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("msgpack")]
    public async Task ReplaceAboutNoResponse_Completes(string format)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var server = await AboutTestServer.StartAsync(ct);
        using var provider = CreateProvider(server, format);
        var about = provider.GetRequiredService<AboutRequestService>();

        await about.ReplaceAboutNoResponseAsync(
            new AboutUpdate { Name = "replaced", Description = "done" }, ct);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("msgpack")]
    public async Task PatchAbout_ReturnsUpdatedBody(string format)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var server = await AboutTestServer.StartAsync(ct);
        using var provider = CreateProvider(server, format);
        var about = provider.GetRequiredService<AboutRequestService>();

        var result = await about.PatchAboutAsync(
            new AboutUpdate { Name = "patched", Description = "done" }, ct);

        Assert.NotNull(result);
        Assert.Equal("patched", result.Name);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("msgpack")]
    public async Task PatchAboutNoResponse_Completes(string format)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var server = await AboutTestServer.StartAsync(ct);
        using var provider = CreateProvider(server, format);
        var about = provider.GetRequiredService<AboutRequestService>();

        await about.PatchAboutNoResponseAsync(
            new AboutUpdate { Name = "patched", Description = "done" }, ct);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("msgpack")]
    public async Task DeleteAbout_Completes(string format)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var server = await AboutTestServer.StartAsync(ct);
        using var provider = CreateProvider(server, format);
        var about = provider.GetRequiredService<AboutRequestService>();

        await about.DeleteAboutAsync(ct);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("msgpack")]
    public async Task DeleteAboutWithResponse_ReturnsBody(string format)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var server = await AboutTestServer.StartAsync(ct);
        using var provider = CreateProvider(server, format);
        var about = provider.GetRequiredService<AboutRequestService>();

        var result = await about.DeleteAboutAsync<AboutInfo>(ct);

        Assert.NotNull(result);
        Assert.Equal("deleted", result.Name);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("msgpack")]
    public async Task SendAsync_NonSuccess_ThrowsApiException(string format)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var server = await AboutTestServer.StartAsync(ct);
        using var provider = CreateProvider(server, format);
        var about = provider.GetRequiredService<AboutRequestService>();
        using var request = new HttpRequestMessage(HttpMethod.Get, "about/missing");

        var exception = await Assert.ThrowsAsync<ApiException>(() => about.SendAsync(request, ct));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(HttpMethod.Get, exception.Method);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("msgpack")]
    public async Task SendAsyncGeneric_ReturnsContent(string format)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var server = await AboutTestServer.StartAsync(ct);
        using var provider = CreateProvider(server, format);
        var about = provider.GetRequiredService<AboutRequestService>();
        using var request = new HttpRequestMessage(HttpMethod.Get, "about");

        var response = await about.SendAsync<AboutInfo>(request, ct);

        Assert.NotNull(response.Content);
        Assert.Equal("Mirai.Space", response.Content.Name);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("msgpack")]
    public async Task SendStreamAsync_CopiesBodyIntoDestination(string format)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var server = await AboutTestServer.StartAsync(ct);
        using var provider = CreateProvider(server, format);
        var about = provider.GetRequiredService<AboutRequestService>();
        using var request = new HttpRequestMessage(HttpMethod.Get, "about/stream");
        using var destination = new MemoryStream();

        var response = await about.SendStreamAsync(request, destination, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("about-stream", Encoding.UTF8.GetString(destination.ToArray()));
    }

    [Theory]
    [InlineData("json")]
    [InlineData("msgpack")]
    public async Task SendAsyncEnumerable_StreamsItems(string format)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var server = await AboutTestServer.StartAsync(ct);
        using var provider = CreateProvider(server, format);
        var about = provider.GetRequiredService<AboutRequestService>();
        using var request = new HttpRequestMessage(HttpMethod.Get, "about/tags");
        var tags = new List<string>();

        await foreach (var tag in about.SendAsyncEnumerable<string>(request, ct))
        {
            tags.Add(tag);
        }

        Assert.Equal(new[] { "alpha", "beta" }, tags);
    }

    private static ServiceProvider CreateProvider(AboutTestServer server, string format)
    {
        var services = new ServiceCollection();
        var builder = services.AddRequestService<AboutRequestService>()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri(server.BaseAddress);
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue(ContentType(format)));
            });

        if (format == "json")
        {
            builder.UseJson();
        }
        else
        {
            builder.UseMessagePack();
        }

        return services.BuildServiceProvider();
    }

    private static string ContentType(string format) =>
        format == "json" ? "application/json" : "application/x-msgpack";
}
