using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Client.Crypto;
using Client.UI;
using Client.World.Definitions;

namespace Client.World.Network
{
    public partial class WorldSocket
    {

        [PacketHandler(WorldCommand.SMSG_GUILD_QUERY_RESPONSE)]
        void HandeGuildQueryResponse(InPacket packet)
        {
            uint member_id = packet.ReadUInt32();
            string name = packet.ReadCString();
            string rankName = packet.ReadCString();

            if (!Game.World.GuildMember.ContainsKey(member_id))
            {
                Game.World.GuildMember.Add(member_id, name);
                Game.World.GuildMemberRank.Add(member_id, rankName);
                Game.UI.LogLine(String.Format(">HandeGuildQueryResponse: Added info about {0} ({1})", name, rankName), LogLevel.Info);
            }
        }

        [PacketHandler(WorldCommand.SMSG_GUILD_ROSTER)]
        void HandleGuildRoster(InPacket packet)
        {
            uint members = packet.ReadUInt32();
            string motd = packet.ReadCString();
            string info = packet.ReadCString();
            uint ranks = packet.ReadUInt32();

            
            Game.UI.LogLine(String.Format("motd: {0}", motd), LogLevel.Info);
            Game.UI.LogLine(String.Format("info: {0}", info), LogLevel.Info);

            for (uint i = 0; i < ranks; ++i)
            {
                Game.World.Guild.rankInfo.SetInfo(packet);
                Game.World.Guild.ranks.Add(Game.World.Guild.rankInfo);
            }
            
            Game.UI.LogLine(String.Format("Total members: {0}", members), LogLevel.Info); 

            for (uint i = 0; i < members; ++i)
            {
                GuildMember member = new GuildMember(packet);
                if (member.isOnline)
                    Game.World.Guild.online++;

                Game.World.Guild.GuildMembers.Add(member);
            }

            Game.UI.LogLine(String.Format("Online members: {0}", Game.World.Guild.online), LogLevel.Info);

            foreach (GuildMember member in Game.World.Guild.GuildMembers)
            {
                if (member.isOnline)
                    Game.UI.LogLine(String.Format(member.m_publicNote.Length > 0 ? "{0}, L{1} {2} - {3}" : "{0}, L{1} {2}", member.m_name, member.m_level, (Class)member.m_class, member.m_publicNote), LogLevel.Info);
            }

        }
    }
}
