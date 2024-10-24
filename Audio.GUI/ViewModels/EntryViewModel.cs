using Audio.Entries;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Audio.GUI.ViewModels;

public partial class EntryViewModel : ViewModelBase
{
    private readonly PlayerViewModel _playerViewModel;

    [ObservableProperty]
    private string infoText = "";

    [ObservableProperty]
    private Entry? entry;

    public PlayerViewModel PlayerViewModel => _playerViewModel;

    public EntryViewModel()
    {
        _playerViewModel = new();
    }
    partial void OnEntryChanged(Entry? value)
    {
        if (value != null)
        {
            StringBuilder sb = new();
            sb.AppendLine($"Name: {value.Name}");
            sb.AppendLine($"Type: {value.Type}");
            sb.AppendLine($"Offset: {value.Offset}");
            sb.AppendLine($"Size: {value.Size}");
            sb.AppendLine($"Location: {value.Location}");
            sb.AppendLine($"Source: {value.Source}");
            if (value is TaggedEntry<uint> taggedEntry && taggedEntry.Events.Count > 0)
            {
                sb.AppendLine($"Events: ");
                foreach(KeyValuePair<FNVID<uint>, HashSet<EventTag>> evt in taggedEntry.Events)
                {
                    sb.AppendLine($"\t{evt.Key}:");
                    foreach(IGrouping<FNVID<uint>, EventTag> group in evt.Value.GroupBy(x => x.Type))
                    {
                        sb.AppendLine($"\t\t{group.Key}: [{string.Join(',', group.Select(x => x.Value))}]");
                    }
                }
            }

            InfoText = sb.ToString();
            Task.Run(() => _playerViewModel.LoadAudio(value));
        }
    }
}