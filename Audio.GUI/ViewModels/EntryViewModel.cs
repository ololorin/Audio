using Audio.Entries;
using Audio.GUI.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Audio.GUI.ViewModels;

public partial class EntryViewModel : ViewModelBase
{
    private readonly AudioManager _audioManager;
    private readonly PlayerViewModel _playerViewModel;

    [ObservableProperty]
    private string infoText = "";

    [ObservableProperty]
    private EntryTreeNode? entry;

    public PlayerViewModel PlayerViewModel => _playerViewModel;

    public EntryViewModel(AudioManager audioManager)
    {
        _audioManager = audioManager;
        _playerViewModel = new();
    }
    partial void OnEntryChanged(EntryTreeNode? value)
    {
        if (value != null)
        {
            StringBuilder sb = new();
            sb.AppendLine($"Name: {value.Entry.Name}");
            sb.AppendLine($"Type: {value.Entry.Type}");
            sb.AppendLine($"Offset: {value.Entry.Offset}");
            sb.AppendLine($"Size: {value.Entry.Size}");
            sb.AppendLine($"Location: {value.Entry.Location}");
            sb.AppendLine($"Source: {value.Entry.Source}");
            if (value.EventInfo != null && value.EventInfo.Groups.Any(x => x.Key.Count > 0))
            {
                sb.AppendLine($"Tags: ");
                foreach (IGrouping<FNVID<uint>, EventTag> group in value.EventInfo.GetGroupsByID((uint)value.Entry.ID))
                {
                    sb.AppendLine($"\t{group.Key}:");
                    foreach(EventTag tag in group)
                    {
                        sb.AppendLine($"\t\t{tag.Value}");
                    }
                }
            }

            InfoText = sb.ToString();
            if (value.Entry is AudioEntry audioEntry)
            {
                Task.Run(() => _playerViewModel.LoadAudio(audioEntry));
            }
        }
    }
}