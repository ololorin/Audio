using Audio.Chunks;
using Audio.Chunks.Types.HIRC;
using Audio.Entries;
using Audio.Extensions;

namespace Audio;

public class AudioManager
{
    private readonly List<Chunk> _chunks = [];
    private readonly List<Entry> _entries = [];
    private readonly List<EventInfo> _events = [];

    public bool Convert = false;

    public IEnumerable<Entry> Entries
    {
        get
        {
            foreach (Entry entry in _entries)
            {
                if (entry is AudioEntry audioEntry)
                {
                    audioEntry.Manager ??= this;
                }

                yield return entry;
            }
        }
    }
    public IEnumerable<HIRC> Hierarchies
    {
        get
        {
            foreach (Bank bank in Entries.OfType<Bank>())
            {
                if (bank.GetChunk(out HIRC? hirc) == true)
                {
                    hirc.BKHD ??= bank.BKHD;
                    hirc.Manager ??= this;
                    yield return hirc;
                }
            }
        }
    }
    public List<EventInfo> Events => _events;
    public void Clear()
    {
        foreach(AudioEntry audioEntry in _entries.OfType<AudioEntry>())
        {
            audioEntry.Dispose();
        }

        _events.Clear();
        _chunks.Clear();
        _entries.Clear();
        FNVID<uint>.Clear();
        FNVID<ulong>.Clear();
    }
    public int LoadFiles(string[] paths)
    {
        Logger.Info($"Loading {paths.Length} files...");

        int loaded = 0;
        foreach (string path in paths)
        {
            if (TryLoadFile(path))
            {
                Logger.Progress($"Loaded {Path.GetFileName(path)}", ++loaded, paths.Length);
            }
        }

        return loaded;
    }

