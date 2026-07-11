using GameInterface.Services.ObjectManager;
using System;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class ItemObjectBinaryPackage : BinaryPackageBase<ItemObject>
    {

        public string stringId;

        public ItemObjectBinaryPackage(ItemObject obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        protected override void PackInternal()
        {
            stringId = ResolveId(Object);

            base.PackFields();
        }

        protected override void UnpackInternal()
        {
            var resolvedObj = ResolveObject<ItemObject>(stringId);
            if (resolvedObj != null)
            {
                Object = resolvedObj;
                return;
            }

            // The coop registry can miss a valid item: ItemObjectRegistry.RegisterAllObjects snapshots
            // MBObjectManager's item list once, so an item that lands in MBObjectManager after that
            // snapshot (a load-order timing gap seen between game builds) is absent from the coop
            // registry even though the game has it. Items are static XML data, so consult the game's
            // authoritative MBObjectManager by StringId before reconstructing. Full reconstruction of a
            // real item is wrong (it builds a duplicate) and, on the headless dedicated server, fatal —
            // it invokes a native mesh/visual callback exposed only as an UnmanagedCallersOnly
            // placeholder, aborting the process.
            var gameItem = MBObjectManager.Instance?.GetObject<ItemObject>(ObjectManager.Compact(stringId, typeof(ItemObject)));
            if (gameItem != null)
            {
                Object = gameItem;
                return;
            }

            base.UnpackFields();
        }
    }
}
