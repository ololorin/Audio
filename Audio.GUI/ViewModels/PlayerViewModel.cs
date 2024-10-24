using Audio.Entries;
using CommunityToolkit.Mvvm.ComponentModel;
using LibVLCSharp.Shared;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Audio.GUI.ViewModels;

public partial class PlayerViewModel : ViewModelBase, IDisposable
{
    private readonly MediaPlayer _mediaPlayer;
    private readonly LibVLC _context;

    [ObservableProperty]
    private float position;
    [ObservableProperty]
    private float volume;
    [ObservableProperty]
    private TimeSpan time;
    [ObservableProperty]
    private TimeSpan duration;
    [ObservableProperty]
    private bool isChecked;

    private MemoryStream? _stream;

    public PlayerViewModel()
    {
        _context = new();
        _mediaPlayer = new(_context);
        _mediaPlayer.PositionChanged += MediaPlayer_PositionChanged;
        _mediaPlayer.LengthChanged += MediaPlayer_LengthChanged;
        _mediaPlayer.TimeChanged += MediaPlayer_TimeChanged;
        _mediaPlayer.EndReached += MediaPlayer_EndReached;

        Volume = 100;
    }

    public void LoadAudio(Entry? entry)
    {
        if (entry == null) return;
        else if (entry.Type == EntryType.Bank)
        {
            Logger.Warning("Playing Bank type is not supported !!");
            return;
        }

        Logger.Info($"Attempting to load audio {entry.Location}");

        IsChecked = false;
        MemoryStream memoryStream = new();
        if (entry.TryConvert(memoryStream, out _))
        {
            _stream?.Dispose();
            _stream = memoryStream;

            Position = 0;
            IsChecked = true;
            _mediaPlayer.Media = new Media(_context, new StreamMediaInput(_stream));
            _mediaPlayer.Play();

            Logger.Info($"{entry.Location} loaded successfully");
            return; 
        }

        
        Logger.Info($"Unable to load {entry.Location}");
        return;
    }
    private void MediaPlayer_PositionChanged(object? sender, MediaPlayerPositionChangedEventArgs e)
    {
        position = e.Position * 100.0f;
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Position)));
    }
    private void MediaPlayer_LengthChanged(object? sender, MediaPlayerLengthChangedEventArgs e)
    {
        Duration = TimeSpan.FromMilliseconds(e.Length == -1 ? 0 : e.Length);
    }
    private void MediaPlayer_TimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
    {
        Time = TimeSpan.FromMilliseconds(e.Time == -1 ? 0 : e.Time);
    }
    private void MediaPlayer_EndReached(object? sender, EventArgs e)
    {
        Position = 0;
        isChecked = false;
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsChecked)));
    }
    partial void OnPositionChanged(float value)
    {
        _mediaPlayer.Position = value / 100.0f; 
    }
    partial void OnVolumeChanged(float value)
    {
        _mediaPlayer.Volume = (int)value;
    }
    partial void OnIsCheckedChanged(bool value)
    {
        switch (_mediaPlayer.State)
        {
            case VLCState.Ended:
                _mediaPlayer.Stop();
                OnIsCheckedChanged(value);
                break;
            case VLCState.Paused:
            case VLCState.Stopped:
                _mediaPlayer.Play();
                break;
            case VLCState.Playing:
                _mediaPlayer.Pause();
                break;
        }
    }
    public void Dispose()
    {
        _stream?.Dispose();
        _mediaPlayer.Dispose();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}