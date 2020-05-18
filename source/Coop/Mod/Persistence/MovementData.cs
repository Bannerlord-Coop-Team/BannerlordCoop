using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Mod.Persistence.Party
{
    public class MovementData : IEnumerable<object>
    {
        public MovementData()
        {
            Values = new List<object>();
            Values.Add(AiBehavior.None);
            Values.Add(null);
            Values.Add(null);
            Values.Add(Vec2.Invalid);
            Values.Add(0);
        }

        public MovementData(IEnumerable<object> collection)
        {
            Values = collection.ToList();
        }

        private static Type[] Types { get; } =
        {
            typeof(AiBehavior),
            typeof(Settlement),
            typeof(MobileParty),
            typeof(Vec2),
            typeof(int)
        };

        public AiBehavior DefaultBehaviour
        {
            get => (AiBehavior) Values[(int) Field.DefaultBehavior];
            set => Values[(int) Field.DefaultBehavior] = value;
        }

        public Settlement TargetSettlement
        {
            get => (Settlement) Values[(int) Field.TargetSettlement];
            set => Values[(int) Field.TargetSettlement] = value;
        }

        public MobileParty TargetParty
        {
            get => (MobileParty) Values[(int) Field.TargetParty];
            set => Values[(int) Field.TargetParty] = value;
        }

        public Vec2 TargetPosition
        {
            get => (Vec2) Values[(int) Field.TargetPosition];
            set => Values[(int) Field.TargetPosition] = value;
        }

        public int NumberOfFleeingsAtLastTravel
        {
            get => (int) Values[(int) Field.NumberOfFleeingsAtLastTravel];
            set => Values[(int) Field.NumberOfFleeingsAtLastTravel] = value;
        }

        private List<object> Values { get; }

        public IEnumerator<object> GetEnumerator()
        {
            return Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Values.GetEnumerator();
        }

        private enum Field
        {
            DefaultBehavior = 0,
            TargetSettlement = 1,
            TargetParty = 2,
            TargetPosition = 3,
            NumberOfFleeingsAtLastTravel = 4
        }
    }
}