    public bool TryLoadFile(string path)
    {
        try
        {
            if (Path.Exists(path))
            {
                using BankReader reader = new(path);
                if (Chunk.TryParse(reader, out Chunk? chunk))
                {
                    if (chunk is AKPK akpk)
                    {
                        akpk.LoadBanks(reader);
                        _entries.AddRange(akpk.Entries);
                    }
                    else if (chunk is BKHD bkhd)
                    {
                        Bank bank = new(bkhd, path);
                        bank.Parse(reader);
                        _entries.AddRange(bank.Entries);
                    }

                    _chunks.Add(chunk);
                    return true;
                }
            }        
        }
        catch (Exception ex)
        {
            Logger.Error($"Error while loading file {path}: {ex}");
        }

        Logger.Warning($"Unable to load file {path} !!");

        return false;
    }
    public int UpdatedEvents(string[] events)
    {
        int matched = 0;

        for (int i = 0; i < events.Length; i++)
        {
            string eventName = events[i];
            if (!FNVID<uint>.TryMatch(eventName))
            {
                Logger.Progress($"{eventName} has already been matched, skipping...", matched, events.Length);
                continue;
            }

            matched++;
        }

        Logger.Info($"Matched {matched} out of {FNVID<uint>.Count()} IDs !!");

        return matched;
    }
    public int UpdateExternals(string[] externalsPaths)
    {
        int externalsCount = Entries.OfType<External>().Count();

        int matched = 0;
        for (int i = 0; i < externalsPaths.Length; i++)
        {
            string externalName = externalsPaths[i];
            if (!FNVID<ulong>.TryMatch(externalName))
            {
                Logger.Progress($"{externalName} has already been matched, skipping...", matched, externalsPaths.Length);
                continue;
            }

            matched++;
        }

        Logger.Info($"Matched {matched} out of {externalsCount} externals !!");

        return matched;
    }
    public void ProcessEvents()
    {
        _events.Clear();

        int count = Hierarchies.Sum(x => x.Objects.OfType<Event>().Count());

        int resolved = 0;
        Parallel.ForEach(Hierarchies.Select(x => (HIRC: x, Events: x.Objects.OfType<Event>())), group =>
        {
            foreach (Event evt in group.Events)
            {
                if (evt.ID.ToString().Contains("Play_BGM_Story"))
                {
                    int x = 1;
                }

                EventInfo eventInfo = new(evt.ID);
                group.HIRC?.ResolveObject(evt, eventInfo);
                _events.Add(eventInfo);

                Logger.Progress($"Resolved {evt.ID} with {eventInfo.IDs.Count()} target audio files", ++resolved, count);
            }
        });

        Logger.Info("Done Processing !!");
    }
    public void DumpEvents(string outputDirectory)
    {
        string outputPath = Path.Combine(outputDirectory, "Audio", "Infos", "tags.json");
        try
        {
            ILookup<ulong, Entry> entryLookup = _entries.ToLookup(x => x.ID);
            List<object> events = [];

            foreach (EventInfo eventInfo in _events)
            {
                foreach (FNVID<uint> id in eventInfo.IDs)
                {
                    foreach (Entry entry in entryLookup[id])
                    {
                        events.Add(new
                        {
                            Name = eventInfo.ID.ToString(),
                            entry.Location,
                            Tags = eventInfo.GetGroupsByID(id).ToDictionary(x => x.Key.ToString(), x => x.Select(y => y.Value.ToString()))
                        });
                    }
                }
            }

            string? outputFolder = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            
            using Stream stream = File.Open(outputPath, FileMode.Create);
            events.Serialize(stream);

            Logger.Info($"Dumped {Path.GetFileName(outputPath)} !!");
        }
        catch (Exception e)
        {
            Logger.Error($"Unable to dump {outputPath}, {e}");
        }
    }
    public void DumpInfos(string inputDirectory, string outputDirectory)
    {
        AKPK[] akpks = _chunks.OfType<AKPK>().ToArray();
        Bank[] banks = _entries.OfType<Bank>().ToArray();

        int dumped = 0;
        foreach (AKPK akpk in akpks)
        {
            if (akpk.Source == null)
            {
                Logger.Warning($"Chunk has no source, skipping...");
                continue;
            }

            string relativePath = Path.GetRelativePath(inputDirectory, akpk.Source);
            string jsonPath = Path.ChangeExtension(relativePath, ".json");
            try
            {
                string outputPath = Path.Combine(outputDirectory, "Audio", "Infos", jsonPath);
                string? outputFolder = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputFolder))
                {
                    Directory.CreateDirectory(outputFolder);
                }

                string json = akpk.Serialize();
                File.WriteAllText(outputPath, json);
                Logger.Progress($"Dumped {jsonPath}", ++dumped, akpks.Length);
            }
            catch (Exception e)
            {
                Logger.Error($"Unable to dump {jsonPath}, {e}");
            }
        }

        foreach(Bank bank in banks)
        {
            if (bank.Source == null)
            {
                Logger.Warning($"Bank has no source, skipping...");
                continue;
            }

            if (akpks.Any(x => x.Entries.Contains(bank)))
            {
                Logger.Warning($"Bank is part of packages, skipping...");
                continue;
            }

            string relativePath = Path.GetRelativePath(inputDirectory, bank.Source);
            string jsonPath = Path.ChangeExtension(relativePath, ".json");
            try
            {
                string outputPath = Path.Combine(outputDirectory, "Audio", "Infos", jsonPath);
                string? outputFolder = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputFolder))
                {
                    Directory.CreateDirectory(outputFolder);
                }

                string json = bank.Serialize();
                File.WriteAllText(outputPath, json);
                Logger.Progress($"Dumped {jsonPath}", ++dumped, akpks.Length + banks.Length);
            }
            catch (Exception e)
            {
                Logger.Error($"Unable to dump {jsonPath}, {e}");
            }
        }

        Logger.Info($"Dumped {dumped} out of {akpks.Length + banks.Length} infos !!");
    }
    public void DumpEntries(string outputDirectory, IEnumerable<EntryType> types)
    {
        Entry[] entries = Entries.Where(x => types.Contains(x.Type)).ToArray();
        DumpEntries(outputDirectory, entries);
    }
    public void DumpEntries(string outputDirectory, IEnumerable<Entry> entries)
    {
        int processed = 0;
        int count = entries.Count();
        foreach (Entry? entry in entries)
        {
            string entryOutputPath = Path.Combine(outputDirectory, entry.Type == EntryType.Bank ? "Bank" : "Audio");

            if (!string.IsNullOrEmpty(entry.Location))
            {
                string outputPath = Path.Combine(entryOutputPath, entry.Location);
                string? dirPath = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }

                if (File.Exists(outputPath))
                {
                    continue;
                }

                using FileStream fileStream = File.OpenWrite(outputPath);
                if (entry.TryWrite(fileStream))
                {
                    Logger.Progress($"Dumped {entry.Location}", ++processed, count);
                }
                else
                {
                    Logger.Warning($"Unable to dump {entry.Location}");
                }
            }
        }

        Logger.Info($"Dumped {processed} out of {count} entries !!");
    }
    internal void ResolveObject(FNVID<uint> id, EventInfo eventInfo)
    {
        foreach (HIRC hirc in Hierarchies)
        {
            hirc.ResolveObject(id, eventInfo);
        }
    }
}