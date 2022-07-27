using System;
using TaleWorlds.Library;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class PathFaceRecordSerializer : ICustomSerializer
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

        public T Deserialize<T>()
        {
            return (T)Deserialize();
        }

        public void ResolveReferenceGuids()
        {
            // No references
        }
    }
}