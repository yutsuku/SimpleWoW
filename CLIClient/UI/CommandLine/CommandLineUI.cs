using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Client;
using Client.Authentication;
using Client.Chat;
using Client.Chat.Definitions;
using Client.World;
using Client.World.Definitions;
using Client.World.Network;

namespace Client.UI.CommandLine
{
    public partial class CommandLineUI : IGameUI
    {
        #region Private Members
        private LogLevel _logLevel;
        private StreamWriter _logFile;
        private string _chatHeader; // keeps info on the last written chat (so that we don't have to write /say twice, it 'sticks')
        private string _chatTarget; // used for /whisper <target>, keeps the <target>

        #endregion

        public CommandLineUI()
        {
            _logFile = new StreamWriter(String.Format("{0}.log", DateTime.Now).Replace(':', '_').Replace('/', '-'));
            _logFile.AutoFlush = true;

            InitializeKeybinds();
        }

        #region IGameUI Members

        public LogLevel LogLevel
        {
            get { return _logLevel; }
            set { _logLevel = value; }
        }

        public IGame Game { get; set; }

        public void Update()
        {
            if (Game.World.SelectedCharacter == null)
                return;

            ConsoleKeyInfo keyPress = Console.ReadKey();
            KeyBind handler;
            if (_keyPressHandlers.TryGetValue(keyPress.Key, out handler))
                handler();
        }

        public void UpdateCommands()
        {
            if (Game.World.SelectedCharacter == null)
                return;

            string s = Console.ReadLine();
            if (String.IsNullOrWhiteSpace(s))
                return;

            string message = s;
            if (s.StartsWith("/")) // client command
            {
                int idx = s.IndexOf(" "); // first space, get the end of the '/' command
                if (idx != -1)
                {
                    _chatHeader = s.Substring(0, idx);
                    message = s.Substring(idx + 1); // after the space
                }
            }

            if (String.IsNullOrWhiteSpace(_chatHeader))
                return;

            if (_chatHeader.StartsWith("/s")) // "/say"
            {
                var response = new OutPacket(WorldCommand.CMSG_MESSAGECHAT);

                response.Write((uint)ChatMessageType.Say);
                var race = Game.World.SelectedCharacter.Race;
                var language = race.IsHorde() ? Language.Orcish : Language.Common;
                response.Write((uint)language);
                response.Write(message.ToCString());
                Game.SendPacket(response);
            }
            else if (_chatHeader.StartsWith("/y")) // "/yell"
            {
                var response = new OutPacket(WorldCommand.CMSG_MESSAGECHAT);

                response.Write((uint)ChatMessageType.Yell);
                var race = Game.World.SelectedCharacter.Race;
                var language = race.IsHorde() ? Language.Orcish : Language.Common;
                response.Write((uint)language);
                response.Write(message.ToCString());
                Game.SendPacket(response);
            }
            else if (_chatHeader.StartsWith("/g")) // "/guild"
            {
                var response = new OutPacket(WorldCommand.CMSG_MESSAGECHAT);

                response.Write((uint)ChatMessageType.Guild);
                var race = Game.World.SelectedCharacter.Race;
                var language = race.IsHorde() ? Language.Orcish : Language.Common;
                response.Write((uint)language);
                response.Write(message.ToCString());
                Game.SendPacket(response);
            }
            else if (_chatHeader.StartsWith("/w")) // "/whisper <target>"
            {
                var response = new OutPacket(WorldCommand.CMSG_MESSAGECHAT);

                if (s.StartsWith("/w")) // if it's the /w command being used, get the target, it must be in the string
                {
                    int idx = message.IndexOf(" ");
                    if (idx != -1)
                    {
                        _chatTarget = message.Substring(0, idx);
                        message = message.Substring(idx);
                    }
                }
                // else, we're using last _chatHeader, and thus last _chatTarget

                response.Write((uint)ChatMessageType.Whisper);
                var race = Game.World.SelectedCharacter.Race;
                var language = race.IsHorde() ? Language.Orcish : Language.Common;
                response.Write((uint)language);
                response.Write(_chatTarget.ToCString());
                response.Write(message.ToCString());
                Game.SendPacket(response);
            }
            else if (_chatHeader.StartsWith("/join")) // "/join <channel>"
            {
                var response = new OutPacket(WorldCommand.CMSG_JOIN_CHANNEL);

                uint channelId = 0;
                // byte is uint8
                byte unk1 = 0;
                byte unk2 = 0;
                int idx = message.IndexOf(" ");
                if (idx == -1)
                    return;

                string channel = message.Substring(0, idx);
                string password = "";

                response.Write((uint)channelId);
                response.Write((byte)unk1);
                response.Write((byte)unk2);
                response.Write(channel.ToCString());
                response.Write(password.ToCString());
                Game.SendPacket(response);
            }
            else if (_chatHeader.StartsWith("/leave")) // "/leave <channel>"
            {
                uint unk = 0;
                int idx = message.IndexOf(" ");
                if (idx == -1)
                    return;

                string channel = message.Substring(0, idx);

                var response = new OutPacket(WorldCommand.CMSG_LEAVE_CHANNEL);
                response.Write((uint)unk);
                response.Write(channel.ToCString());
                Game.SendPacket(response);
            }
        }

