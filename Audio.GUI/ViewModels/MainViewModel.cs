using Audio.Entries;
using Audio.GUI.Models;
using Audio.GUI.Services;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Audio.GUI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly AudioManager _audioManager;
    private readonly LogViewModel _logViewModel;
    private readonly TreeViewModel _treeViewModel;
    private readonly EntryViewModel _entryViewModel;

    private string? _lastDirectory;
    [ObservableProperty]
    private bool convert;

    private IPlatformServiceProvider? _platformServiceProvider;
    private IPlatformServiceProvider PlatformServiceProvider
    {
        get
        {
            return _platformServiceProvider ??= Ioc.Default.GetRequiredService<IPlatformServiceProvider>();
        }
    }
    public LogViewModel LogViewModel => _logViewModel;
    public TreeViewModel TreeViewModel => _treeViewModel;
    public EntryViewModel EntryViewModel => _entryViewModel;
    public string? VOPath
    {
        get => ConfigManager.Instance.VOPath;
        set
        {
            ConfigManager.Instance.VOPath = value;
            ConfigManager.Instance.Save();
        }
    }
    public string? EventPath
    {
        get => ConfigManager.Instance.EventPath;
        set
        {
            ConfigManager.Instance.EventPath = value;
            ConfigManager.Instance.Save();
        }
    }

    public MainViewModel()
    {
        _audioManager = new();
        _logViewModel = new();
        _entryViewModel = new(_audioManager);
        _treeViewModel = new(_audioManager, _entryViewModel);

        ConfigManager.Instance.Load();

        Convert = ConfigManager.Instance.Convert;
    }

    [RelayCommand]
    public async Task LoadFile()
    {
        if (PlatformServiceProvider.StorageProvider !=  null)
        {
            IStorageFolder? lastDirectory = await PlatformServiceProvider.StorageProvider.TryGetFolderFromPathAsync(_lastDirectory);
            IReadOnlyList<IStorageFile> files = await PlatformServiceProvider.StorageProvider.OpenFilePickerAsync(new() { Title = "Pick file(s)", AllowMultiple = true, SuggestedStartLocation = lastDirectory });
            IEnumerable<string> paths = files.Select(x => x.TryGetLocalPath() ?? "");

            _lastDirectory = Path.GetDirectoryName(paths.FirstOrDefault());
            await LoadFiles(paths);
        }
    }
    
    [RelayCommand]
    public async Task LoadFolder()
    {
        if (PlatformServiceProvider.StorageProvider != null)
        {
            IStorageFolder? lastDirectory = await PlatformServiceProvider.StorageProvider.TryGetFolderFromPathAsync(_lastDirectory);
            IReadOnlyList<IStorageFolder> folders = await PlatformServiceProvider.StorageProvider.OpenFolderPickerAsync(new() { SuggestedStartLocation = lastDirectory });

            List<string> files = [];
            foreach (IStorageFolder folder in folders)
            {
                string? path = folder.TryGetLocalPath();
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                {
                    files.AddRange(Directory.GetFiles(path, "*.*", SearchOption.AllDirectories));
                }
            }

            _lastDirectory = Path.GetDirectoryName(files.FirstOrDefault());
            await LoadFiles(files);
        }
    }
    
    [RelayCommand]
    public async Task ExportSound() => await ExportEntry([EntryType.Sound, EntryType.EmbeddedSound, EntryType.External]);

    [RelayCommand]
    public async Task ExportBank() => await ExportEntry([EntryType.Bank]);

    [RelayCommand]
    public async Task ExportAll() => await ExportEntry([EntryType.Bank, EntryType.Sound, EntryType.EmbeddedSound, EntryType.External]);
    
    [RelayCommand]
    public async Task ExportInfo()
    {
        if (PlatformServiceProvider.StorageProvider != null)
        {
            IReadOnlyList<IStorageFolder> folders = await PlatformServiceProvider.StorageProvider.OpenFolderPickerAsync(new() { AllowMultiple = false });

            if (folders.Any())
            {
                string? path = folders[0].TryGetLocalPath();
                if (Directory.Exists(path) && Directory.Exists(_lastDirectory))
                {
                    await Task.Run(() => _audioManager.DumpInfos(_lastDirectory, path));
                }
            }
        }
    }
    [RelayCommand]
    public async Task ExportEvent()
    {
        if (PlatformServiceProvider.StorageProvider != null)
        {
            IReadOnlyList<IStorageFolder> folders = await PlatformServiceProvider.StorageProvider.OpenFolderPickerAsync(new() { AllowMultiple = false });

            if (folders.Any())
            {
                string? path = folders[0].TryGetLocalPath();
                if (Directory.Exists(path))
                {
                    await Task.Run(() => _audioManager.DumpEvents(path));
                }
            }
        }
    }

    [RelayCommand]
    public async Task SetVOPath()
    {
        if (PlatformServiceProvider.StorageProvider != null)
        {
            IReadOnlyList<IStorageFile> files = await PlatformServiceProvider.StorageProvider.OpenFilePickerAsync(new() { Title = "Pick file" });
            IEnumerable<string> paths = files.Select(x => x.TryGetLocalPath() ?? "");
            if (paths.Any())
            {
                VOPath = paths.First();
            }
        }
    }
    
    [RelayCommand]
    public async Task SetEventPath()
    {
        if (PlatformServiceProvider.StorageProvider != null)
        {
            IReadOnlyList<IStorageFile> files = await PlatformServiceProvider.StorageProvider.OpenFilePickerAsync(new() { Title = "Pick file" });
            IEnumerable<string> paths = files.Select(x => x.TryGetLocalPath() ?? "");
            if (paths.Any())
            {
                EventPath = paths.First();
            }
        }
    }
    
    [RelayCommand]
    public async Task LoadVO()
    {
        if (string.IsNullOrEmpty(VOPath))
        {
            Logger.Warning("VO path must be set first !!");
            return;
        }

        await Task.Run(() => _audioManager.UpdateExternals(File.ReadAllLines(VOPath)));
        await _treeViewModel.Update();
    }
    
    [RelayCommand]
    public async Task LoadEvents()
    {
        if (string.IsNullOrEmpty(EventPath))
        {
            Logger.Warning("Event path must be set first !!");
            return;
        }

        await Task.Run(() => _audioManager.UpdatedEvents(File.ReadAllLines(EventPath)));
        await Task.Run(_audioManager.ProcessEvents);
        await _treeViewModel.Update();
    }

    public async Task LoadFiles(IEnumerable<string> files)
    {
        if (files.Any())
        {
            _audioManager.Clear();
            int loaded = await Task.Run(() => _audioManager.LoadFiles([.. files]));
            if (loaded > 0)
            {
                await _treeViewModel.Update();
            }
        }
    }

    private async Task ExportEntry(IEnumerable<EntryType> types)
    {
        if (PlatformServiceProvider.StorageProvider != null)
        {
            IReadOnlyList<IStorageFolder> folders = await PlatformServiceProvider.StorageProvider.OpenFolderPickerAsync(new() { AllowMultiple = false });

            IStorageFolder? folder = folders.FirstOrDefault();
            if (folder != null)
            {
                string? path = folder.TryGetLocalPath();
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                {
                    if (_treeViewModel.Checked.Any())
                    {
                        await Task.Run(() => _audioManager.DumpEntries(path, _treeViewModel.Checked.OfType<EntryTreeNode>().Select(x => x.Entry)));
                    }
                    else
                    {
                        await Task.Run(() => _audioManager.DumpEntries(path, types));
                    }
                }
            }
        }
    }

    partial void OnConvertChanged(bool value)
    {
        _audioManager.Convert = value;

        ConfigManager.Instance.Convert = value;
        ConfigManager.Instance.Save();
    }
}
