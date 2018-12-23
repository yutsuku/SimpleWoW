using Client.World.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Client.UI;
using Client.World.Definitions;

namespace Client.World
{
    public enum GuildMisc : ulong
    {
        GUILD_BANK_MAX_TABS = 6,
        GUILD_BANK_MAX_SLOTS = 98,
        GUILD_BANK_MONEY_LOGS_TAB = 100,
        GUILD_RANKS_MIN_COUNT = 5,
        GUILD_RANKS_MAX_COUNT = 10,
        GUILD_RANK_NONE = 0xFF,
        GUILD_WITHDRAW_MONEY_UNLIMITED = 0xFFFFFFFF,
        GUILD_WITHDRAW_SLOT_UNLIMITED = 0xFFFFFFFF,
        GUILD_EVENT_LOG_GUID_UNDEFINED = 0xFFFFFFFF,
        TAB_UNDEFINED = 0xFF,
    }

    [Flags]
    public enum GuildMemberFlags
    {
        GUILDMEMBER_STATUS_NONE = 0x0000,
        GUILDMEMBER_STATUS_ONLINE = 0x0001,
        GUILDMEMBER_STATUS_AFK = 0x0002,
        GUILDMEMBER_STATUS_DND = 0x0004,
        GUILDMEMBER_STATUS_MOBILE = 0x0008, // remote chat from mobile app
    };

    public class Guild
    {
        public List<RankInfo> ranks = new List<RankInfo>();
        public RankInfo rankInfo = new RankInfo();
        public List<GuildMember> GuildMembers = new List<GuildMember>();
        public uint online = 0;
    }

    public class GuildMember
    {
        public ulong m_guid;
        public byte m_flags;
        public bool isOnline;
        public float lastOnline;
        public string m_name;
        public uint m_rankId;
        public byte m_level;
        public byte m_class;
        public byte m_gender;
        public uint m_zoneId;
        public string m_publicNote;
        public string m_officerNote;

        public GuildMember(InPacket packet)
        {
            m_guid = packet.ReadUInt64();
            m_flags = packet.ReadByte();
            m_name = packet.ReadCString();
            m_rankId = packet.ReadUInt32();
            m_level = packet.ReadByte();
            m_class = packet.ReadByte();
            m_gender = packet.ReadByte();
            m_zoneId = packet.ReadUInt32();

            isOnline = ((GuildMemberFlags)m_flags & GuildMemberFlags.GUILDMEMBER_STATUS_ONLINE) != 0;

            if (!isOnline)
            {
                lastOnline = packet.ReadSingle();
            }

            m_publicNote = packet.ReadCString();
            m_officerNote = packet.ReadCString();
        }
}

    public class GuildBankRightsAndSlots
    {
        public byte tabId;
        public uint rights;
        public uint slots;
    }

    public class RankInfo
    {
        //public ulong m_guildId;
        public byte m_rankId;
        public string m_name;
        public uint m_rights;
        public uint m_bankMoneyPerDay;
        public GuildBankRightsAndSlots m_bankTabRightsAndSlots = new GuildBankRightsAndSlots();

        public void SetInfo(InPacket packet)
        {
            m_rights = packet.ReadUInt32();
            m_bankMoneyPerDay = packet.ReadUInt32();

            for (int i = 0; i < (int)GuildMisc.GUILD_BANK_MAX_TABS; ++i)
            {
                m_bankTabRightsAndSlots.tabId = (byte)i;
                m_bankTabRightsAndSlots.rights = packet.ReadUInt32();
                m_bankTabRightsAndSlots.slots = packet.ReadUInt32();
            }
        }
    }
}
