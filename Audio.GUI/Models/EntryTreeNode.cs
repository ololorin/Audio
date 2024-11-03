using Audio.Entries;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace Audio.GUI.Models;
public partial class EntryTreeNode : TreeNode
{
    [ObservableProperty]
    private Entry entry;

    [ObservableProperty]
    private EventInfo? eventInfo;

    public EntryTreeNode(Entry entry)
    {
        this.entry = entry;
    }

    public override bool HasMatch(string? searchText)
    {
        if (string.IsNullOrEmpty(searchText))
        {
            return true;
        }

        Regex regex = new(searchText, RegexOptions.IgnoreCase);

        bool match = base.HasMatch(searchText);
        match |= regex.IsMatch(Entry.ID.ToString());
        match |= regex.IsMatch(Entry.Type.ToString());
        match |= regex.IsMatch(Entry.Location ?? "");
        match |= regex.IsMatch(Entry.Source ?? "");
        match |= regex.IsMatch(Entry.Offset.ToString());
        match |= regex.IsMatch(Entry.Size.ToString());

        if (EventInfo != null)
        {
            match |= regex.IsMatch(EventInfo.ID.ToString());
            foreach (IGrouping<FNVID<uint>, EventTag> group in EventInfo.GetGroupsByID((uint)Entry.ID))
            {
                match |= regex.IsMatch(group.Key.ToString());
                foreach (EventTag tag in group)
                {
                    match |= regex.IsMatch(tag.Value.ToString());
                }
            }
        }

        return match;
    }
}
