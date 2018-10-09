using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using Client.Authentication;
using Client.UI;

namespace Client.World.Network
{
    public partial class WorldSocket : GameSocket
    {
        WorldServerInfo ServerInfo;

        private long transferred;
        public long Transferred { get { return transferred; } }

        private long sent;
        public long Sent { get { return sent; } }

        private long received;
        public long Received { get { return received; } }

        BatchQueue<InPacket> packetsQueue = new BatchQueue<InPacket>();

        public WorldSocket(IGame program, WorldServerInfo serverInfo)
        {
            Game = program;
            ServerInfo = serverInfo;
        }

        #region Handler registration

        Dictionary<WorldCommand, PacketHandler> PacketHandlers;

        public override void InitHandlers()
        {
            PacketHandlers = new Dictionary<WorldCommand, PacketHandler>();

            RegisterHandlersFrom(this);
            RegisterHandlersFrom(Game);
        }

        void RegisterHandlersFrom(object obj)
        {
            // create binding flags to discover all non-static methods
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            IEnumerable<PacketHandlerAttribute> attributes;
            foreach (var method in obj.GetType().GetMethods(flags))
            {
                if (!method.TryGetAttributes(false, out attributes))
                    continue;

                PacketHandler handler = (PacketHandler)PacketHandler.CreateDelegate(typeof(PacketHandler), obj, method);

                foreach (var attribute in attributes)
                {
                    Game.UI.LogLine(string.Format("Registered '{0}.{1}' to '{2}'", obj.GetType().Name, method.Name, attribute.Command), LogLevel.Debug);
                    PacketHandlers[attribute.Command] = handler;
                }
            }
        }

        #endregion

        #region Asynchronous Reading

        int Index;
        int Remaining;

        private void ReadAsync(EventHandler<SocketAsyncEventArgs> callback, object state = null)
        {
            if (Disposing)
                return;

            SocketAsyncState = state;
            SocketArgs.SetBuffer(ReceiveData, Index, Remaining);
            SocketCallback = callback;
            connection.Client.ReceiveAsync(SocketArgs);
        }

        /*private void BeginRead(AsyncCallback callback, object state = null)
        {
            this.connection.Client.BeginReceive
            (
                ReceiveData, Index, Remaining,
                SocketFlags.None,
                callback,
                state
            );
        }*/

        /// <summary>
        /// Determines how large the incoming header will be by
        /// inspecting the first byte, then initiates reading the header.
        /// </summary>
        private void ReadSizeCallback(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                int bytesRead = e.BytesTransferred;
                if (bytesRead == 0)
                {
                    // TODO: world server disconnect
                    Game.UI.LogLine("Server has closed the connection");
                    //Game.Reconnect();
                    return;
                }

                Interlocked.Increment(ref transferred);
                Interlocked.Increment(ref received);

                authenticationCrypto.Decrypt(ReceiveData, 0, 1);
                if ((ReceiveData[0] & 0x80) != 0)
                {
                    // need to resize the buffer
                    byte temp = ReceiveData[0];
                    ReserveData(5);
                    ReceiveData[0] = (byte)((0x7f & temp));

                    Remaining = 4;
                }
                else
                    Remaining = 3;

                Index = 1;
                ReadAsync(ReadHeaderCallback);
            }
            // these exceptions can happen as race condition on shutdown
            catch (ObjectDisposedException ex)
            {
                Game.UI.LogException(ex.Message);
            }
            catch (NullReferenceException ex)
            {
                Game.UI.LogException(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                Game.UI.LogException(ex.Message);
            }
            catch (SocketException ex)
            {
                Game.UI.LogException(ex.Message);
                //Game.Reconnect();
            }
        }

