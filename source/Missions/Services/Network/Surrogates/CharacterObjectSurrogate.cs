using Common.Logging;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace Missions.Services.Network.Surrogates
{
    /// <summary>
    /// Sends only the object-manager registry id (the StringId) instead of the whole CharacterObject
    /// graph, and resolves the real object from the main-map server's registry on the other side.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class CharacterObjectSurrogate
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CharacterObjectSurrogate>();

        [ProtoMember(1)]
        public string Id { get; }

        public CharacterObjectSurrogate(CharacterObject obj)
        {
            if (obj == null) return;

            // Resolve against the campaign (main-map) container's registry, not the mission snapshot.
            if (GameInterface.ContainerProvider.TryResolve(out IObjectManager objectManager) == false) return;

            if (objectManager.TryGetId(obj, out var id) == false)
            {
                Logger.Error("No registry id for CharacterObject {StringId}", obj.StringId);
                return;
            }

            Id = id;
        }

        private CharacterObject Deserialize()
        {
            if (string.IsNullOrEmpty(Id)) return null;
            if (GameInterface.ContainerProvider.TryResolve(out IObjectManager objectManager) == false) return null;

            if (objectManager.TryGetObject<CharacterObject>(Id, out var character) == false)
            {
                Logger.Error("Could not resolve CharacterObject from registry id {Id}", Id);
                return null;
            }

            return character;
        }

        public static implicit operator CharacterObjectSurrogate(CharacterObject obj)
        {
            return new CharacterObjectSurrogate(obj);
        }

        public static implicit operator CharacterObject(CharacterObjectSurrogate surrogate)
        {
            return surrogate?.Deserialize();
        }
    }
}
