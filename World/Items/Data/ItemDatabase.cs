using System.Collections.Generic;
using Godot;

namespace RPG2d.World.Items.Data;

public static class ItemDatabase
{
    private static Dictionary<string, ItemData> _items;
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        _items = new Dictionary<string, ItemData>();
        ScanDirectory("res://World/Items");
        _initialized = true;
    }

    private static void ScanDirectory(string path)
    {
        using var dir = DirAccess.Open(path);
        if (dir == null) return;

        foreach (var file in dir.GetFiles())
        {
            if (file.EndsWith(".tres"))
                TryLoad(path.PathJoin(file));
        }

        foreach (var subDir in dir.GetDirectories())
            ScanDirectory(path.PathJoin(subDir));
    }

    private static void TryLoad(string path)
    {
        var resource = GD.Load<Resource>(path);
        if (resource is ItemData itemData && !string.IsNullOrEmpty(itemData.ItemId))
            _items[itemData.ItemId] = itemData;
    }

    public static ItemData Get(string itemId)
    {
        if (!_initialized) Initialize();
        return itemId != null ? _items.GetValueOrDefault(itemId) : null;
    }
}