        public void Exit()
        {
            Console.Write("Press any key to continue...");
            Console.ReadKey(true);

            _logFile.Close();
        }

        public void PresentRealmList(WorldServerList worldServerList)
        {
            WorldServerInfo selectedServer = null;

            if (worldServerList.Count == 1)
                selectedServer = worldServerList[0];
            else
            {
                LogLine("\n\tName\tType\tPopulation");

                int index = 0;
                foreach (WorldServerInfo server in worldServerList)
                    LogLine
                    (
                        string.Format("{0}\t{1}\t{2}\t{3}",
                        index++,
                        server.Name,
                        server.Type,
                        server.Population
                        )
                    );

                // select a realm - default to the first realm if there is only one
                index = worldServerList.Count == 1 ? 0 : -1;
                while (index > worldServerList.Count || index < 0)
                {
                    Log("Choose a realm:  ");
                    if (!int.TryParse(Console.ReadLine(), out index))
                        LogLine();
                }
                selectedServer = worldServerList[index];
            }

            Game.ConnectTo(selectedServer);
        }

        public void PresentCharacterList(Character[] characterList)
        {
            LogLine("\n\tName\tLevel Class Race");

            int index = 0;
            foreach (Character character in characterList)
                LogLine
                (
                    string.Format("{4}\t{0}\t{1} {2} {3}",
                    character.Name,
                    character.Level,
                    character.Race,
                    character.Class,
                    index++)
                );

            if (characterList.Length < 10)
                LogLine(string.Format("{0}\tCreate a new character. (NOT YET IMPLEMENTED)", index));

            int length = characterList.Length == 10 ? 10 : (characterList.Length + 1);
            index = -1;
            while (index > length || index < 0)
            {
                Log("Choose a character:  ");
                if (!int.TryParse(Console.ReadLine(), out index))
                    LogLine();
            }

            if (index < characterList.Length)
            {
                Game.World.SelectedCharacter = characterList[index];
                // TODO: enter world

                LogLine(string.Format("Entering pseudo-world with character {0}", Game.World.SelectedCharacter.Name));

                OutPacket packet = new OutPacket(WorldCommand.CMSG_PLAYER_LOGIN);
                packet.Write(Game.World.SelectedCharacter.GUID);
                Game.SendPacket(packet);
            }
            else
            {
                // TODO: character creation
            }
        }

