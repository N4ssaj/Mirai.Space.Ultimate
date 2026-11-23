using System.ComponentModel;

namespace Mirai.Space.Extensions.ComponentModel.Tests;

public sealed class AddressModel
{
    [Description("The city name.")]
    public string City { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;
}
