using System.ComponentModel;

namespace Mirai.Space.Extensions.ComponentModel.Tests;

[Description("A person described by a PolyType shape.")]
[DefaultProperty(nameof(Name))]
public sealed class PersonModel
{
    [Description("The display name.")]
    [DisplayName("Full name")]
    [Category("Identity")]
    public string Name { get; set; } = string.Empty;

    [DefaultValue(30)]
    public int Age { get; set; }

    [Browsable(false)]
    public string? Secret { get; set; }

    [TypeConverter(typeof(UpperCaseConverter))]
    public string Code { get; set; } = string.Empty;

    public string ReadOnlyValue => "fixed";

    [ReadOnly(true)]
    public string Locked { get; set; } = "locked";

    public AddressModel Address { get; set; } = new();
}
