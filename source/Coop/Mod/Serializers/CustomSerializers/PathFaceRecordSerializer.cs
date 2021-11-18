using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class PathFaceRecordSerializer : ICustomSerializer
    {
        private int pathFaceIndex;
        private int pathFaceGroupIndex;
        private int pathFaceIslandIndex;

        public PathFaceRecordSerializer(PathFaceRecord pathFaceRecord)
        {
            pathFaceIndex = pathFaceRecord.FaceIndex;
            pathFaceGroupIndex = pathFaceRecord.FaceGroupIndex;
            pathFaceIslandIndex = pathFaceRecord.FaceIslandIndex;
        }

        public object Deserialize()
        {
            return new PathFaceRecord(pathFaceIndex, pathFaceGroupIndex, pathFaceIslandIndex);
        }

        public void ResolveReferenceGuids()
        {
            throw new NotImplementedException();
        }
    }
}