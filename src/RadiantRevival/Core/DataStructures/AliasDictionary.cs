using System.Collections.Generic;
using System.Linq;

namespace RadiantRevival.Core.DataStructures;

public class AliasDictionary<TKey, TValue> : List<(HashSet<TKey> Keys, List<TValue> Items)>
{
    public List<TValue> this[TKey name]
    {
        get
        {
            if (!TryFind(name, out var elements))
            {
                throw new KeyNotFoundException();
            }

            return elements;
        }
    }

    /// <inheritdoc cref="List{T}.Add" />
    public bool Add(TKey key, TValue item)
    {
        var inList = FindIndex(e => e.Keys.Contains(key));

        if (inList != -1)
        {
            this[inList].Items.Add(item);
        }
        else
        {
            Add(([key], [item]));
        }

        return inList == -1;
    }

    /// <inheritdoc cref="List{T}.Add" />
    public bool Add(TKey key, List<TValue> items)
    {
        return Add([key], items);
    }

    /// <inheritdoc cref="List{T}.Add" />
    public bool Add(IEnumerable<TKey> keys, List<TValue> items)
    {
        return Add(keys.ToHashSet(), items);
    }

    /// <inheritdoc cref="List{T}.Add" />
    public bool Add(HashSet<TKey> keys, TValue item)
    {
        return Add(keys, [item]);
    }

    /// <inheritdoc cref="List{T}.Add" />
    public bool Add(HashSet<TKey> keys, List<TValue> items)
    {
        var inList = FindIndex(e => keys.All(k => e.Keys.Contains(k)));

        if (inList != -1)
        {
            this[inList].Items.AddRange(items);
        }
        else
        {
            Add((keys, items));
        }

        return inList == -1;
    }

    public bool TryFind(TKey key, out List<TValue> items)
    {
        foreach (var (keys, elements) in this)
        {
            if (!keys.Contains(key))
            {
                continue;
            }

            items = elements;

            return true;
        }

        items = [];

        return false;
    }
}
