namespace Audio.Entries;

public record EventTag(FNVID<uint> Type, FNVID<uint> Value);
public record EventInfo(FNVID<uint> ID)
{
    public Dictionary<HashSet<EventTag>, HashSet<FNVID<uint>>> Groups { get; set; } = new Dictionary<HashSet<EventTag>, HashSet<FNVID<uint>>>(HashSet<EventTag>.CreateSetComparer());
    public IEnumerable<FNVID<uint>> IDs => Groups.Values.SelectMany(x => x);
    public IEnumerable<EventTag> Tags => Groups.Keys.SelectMany(x => x);
    public Stack<EventTag> TagStack { get; } = [];
    public void AddTarget(FNVID<uint> id)
    {
        HashSet<EventTag> tags = [.. TagStack];

        if (!Groups.TryGetValue(tags, out HashSet<FNVID<uint>>? ids))
        {
            Groups[tags] = ids = [];
        }

        ids.Add(id);
    }
    public IEnumerable<IGrouping<FNVID<uint>, EventTag>> GetGroupsByID(FNVID<uint> id)
    {
        foreach (IGrouping<FNVID<uint>, EventTag> group in Groups.Where(tc => tc.Value.Contains(id)).SelectMany(x => x.Key).ToHashSet().GroupBy(x => x.Type))
        {
            yield return group;
        }
    }
    public IEnumerable<FNVID<uint>> GetIDsByTags(HashSet<EventTag> tags)
    {
        foreach((_, HashSet<FNVID<uint>> ids) in Groups.Where(x => x.Key.SetEquals(tags)))
        {
            foreach (FNVID<uint> id in ids)
            {
                yield return id;
            }
        }
    }
}