namespace Mirai.Space.Extensions.Clone.Tests;

public sealed class ReadonlyFieldDto
{
    private readonly Address _address;

    public ReadonlyFieldDto(Address address) => _address = address;

    public Address Address => _address;
}
