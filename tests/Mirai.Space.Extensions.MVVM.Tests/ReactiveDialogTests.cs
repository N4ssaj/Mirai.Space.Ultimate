using Mirai.Space.Extensions.MVVM.Foundation;
using Xunit;

namespace Mirai.Space.Extensions.MVVM.Tests;

public sealed class ReactiveDialogTests
{
    [Fact]
    public void Completion_IsNotCompletedBeforeClose()
    {
        var dialog = new TestDialog();

        Assert.False(dialog.Completion.IsCompleted);
    }

    [Fact]
    public async Task Close_CompletesTheDialog()
    {
        var dialog = new TestDialog();

        dialog.Close();

        await dialog.Completion;
        Assert.True(dialog.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Close_Twice_DoesNotThrow()
    {
        var dialog = new TestDialog();

        dialog.Close();
        dialog.Close();

        await dialog.Completion;
        Assert.True(dialog.Completion.IsCompletedSuccessfully);
    }

    private sealed class TestDialog : ReactiveDialog
    {
    }
}
