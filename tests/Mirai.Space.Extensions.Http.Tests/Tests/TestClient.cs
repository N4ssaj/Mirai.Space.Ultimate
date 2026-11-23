using Mirai.Space.Extensions.Http.Abstractions;

namespace Mirai.Space.Extensions.Http.Tests;

internal sealed class TestClient(IRequestService requests)
{
    public IRequestService Requests { get; } = requests;
}
