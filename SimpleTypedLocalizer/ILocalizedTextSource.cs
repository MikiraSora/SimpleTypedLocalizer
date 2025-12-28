using System.ComponentModel;

namespace SimpleTypedLocalizer;

public interface ILocalizedTextSource : INotifyPropertyChanged
{
    public string Text { get; }
}