        public void PresentChatMessage(ChatMessage message)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(message.Sender.Type == ChatMessageType.WhisperInform ? "To: " : message.Sender.Type.ToString());
            //! Color codes taken from default chat_cache in WTF folder
            //! TODO: RTF form?
            switch (message.Sender.Type)
            {
                case ChatMessageType.Channel:
                    {
                        //sb.ForeColor(Color.FromArgb(255, 192, 192));
                        Console.ForegroundColor = ConsoleColor.DarkYellow;//Color.DarkSalmon;
                        sb.Append(" [");
                        sb.Append(message.Sender.ChannelName);
                        sb.Append("] ");
                        break;
                    }
                case ChatMessageType.Whisper:
                    if (message.Language != Language.Addon)
                        Console.Beep(1400, 400);

                    Game.World.LastWhisperers.Enqueue(message.Sender.Sender);
                        goto case ChatMessageType.WhisperInform;
                case ChatMessageType.WhisperInform:
                    Console.ForegroundColor = ConsoleColor.Magenta;//Color.DeepPink;
                    break;
                case ChatMessageType.WhisperForeign:
                    Console.ForegroundColor = ConsoleColor.Magenta;//Color.DeepPink;
                    break;
                case ChatMessageType.System:
                case ChatMessageType.Money:
                case ChatMessageType.TargetIcons:
                case ChatMessageType.Achievement:
                        //sb.ForeColor(Color.FromArgb(255, 255, 0));
                    Console.ForegroundColor = ConsoleColor.Yellow;//Color.Gold;
                    break;
                case ChatMessageType.Emote:
                case ChatMessageType.TextEmote:
                case ChatMessageType.MonsterEmote:
                        //sb.ForeColor(Color.FromArgb(255, 128, 64));
                    Console.ForegroundColor = ConsoleColor.DarkRed;//Color.Chocolate;
                    break;
                case ChatMessageType.Party:
                        //sb.ForeColor(Color.FromArgb(170, 170, 255));
                    Console.ForegroundColor = ConsoleColor.DarkCyan;//Color.CornflowerBlue;
                    break;
                case ChatMessageType.PartyLeader:
                        //sb.ForeColor(Color.FromArgb(118, 200, 255));
                    Console.ForegroundColor = ConsoleColor.Cyan;//Color.DodgerBlue;
                    break;
                case ChatMessageType.Raid:
                        //sb.ForeColor(Color.FromArgb(255, 172, 0));
                    Console.ForegroundColor = ConsoleColor.Red;//Color.DarkOrange;
                    break;
                case ChatMessageType.RaidLeader:
                        //sb.ForeColor(Color.FromArgb(255, 72, 9));
                    Console.ForegroundColor = ConsoleColor.Red;//Color.DarkOrange;
                    break;
                case ChatMessageType.RaidWarning:
                        //sb.ForeColor(Color.FromArgb(255, 72, 0));
                    Console.ForegroundColor = ConsoleColor.Red;//Color.DarkOrange;
                    break;
                case ChatMessageType.Guild:
                case ChatMessageType.GuildAchievement:
                        //sb.ForeColor(Color.FromArgb(64, 255, 64));
                    Console.ForegroundColor = ConsoleColor.Green;//Color.LimeGreen;
                    break;
                case ChatMessageType.Officer:
                        //sb.ForeColor(Color.FromArgb(64, 192, 64));
                    Console.ForegroundColor = ConsoleColor.DarkGreen;//Color.DarkSeaGreen;
                    break;
                case ChatMessageType.Yell:
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    break;
                case ChatMessageType.Say:
                case ChatMessageType.MonsterSay:
                default:
                    //sb.ForeColor(Color.FromArgb(255, 255, 255));
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
            }

            // ignore repeated system messages
            if (message.Sender.Type == ChatMessageType.System && String.IsNullOrEmpty(message.Sender.Sender))
                return;

            sb.Append("[");
            if (message.ChatTag.HasFlag(ChatTag.Gm))
                sb.Append("<GM>");
            if (message.ChatTag.HasFlag(ChatTag.Afk))
                sb.Append("<AFK>");
            if (message.ChatTag.HasFlag(ChatTag.Dnd))
                sb.Append("<DND>");
            sb.Append(message.Sender.Sender);
            sb.Append("]: ");
            sb.Append(message.Message);

            LogLine(sb.ToString(), message.Language == Language.Addon ? LogLevel.Debug : LogLevel.Info);
        }

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            lock (Console.Out)
            {
                if (level >= LogLevel)
                {
                    Console.Write(message);
                    _logFile.Write(String.Format("{0} : {1}", DateTime.Now, message));
                    Console.ResetColor();
                }
            }
        }

        public void LogLine(LogLevel level = LogLevel.Info)
        {
            lock (Console.Out)
            {
                if (level >= LogLevel)
                {
                    Console.WriteLine();
                    _logFile.WriteLine();
                    Console.ResetColor();
                }
            }
        }

        public void LogLine(string message, LogLevel level = LogLevel.Info)
        {
            lock (Console.Out)
            {
                if (level >= LogLevel)
                {
                    Console.WriteLine(message);
                    _logFile.WriteLine(String.Format("{0} : {1}", DateTime.Now, message));
                    Console.ResetColor();
                }
            }
        }

        public void LogException(Exception exception)
        {
            LogException(exception.Message);
        }

        public void LogException(string message)
        {
            _logFile.WriteLine(String.Format("{0} : Exception: {1}", DateTime.Now, message));
            _logFile.WriteLine((new StackTrace(1, true)).ToString());
            throw new Exception(message);
        }

        public string ReadLine()
        {
            //! We don't want to clutter the console, so wait for input before printing output
            string ret;
            lock (Console.Out)
                ret = Console.ReadLine();

            return ret;
        }

        public int Read()
        {
            //! We don't want to clutter the console, so wait for input before printing output
            int ret;
            lock (Console.Out)
                ret = Console.Read();

            return ret;
        }

        public ConsoleKeyInfo ReadKey()
        {
            //! We don't want to clutter the console, so wait for input before printing output
            ConsoleKeyInfo ret;
            lock (Console.Out)
                ret = Console.ReadKey();

            return ret;
        }

        #endregion
    }
}
