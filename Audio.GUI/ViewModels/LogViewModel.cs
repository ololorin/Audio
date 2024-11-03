using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Audio.GUI.ViewModels;

public partial class LogViewModel : ViewModelBase, ILogger, IDisposable
{
    private readonly object _logLock = new();

    [ObservableProperty]
    private bool isScrolling;
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
        if (!IsScrolling && e.Source is ScrollViewer scrollViewer && e.ExtentDelta != Avalonia.Vector.Zero)
        {
            await Dispatcher.UIThread.InvokeAsync(scrollViewer.ScrollToEnd);
        }
    }
    public void Log(LogLevel logLevel, string message)
    {
        lock (_logLock)
        {
            Logs.Add($"[{logLevel}]: {message}");
        }
    }
    public void Dispose()
    {
        Logger.TryUnregister(this);
        GC.SuppressFinalize(this);
    }
}