using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Audio.GUI.ViewModels;

public partial class LogViewModel : ViewModelBase, ILogger, IDisposable
{
    private bool _scrolling = false;

    [ObservableProperty]
    private ObservableCollection<string> logs = [];


    public LogViewModel()
    {
        Logger.TryRegister(this);
    }

    [RelayCommand]
    public void Clear()
    {
        Logs.Clear();
    }
    [RelayCommand]
    public async Task ScrollToEnd(ScrollChangedEventArgs e)
    {
        if (!_scrolling && e.Source is ScrollViewer scrollViewer && e.ExtentDelta != Avalonia.Vector.Zero)
        {
            _scrolling = true;
            await Dispatcher.UIThread.InvokeAsync(scrollViewer.ScrollToEnd);
            _scrolling = false;
        }
    }
    public void Log(LogLevel logLevel, string message)
    {
        Logs.Add($"[{logLevel}]: {message}");
    }
    public void Dispose()
    {
        Logger.TryUnregister(this);
        GC.SuppressFinalize(this);
    }
}