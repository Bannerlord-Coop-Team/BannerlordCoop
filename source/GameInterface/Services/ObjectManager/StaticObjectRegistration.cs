using Common.Logging;
using Serilog;
using System;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.ObjectManager;

/// <summary>
/// Repairs a static MB object that was loaded after Coop's initial registry pass.
/// The normal static registry convention is TypeName_StringId on both peers.
/// This helper never replaces or aliases a collision.
/// </summary>
public static class StaticObjectRegistration
{
    private static readonly ILogger Logger =
        LogManager.GetLogger(typeof(StaticObjectRegistration));

    public static bool TryEnsure<T>(
        IObjectManager objectManager,
        T value,
        out string id)
        where T : MBObjectBase
    {
        id = null;
        if (objectManager == null || value == null)
            return false;
        if (objectManager.TryGetId(value, out id))
        {
            id = ObjectManager.Compact(id, typeof(T));
            return true;
        }
        if (string.IsNullOrEmpty(value.StringId))
            return false;

        string candidate = $"{typeof(T).Name}_{value.StringId}";
        if (objectManager.TryGetObject(candidate, out T existing))
        {
            if (ReferenceEquals(existing, value))
            {
                id = value.StringId;
                return true;
            }

            // A late XML override can replace Bannerlord's authoritative static
            // instance after Coop's initial registry scan.  Replace the stale
            // mapping only when the native MBObjectManager confirms that the
            // incoming instance is now the canonical object for this StringId.
            // An arbitrary same-id object remains a hard collision.
            T nativeObject = MBObjectManager.Instance?.GetObject<T>(value.StringId);
            if (ReferenceEquals(nativeObject, value) &&
                objectManager.ReplaceExisting(candidate, existing, value))
            {
                id = value.StringId;
                Logger.Warning(
                    "Replaced stale static object registration {Id} while packing a late module override",
                    candidate);
                return true;
            }

            Logger.Error(
                "Static object registration collision at {Id}: existing={ExistingType}, incoming={IncomingType}",
                candidate,
                existing?.GetType().FullName,
                value.GetType().FullName);
            return false;
        }

        if (!objectManager.AddExisting(candidate, value) ||
            !objectManager.TryGetObject(candidate, out T registered) ||
            !ReferenceEquals(registered, value))
            return false;

        id = value.StringId;
        Logger.Warning(
            "Late-registered static object {Id}; module content loaded after Coop's initial registry pass",
            candidate);
        return true;
    }

    /// <summary>
    /// Resolves a deterministic static Coop id even when the local object was
    /// loaded after the initial registry scan. Dynamic ids are intentionally
    /// never guessed here.
    /// </summary>
    public static bool TryResolve<T>(
        IObjectManager objectManager,
        string id,
        out T value)
        where T : MBObjectBase
    {
        value = null;
        if (objectManager == null || string.IsNullOrEmpty(id))
            return false;
        string prefix = $"{typeof(T).Name}_";
        string stringId = id.StartsWith(prefix, StringComparison.Ordinal)
            ? id.Substring(prefix.Length)
            : id;
        if (string.IsNullOrEmpty(stringId))
            return false;

        T nativeObject = MBObjectManager.Instance?.GetObject<T>(stringId);
        if (nativeObject == null)
            return objectManager.TryGetObject(id, out value);

        if (objectManager.TryGetId(nativeObject, out var nativeId))
        {
            if (!string.Equals(
                    ObjectManager.Compact(nativeId, typeof(T)),
                    stringId,
                    StringComparison.Ordinal))
                return false;
            value = nativeObject;
            return true;
        }

        string candidate = prefix + stringId;
        if (objectManager.TryGetObject(stringId, out T stale) &&
            !ReferenceEquals(stale, nativeObject))
        {
            if (!objectManager.ReplaceExisting(candidate, stale, nativeObject))
                return false;
            Logger.Warning(
                "Replaced stale static object registration {Id} after a late module override",
                candidate);
        }
        else if (!TryEnsure(objectManager, nativeObject, out var registeredId) ||
                 !string.Equals(registeredId, stringId, StringComparison.Ordinal))
            return false;

        value = nativeObject;
        return true;
    }
}
