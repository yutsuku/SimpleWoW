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
            // m_emblemInfo
            // rankSize

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
            //WorldPacket data(SMSG_GUILD_ROSTER, (4 + m_motd.length() + 1 + m_info.length() + 1 + 4 + _GetRanksSize() * (4 + 4 + GUILD_BANK_MAX_TABS * (4 + 4)) + m_members.size() * 50));
            //data << uint32(m_members.size());
            //data << m_motd;
            //data << m_info;

            //data << uint32(_GetRanksSize());
            //for (auto ritr = m_ranks.begin(); ritr != m_ranks.end(); ++ritr)
            //    ritr->WritePacket(data);

            //for (auto itr = m_members.begin(); itr != m_members.end(); ++itr)
            //    itr->second->WritePacket(data, _HasRankRight(session->GetPlayer(), GR_RIGHT_VIEWOFFNOTE));
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
                //Game.UI.LogLine(String.Format("rank {0}, id: {1}", Game.World.Guild.rankInfo.m_name, Game.World.Guild.rankInfo.m_rankId), LogLevel.Info);
            }


            //            void Guild::Member::WritePacket(WorldPacket & data, bool sendOfficerNote) const
            //{
            //                data << uint64(m_guid)
            //                     << uint8(m_flags)
            //                     << m_name
            //                     << uint32(m_rankId)
            //                     << uint8(m_level)
            //                     << uint8(m_class)
            //                     << uint8(m_gender)
            //                     << uint32(m_zoneId);

            //                if (!m_flags)
            //                    data << float(float(GameTime::GetGameTime() - m_logoutTime) / DAY);

            //                data << m_publicNote;

            //                if (sendOfficerNote)
            //                    data << m_officerNote;
            //                else
            //                    data << "";
            //            }

            
            Game.UI.LogLine(String.Format("ranks count: {0}", ranks), LogLevel.Info);
            Game.UI.LogLine(String.Format("Total members: {0}", members), LogLevel.Info);
            Game.UI.LogLine("Online members:", LogLevel.Info);

            for (uint i = 0; i < members; ++i)
            {
                ulong m_guid = packet.ReadUInt64();
                byte m_flags = packet.ReadByte(); // online?
                string m_name = packet.ReadCString();
                uint m_rankId = packet.ReadUInt32();
                byte m_level = packet.ReadByte();
                byte m_class = packet.ReadByte();
                byte m_gender = packet.ReadByte();
                uint m_zoneId = packet.ReadUInt32();
                string m_publicNote = packet.ReadCString();
                string m_officerNote = packet.ReadCString();
                bool isOnline = ((GuildMemberFlags)m_flags & GuildMemberFlags.GUILDMEMBER_STATUS_ONLINE) != 0;
                if (isOnline)
                {
                    Game.UI.LogLine(String.Format(m_publicNote.Length > 0 ? "{0}, L{1} {2} - {3}" : "{0}, L{1} {2}", m_name, m_level, (Class)m_class, m_publicNote), LogLevel.Info);
                }
            }

        }
    }
}
