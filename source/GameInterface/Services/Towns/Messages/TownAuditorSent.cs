using Common.Messaging;
using GameInterface.Services.Towns.Data;
using System.Collections.Generic;

namespace GameInterface.Services.Towns.Messages
{
    public record class TownAuditorSent : ICommand
    {

        public TownAuditorData[] Datas { get; }

        public TownAuditorSent(List<TownAuditorData> townAuditorDatas)
        {
            Datas = townAuditorDatas.ToArray();
        }
    }
}
