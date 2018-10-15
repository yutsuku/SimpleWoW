using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Client.Chat;
using Client.World;

namespace Client
{
    /// <summary>
    /// Stores world variables
    /// </summary>
    public class GameWorld
    {
        //! Player name lookup per GUID - trough CMSG/SMSG_NAME_QUERY(_response)
        public Dictionary<ulong, string> PlayerNameLookup = new Dictionary<ulong, string>();

        public Dictionary<uint, string> GuildMember = new Dictionary<uint, string>();
        public Dictionary<uint, string> GuildMemberRank = new Dictionary<uint, string>();
        public Dictionary<uint, uint> GuildMemberRankValue = new Dictionary<uint, uint>();

        //! Message queue for when sender's name hasn't been queried trough NAME_QUERY yet
        public Dictionary<ulong, Queue<ChatMessage>> QueuedChatMessages = new Dictionary<ulong, Queue<ChatMessage>>();

        //! Character currently logged into world
        public Character SelectedCharacter;

        public Guild Guild = new Guild();

        //! Persons who last whispered the client
        public Queue<string> LastWhisperers = new Queue<string>();
    }
}
