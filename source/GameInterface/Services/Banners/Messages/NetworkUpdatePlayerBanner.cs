using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Banners.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkUpdatePlayerBanner : ICommand
    {
        [ProtoMember(1)]
        public string BannerCode { get; }

        [ProtoMember(2)]
        public string ClanId { get; }

        public NetworkUpdatePlayerBanner(string bannerCode, string clanId)
        {
            BannerCode = bannerCode;
            ClanId = clanId;
        }
    }
}
