using System.Numerics;
using System.Threading;
using Client.Authentication;
using Client.Authentication.Network;
using Client.UI;
using Client.World.Network;
using Client.Chat;
using System.Collections.Generic;
using Client.World.Entities;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace Client
{
    public interface IGame
    {
        BigInteger Key { get; }
        string Username { get; }

        IGameUI UI { get; }

        GameWorld World { get; }

        void ConnectTo(WorldServerInfo server);

        void Start();

        void Exit();

        void SendPacket(OutPacket packet);
    }

    public class Game<T> : IGame
        where T : IGameUI, new()
    {
        bool Running;

        GameSocket socket;

        public BigInteger Key { get; private set; }
        public string Username { get; private set; }

        public IGameUI UI { get; protected set; }

        public DateTime LastUpdate
        {
            get;
            private set;
        }
        TaskCompletionSource<bool> loggedOutEvent = new TaskCompletionSource<bool>();
        public int ScheduledActionsCount => scheduledActions.Count;
        ScheduledActions scheduledActions;
        ActionFlag disabledActions;
        int scheduledActionCounter;

        public GameWorld World
        {
            get { return _world; }
            private set { _world = value; }
        }

        private GameWorld _world;

        public Game(string hostname, int port, string username, string password, LogLevel logLevel)
        {
            UI = new T();
            UI.Game = this;
            UI.LogLevel = logLevel;

            World = new GameWorld();

            this.Username = username;

            scheduledActions = new ScheduledActions();
            Triggers = new IteratedList<Trigger>();

            socket = new AuthSocket(this, hostname, port, username, password);
            socket.InitHandlers();
        }

        public void ConnectTo(WorldServerInfo server)
        {
            if (socket is AuthSocket)
                Key = ((AuthSocket)socket).Key;

            socket.Dispose();

            socket = new WorldSocket(this, server);
            socket.InitHandlers();

            if (socket.Connect())
                socket.Start();
            else
                Exit();
        }

        public void Start()
        {
            /*// the initial socket is an AuthSocket - it will initiate its own asynch read
            Running = socket.Connect();

            Task.Run(async () =>
            {
                while (Running)
                {
                    // main loop here
                    Update();
                    await Task.Delay(100);
                }
            });*/
            Running = socket.Connect();

            ThreadStart work = RunCommands;
            Thread thread = new Thread(work);
            thread.Start();

            while (Running)
            {
                // main loop here
                UI.Update();
                Thread.Sleep(100);
            }

            socket.KeepAliveTimer.Dispose();

            thread.Join();
            UI.Exit();
        }

        public void RunCommands()
        {
            while (Running)
            {
                // main loop here
                UI.UpdateCommands();
                Thread.Sleep(100);
            }
        }

        public void Update()
        {
            LastUpdate = DateTime.Now;

            (socket as WorldSocket)?.HandlePackets();

            if (World.SelectedCharacter == null)
                return;

            while (scheduledActions.Count != 0)
            {
                var scheduledAction = scheduledActions.First();
                if (scheduledAction.ScheduledTime <= DateTime.Now)
                {
                    scheduledActions.RemoveAt(0, false);
                    if (scheduledAction.Interval > TimeSpan.Zero)
                        ScheduleAction(scheduledAction.Action, DateTime.Now + scheduledAction.Interval, scheduledAction.Interval, scheduledAction.Flags, scheduledAction.Cancel);
                    try
                    {
                        scheduledAction.Action();
                    }
                    catch (Exception ex)
                    {
                        
                    }
                }
                else
                    break;
            }
            UI.Update();
        }

        public void Exit()
        {
            Running = false;
        }

        public void SendPacket(OutPacket packet)
        {
            if (socket is WorldSocket)
                ((WorldSocket)socket).Send(packet);
        }

        public int ScheduleAction(Action action, TimeSpan interval = default(TimeSpan), ActionFlag flags = ActionFlag.None, Action cancel = null)
        {
            return ScheduleAction(action, DateTime.Now, interval, flags, cancel);
        }

        public int ScheduleAction(Action action, DateTime time, TimeSpan interval = default(TimeSpan), ActionFlag flags = ActionFlag.None, Action cancel = null)
        {
            if (Running && (flags == ActionFlag.None || !disabledActions.HasFlag(flags)))
            {
                scheduledActionCounter++;
                scheduledActions.Add(new RepeatingAction(action, cancel, time, interval, flags, scheduledActionCounter));
                return scheduledActionCounter;
            }
            else
                return 0;
        }

        public void CancelActionsByFlag(ActionFlag flag, bool cancel = true)
        {
            scheduledActions.RemoveByFlag(flag, cancel);
        }

        public bool CancelAction(int actionId)
        {
            return scheduledActions.Remove(actionId);
        }

        public void DisableActionsByFlag(ActionFlag flag)
        {
            disabledActions |= flag;
            CancelActionsByFlag(flag);
        }

        public void EnableActionsByFlag(ActionFlag flag)
        {
            disabledActions &= ~flag;
        }

        #region Triggers Handling
        IteratedList<Trigger> Triggers;
        int triggerCounter;

        public int AddTrigger(Trigger trigger)
        {
            triggerCounter++;
            trigger.Id = triggerCounter;
            Triggers.Add(trigger);
            return triggerCounter;
        }

        public IEnumerable<int> AddTriggers(IEnumerable<Trigger> triggers)
        {
            var triggerIds = new List<int>();
            foreach (var trigger in triggers)
                triggerIds.Add(AddTrigger(trigger));
            return triggerIds;
        }

        public bool RemoveTrigger(int triggerId)
        {
            return Triggers.RemoveAll(trigger => trigger.Id == triggerId) > 0;
        }

        public void ClearTriggers()
        {
            Triggers.Clear();
        }

        public void ResetTriggers()
        {
            Triggers.ForEach(trigger => trigger.Reset());
        }

        public void HandleTriggerInput(TriggerActionType type, params object[] inputs)
        {
            Triggers.ForEach(trigger => trigger.HandleInput(type, inputs));
        }

        void OnFieldUpdate(object s, UpdateFieldEventArg e)
        {
            HandleTriggerInput(TriggerActionType.UpdateField, e);
        }
        #endregion
    }
}