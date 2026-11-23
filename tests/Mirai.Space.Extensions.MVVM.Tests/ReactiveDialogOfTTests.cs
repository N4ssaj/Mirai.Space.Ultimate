using Mirai.Space.Extensions.MVVM.Foundation;
using Xunit;

namespace Mirai.Space.Extensions.MVVM.Tests;

public sealed class ReactiveDialogOfTTests
{
    [Fact]
    public void Completion_IsNotCompletedBeforeClose()
    {
        var dialog = new TestDialog();

        Assert.False(dialog.Completion.IsCompleted);
    }

    [Fact]
    public async Task Close_CompletesWithResult()
    {
        var dialog = new TestDialog();

        dialog.Close(42);

        Assert.Equal(42, await dialog.Completion);
        Assert.True(dialog.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Close_WithDefaultValue_Completes()
    {
        var dialog = new TestDialog();

        dialog.Close(0);

        Assert.Equal(0, await dialog.Completion);
    }

    [Fact]
    public async Task Close_Twice_KeepsFirstResult()
    {
        var dialog = new TestDialog();

        dialog.Close(42);
        dialog.Close(7);

        Assert.Equal(42, await dialog.Completion);
    }

    private sealed class TestDialog : ReactiveDialog<int>
    {
    }
}
