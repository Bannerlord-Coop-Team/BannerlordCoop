using System;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class PathFaceRecordSerializer : ICustomSerializer
    {
        private int pathFaceIndex;

        public PathFaceRecordSerializer(PathFaceRecord pathFaceRecord)
        {
            pathFaceIndex = pathFaceRecord.FaceIndex;
        }

        public object Deserialize()
        {
            return new PathFaceRecord(pathFaceIndex);
        }
    }
}