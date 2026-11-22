using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.Engine;

namespace Missions;

public interface IMissionObjectPath
{
    string Describe(WeakGameEntity entity, WeakGameEntity root);
    WeakGameEntity Resolve(WeakGameEntity root, string path);
}

/// <summary>The indexed, named content paths shared by naval objects and missile parents.</summary>
public class MissionObjectPath : IMissionObjectPath
{
    private const int MaxPathDepth = 16;

    public string Describe(WeakGameEntity entity, WeakGameEntity root) => EntityPath(entity, root);
    public WeakGameEntity Resolve(WeakGameEntity root, string path) => ResolvePath(root, path);

    // Keep the existing naval static entry points over this same implementation.
    internal static string EntityPath(WeakGameEntity entity, WeakGameEntity root)
    {
        var parts = new List<string>();
        while (entity.IsValid && parts.Count <= MaxPathDepth)
        {
            if (entity == root)
            {
                parts.Reverse();
                return string.Join("/", parts);
            }

            var parent = entity.Parent;
            if (!parent.IsValid) return null;
            int index = parent.GetChildren().ToList().IndexOf(entity);
            if (index < 0) return null;
            parts.Add(index.ToString(CultureInfo.InvariantCulture) + ":" + entity.Name);
            entity = parent;
        }

        return null;
    }

    internal static WeakGameEntity ResolvePath(WeakGameEntity root, string path)
    {
        var entity = root;
        if (!entity.IsValid || path == null) return WeakGameEntity.Invalid;

        foreach (var part in path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = part.IndexOf(':');
            if (separator <= 0 || !int.TryParse(part.Substring(0, separator), NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
                || index >= entity.ChildCount)
                return WeakGameEntity.Invalid;

            var child = entity.GetChild(index);
            if (!child.IsValid || child.Name != part.Substring(separator + 1)) return WeakGameEntity.Invalid;
            entity = child;
        }

        return entity;
    }
}
