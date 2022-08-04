using System;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace Coop.Mod.Serializers
{
    [Serializable]
    class PlayerLordPartyComponentSerializer : ICustomSerializer
    {
        [NonSerialized]
        public Hero hero;

        public PlayerLordPartyComponentSerializer(LordPartyComponent component)
        {
        }

        public object Deserialize()
        {
            LordPartyComponent lordPartyComponent = (LordPartyComponent)Activator.CreateInstance(
                typeof(LordPartyComponent),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { hero, hero },
                null);

            // Circular referenced object needs assignment before deserialize
            if (hero == null)
            {
                throw new SerializationException("Must set hero reference before deserializing. Use SetHeroReference()");
            }

            lordPartyComponent.GetType()
                .GetProperty("Owner")
                .GetSetMethod(true)
                .Invoke(lordPartyComponent, new object[] { hero });

            return lordPartyComponent;
        }

        public void SetHeroReference(Hero hero)
        {
            this.hero = hero;
        }

        public void ResolveReferenceGuids()
        {
            // Do nothing
        }

        public byte[] Serialize()
        {
            throw new NotImplementedException();
        }
    }
}
