namespace Mirai.Space.Extensions.ComponentModel.Tests;

public sealed class WriteOnlyModel
{
    public int LastValue { get; private set; }

    public int Secret
    {
        set => LastValue = value;
    }
}
