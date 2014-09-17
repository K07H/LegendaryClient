using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryClient.Logic.Region
{
    public sealed class PBE : BaseRegion
    {
        public override string RegionName
        {
            get { return "PBE"; }
        }

        public override string InternalName
        {
            get { return "PBE1"; }
        }

        public override string ChatName
        {
            get { return "pbe1"; }
        }

        public override Uri NewsAddress
        {
            get { return new Uri("http://ll.leagueoflegends.com/landingpage/data/pbe/en_US.js"); }
        }

        public override string Server
        {
            get { return "prod.pbe1.lol.riotgames.com"; }
        }

        public override string LoginQueue
        {
            get { return "https://lq.pbe1.lol.riotgames.com/"; }
        }

        public override string Locale
        {
            get { return "en_GB"; }
        }

        public override IPAddress[] PingAddresses
        {
            get
            {
                return new IPAddress[]
                {
                    // No known IP address
                };
            }
        }

        public override Uri SpectatorLink
        {
            get { return new Uri("http://spectator.pbe1.lol.riotgames.com:8088/observer-mode/rest/"); }
        }

        public override string SpectatorIpAddress
        {
            get { return "69.88.138.29"; }
        }
    }
}