        /// <summary>
        /// Reads the rest of the incoming header.
        /// </summary>
        private void ReadHeaderCallback(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                int bytesRead = e.BytesTransferred;
                if (bytesRead == 0)
                {
                    // TODO: world server disconnect
                    Game.UI.LogLine("Server has closed the connection");
                    //Game.Reconnect();
                    return;
                }

                Interlocked.Add(ref transferred, bytesRead);
                Interlocked.Add(ref received, bytesRead);

                if (bytesRead == Remaining)
                {
                    // finished reading header
                    // the first byte was decrypted already, so skip it
                    authenticationCrypto.Decrypt(ReceiveData, 1, ReceiveDataLength - 1);
                    ServerHeader header = new ServerHeader(ReceiveData, ReceiveDataLength);

                    Game.UI.LogLine(header.ToString(), LogLevel.Debug);
                    if (header.InputDataLength > 5 || header.InputDataLength < 4)
                        Game.UI.LogException(String.Format("Header.InputDataLength invalid: {0}", header.InputDataLength));

                    if (header.Size > 0)
                    {
                        // read the packet payload
                        Index = 0;
                        Remaining = header.Size;
                        ReserveData(header.Size);
                        ReadAsync(ReadPayloadCallback, header);
                    }
                    else
                    {
                        // the packet is just a header, start next packet
                        QueuePacket(new InPacket(header));
                        Start();
                    }
                }
                else
                {
                    // more header to read
                    Index += bytesRead;
                    Remaining -= bytesRead;
                    ReadAsync(ReadHeaderCallback);
                }
            }
            // these exceptions can happen as race condition on shutdown
            catch (ObjectDisposedException ex)
            {
                Game.UI.LogException(ex.Message);
            }
            catch (NullReferenceException ex)
            {
                Game.UI.LogException(ex.Message);
            }
            catch (SocketException ex)
            {
                Game.UI.LogException(ex.Message);
            }
        }

        /// <summary>
        /// Reads the payload data.
        /// </summary>
        private void ReadPayloadCallback(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                int bytesRead = e.BytesTransferred;
                if (bytesRead == 0)
                {
                    // TODO: world server disconnect
                    Game.UI.LogLine("Server has closed the connection");
                    //Game.Reconnect();
                    return;
                }

                Interlocked.Add(ref transferred, bytesRead);
                Interlocked.Add(ref received, bytesRead);

                if (bytesRead == Remaining)
                {
                    // get header and packet, handle it
                    ServerHeader header = (ServerHeader)SocketAsyncState;
                    QueuePacket(new InPacket(header, ReceiveData, ReceiveDataLength));

                    // start new asynchronous read
                    Start();
                }
                else
                {
                    // more payload to read
                    Index += bytesRead;
                    Remaining -= bytesRead;
                    ReadAsync(ReadPayloadCallback, SocketAsyncState);
                }
            }
            catch (NullReferenceException ex)
            {
                Game.UI.LogException(ex.Message);
            }
            catch (SocketException ex)
            {
                Game.UI.LogException(ex.Message);
                //Game.Reconnect();
                return;
            }
        }

        #endregion

        public void HandlePackets()
        {
            foreach (var packet in packetsQueue.BatchDequeue())
                HandlePacket(packet);
        }

        private void HandlePacket(InPacket packet)
        {
            try
            {
                PacketHandler handler;
                if (PacketHandlers.TryGetValue(packet.Header.Command, out handler))
                {
                    Game.UI.LogLine(string.Format("Received {0}", packet.Header.Command), LogLevel.Debug);
                    handler(packet);
                }
                else
                {
                    Game.UI.LogLine(string.Format("Unknown or unhandled command '{0}'", packet.Header.Command), LogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                Game.UI.LogException(ex.Message);
            }
            finally
            {
                packet.Dispose();
            }
        }

        private void QueuePacket(InPacket packet)
        {
            packetsQueue.Enqueue(packet);
        }

        #region GameSocket Members

        public override void Start()
        {
            ReserveData(4, true);
            Index = 0;
            Remaining = 1;
            ReadAsync(ReadSizeCallback);
        }

        public override bool Connect()
        {
            try
            {
                Game.UI.Log(string.Format("Connecting to realm {0}... ", ServerInfo.Name));

                if (connection != null)
                    connection.Close();
                connection = new TcpClient(ServerInfo.Address, ServerInfo.Port);

                Game.UI.LogLine("done!");
            }
            catch (SocketException ex)
            {
                Game.UI.LogLine(string.Format("failed. ({0})", (SocketError)ex.ErrorCode), LogLevel.Error);
                return false;
            }

            return true;
        }

        #endregion

        public void Send(OutPacket packet)
        {
            byte[] data = packet.Finalize(authenticationCrypto);

            try
            {
                connection.Client.Send(data, 0, data.Length, SocketFlags.None);
            }
            catch (ObjectDisposedException ex)
            {
                Game.UI.LogException(ex.Message);
            }
            catch (EndOfStreamException ex)
            {
                Game.UI.LogException(ex.Message);
            }

            Interlocked.Add(ref transferred, data.Length);
            Interlocked.Add(ref sent, data.Length);
        }
    }
}
