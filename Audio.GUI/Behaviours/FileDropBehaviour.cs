using Audio.GUI.ViewModels;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Xaml.Interactions.DragAndDrop;
using System.Collections.Generic;
using System.IO;

namespace Audio.GUI.Behaviours;
public class FileDropBehaviour : DropHandlerBase
{
    public override bool Execute(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        if (targetContext is MainViewModel mainViewModel)
        {
            List<string> files = [];

            foreach(IStorageItem? storageItem in e.Data.GetFiles() ?? [])
            {
                string? path = storageItem.TryGetLocalPath();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    if (Directory.Exists(path))
                    {
                        files.AddRange(Directory.GetFiles(path, "*.*", SearchOption.AllDirectories));
                    }
                    else if (File.Exists(path))
                    {
                        files.Add(path);
                    }
                }
            }

            _ = mainViewModel.LoadFiles(files);

            return true;
        }

        return false;
    }

    public override bool Validate(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        return targetContext is MainViewModel && e.Data.Contains(DataFormats.Files);
    }
}
