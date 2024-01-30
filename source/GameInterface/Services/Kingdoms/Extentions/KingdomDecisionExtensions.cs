using GameInterface.Services.Kingdoms.Data;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms.Extentions
{
    public static class KingdomDecisionExtensions
    {
        private static readonly Dictionary<Type, Func<KingdomDecision, KingdomDecisionData>> SupportedConversions = new Dictionary<Type, Func<KingdomDecision, KingdomDecisionData>>()
        {
            { typeof(DeclareWarDecision), ConvertDeclareWarDecision },
        };
        public static KingdomDecisionData ToKingdomDecisionData(this KingdomDecision kingdomDecision)
        {
            if (SupportedConversions.ContainsKey(kingdomDecision.GetType()))
            {
                return SupportedConversions[kingdomDecision.GetType()](kingdomDecision);
            }
            else
            {
                throw new InvalidOperationException($"Type of kingdom decision: {kingdomDecision.GetType().Name} is not supported.");
            }
        }

        private static KingdomDecisionData ConvertDeclareWarDecision(KingdomDecision decision)
        {
            DeclareWarDecision declareWarDecision = decision as DeclareWarDecision;
            if (declareWarDecision != null)
            {
                return new DeclareWarDecisionData(declareWarDecision.ProposerClan.StringId,
                    declareWarDecision.TriggerTime.GetDayOfSeason, declareWarDecision.IsEnforced, declareWarDecision.NotifyPlayer, declareWarDecision.PlayerExamined, null);
            }
            else
            {
                throw new ArgumentException($"Argument is not a type of DeclareWarDecision.");
            }
        }
    }
}
