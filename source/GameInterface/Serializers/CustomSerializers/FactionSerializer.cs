﻿//using GameInterface.Serializers;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using TaleWorlds.CampaignSystem;

//namespace Coop.Mod.Serializers.Custom
//{
//    public class FactionSerializer : ICustomSerializer
//    {
//        CustomSerializerBase serializer;

//        public FactionSerializer(IFaction faction)
//        {
//            if(faction is Clan clan)
//            {
//                serializer = new ClanSerializer(clan);
//            }
//            else if(faction is Kingdom kingdom)
//            {
//                serializer = new KingdomSerializer(kingdom);
//            }
//            else
//            {
//                throw new ArgumentException($"{faction.GetType()} is not handled by serializer.");
//            }
//        }

//        public object Deserialize()
//        {
//            return serializer.Deserialize();
//        }

//        public void ResolveReferences()
//        {
//            serializer.ResolveReferences();
//        }
//    }
//}