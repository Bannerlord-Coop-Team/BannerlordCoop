using System;

namespace GameInterface.Messages.Queries.Processors
{
    internal class GetPlatformByIdProcessor : QueryProcessor<GetPlatformById, string>
    {
        //TODO: https://github.com/Bannerlord-Coop-Team/BannerlordCoop/blob/Baseline/source/Coop/NetImpl/PlatformAPI.cs
        public override string Process(GetPlatformById query)
        {
            throw new NotImplementedException();
        }
    }
}
