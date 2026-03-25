using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClaudeWatcher.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public ObservableCollection<SessionCardViewModel> Sessions { get; } = new();

    [ObservableProperty]
    private bool _hasSessions;

    public MainViewModel()
    {
        Sessions.CollectionChanged += OnSessionsChanged;
    }

    private void OnSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HasSessions = Sessions.Count > 0;
    }
}
