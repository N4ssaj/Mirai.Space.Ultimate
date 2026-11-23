using System.ComponentModel;

namespace Mirai.Space.Extensions.ComponentModel.Tests;

public sealed class ObservableModel : INotifyPropertyChanged
{
    private string _value = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Value
    {
        get => _value;
        set
        {
            _value = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }
    }
}
