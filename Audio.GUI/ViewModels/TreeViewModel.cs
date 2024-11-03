using Audio.Entries;
using Audio.GUI.Models;
using Audio.GUI.Services;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Audio.GUI.ViewModels;

public partial class TreeViewModel : ViewModelBase
{
    private static readonly char[] _separators = [
        Path.DirectorySeparatorChar,
        Path.AltDirectorySeparatorChar
    ];

    private readonly AudioManager _audioManager;
    private readonly EntryViewModel _entryViewModel;

    private IPlatformServiceProvider? _platformServiceProvider;

    [ObservableProperty]
    private string searchText = "";

    [ObservableProperty]
    private bool isEnabled;

    private IPlatformServiceProvider PlatformServiceProvider
    {
        get
        {
            return _platformServiceProvider ??= Ioc.Default.GetRequiredService<IPlatformServiceProvider>();
        }
    }

    public HierarchicalTreeDataGridSource<TreeNode> Source { get; private set; }
    public IEnumerable<TreeNode> Checked
    {
        get
        {
            foreach(TreeNode node in Source.Items)
            {
                foreach(TreeNode checkedNode in node.Checked)
                {
                    yield return checkedNode;
                }
            }
        }
    } 

    public TreeViewModel(AudioManager audioManager, EntryViewModel entryViewModel)
    {
        _audioManager = audioManager;
        _entryViewModel = entryViewModel;

        Source = new HierarchicalTreeDataGridSource<TreeNode>([])
        {
            Columns =
            {
                new CheckBoxColumn<TreeNode>(null, x => x.IsChecked, (o, v) => o.IsChecked = v, new GridLength(0.1, GridUnitType.Star), new()
                {
                    CanUserResizeColumn = false,
                }),
                new HierarchicalExpanderColumn<TreeNode>(
                    new TextColumn<TreeNode, string>(null, x => x.Name, new GridLength(1, GridUnitType.Star), new() 
                    { 
                        IsTextSearchEnabled = true,
                        CanUserResizeColumn = false
                    }),
                    x => x.Nodes,
                    x => x.Nodes.Count > 0,
                    x => x.IsExpanded
                ),
            },
        };
        Source.RowSelection!.SingleSelect = true;
        Source.RowSelection!.SelectionChanged += TreeViewModel_SelectionChanged;
    }

    [RelayCommand]
    public void Expand(TappedEventArgs e)
    {
        if (Source.RowSelection!.SelectedIndex > -1)
        {
            if (Source.RowSelection!.SelectedItem?.IsExpanded == true)
            {
                Source.Collapse(Source.RowSelection!.SelectedIndex);
            }
            else
            {
                Source.Expand(Source.RowSelection!.SelectedIndex);
            }
        }
    }
    [RelayCommand]
    public async Task Refresh(KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await Update();
        }
    }
    [RelayCommand]
    public async Task Copy(KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.C)
        {
            if (PlatformServiceProvider.Clipboard != null && Source.RowSelection!.SelectedItem != null)
            {
                await PlatformServiceProvider.Clipboard.SetTextAsync(Source.RowSelection!.SelectedItem.Name);
            }
        }
    }
    public async Task Update()
    {
        IsEnabled = false;
        List<TreeNode> nodes = await Task.Run(BuildTree);
        await Dispatcher.UIThread.InvokeAsync(() => Source.Items = nodes);
        IsEnabled = true;
    }
    private void TreeViewModel_SelectionChanged(object? sender, Avalonia.Controls.Selection.TreeSelectionModelSelectionChangedEventArgs<TreeNode> e)
    {
        if (e.SelectedItems.FirstOrDefault() is EntryTreeNode entryTreeNode)
        {
            _entryViewModel.Entry = entryTreeNode;
        }
    }
    private List<TreeNode> BuildTree()
    {
        List<TreeNode> nodes = [];

        IEnumerable<uint> targetIDs = _audioManager.Events.SelectMany(x => x.IDs).Select(x => x.Value).ToHashSet();
        IEnumerable<Entry> eventEntries = _audioManager.Entries.Where(x => targetIDs.Contains((uint)x.ID));
        IEnumerable<Entry> locationEntries = _audioManager.Entries.Except(eventEntries);

        BuildEventTree(nodes, eventEntries);
        BuildLocationTree(nodes, locationEntries);
        Filter(nodes);

        return nodes;
    }
    private void BuildLocationTree(List<TreeNode> nodes, IEnumerable<Entry> entries, TreeNode? parent = null, int index = 0)
    {
        foreach (IGrouping<string?, Entry> group in entries.Where(x => x.Location?.Split(_separators).Length > index).GroupBy(x => Path.ChangeExtension(x.Location?.Split(_separators)[index], null)))
        {
            if (!string.IsNullOrEmpty(group.Key))
            {
                TreeNode? node = null;

                if (!group.Any(x => Path.ChangeExtension(x.Name, null)?.EndsWith(group.Key) == true))
                {
                    node = new TreeNode() { Name = group.Key };
                }
                else
                {
                    node = new EntryTreeNode(group.First()) { Name = group.Key };
                }

                if (parent == null)
                {
                    nodes.Add(node);
                }
                else
                {
                    parent.Nodes.Add(node);
                }

                BuildLocationTree(nodes, group, node, index + 1);
            }
        }
    }
    private void BuildEventTree(List<TreeNode> nodes, IEnumerable<Entry> entries)
    {
        ILookup<ulong, Entry> entryLookup = entries.ToLookup(x => x.ID);

        foreach (EventInfo eventInfo in _audioManager.Events)
        {
            TreeNode eventNode = new() { Name = eventInfo.ID.ToString() };
            nodes.Add(eventNode);

            foreach (FNVID<uint> id in eventInfo.IDs)
            {
                foreach (Entry entry in entryLookup[id])
                {
                    EntryTreeNode entryTreeNode = new(entry) { Name = entry.Name ?? "", EventInfo = eventInfo };
                    eventNode.Nodes.Add(entryTreeNode);
                }
            }
        }
    }
    void Filter(IList<TreeNode> nodes)
    {
        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            TreeNode node = nodes[i];

            if (node.HasMatch(SearchText))
            {
                continue;
            }

            if (node.Nodes.Count > 0)
            {
                Filter(node.Nodes);
            }

            if (node.Nodes.Count == 0)
            {
                nodes.RemoveAt(i);
            }
        }
    }
    partial void OnSearchTextChanging(string? oldValue, string newValue)
    {
        if (!string.IsNullOrEmpty(newValue))
        {
            try
            {
                Regex.Match("", newValue, RegexOptions.IgnoreCase);
            }
            catch (ArgumentException)
            {
                throw new DataValidationException("Not a valid Regex value");
            }
        }
    }
}