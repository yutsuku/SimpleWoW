using System;
using System.Numerics;
using System.Text;
using Client.Crypto;

namespace Client.World.Network
{
    public partial class WorldSocket
    {
        internal uint counter, clientTicks;

        void ResetTimeSync()
        {
            counter = 0;
            clientTicks = 0;
        }

        [PacketHandler(WorldCommand.SMSG_TIME_SYNC_REQ)]
        void HandleTimeSync(InPacket packet)
        {
            counter = packet.ReadUInt32();
            counter++;

            clientTicks = (uint)DateTime.Now.Ticks;

            OutPacket response = new OutPacket(WorldCommand.CMSG_TIME_SYNC_RESP);
            response.Write(counter);
            response.Write(clientTicks);

            Send(response);
        }
    }
}
