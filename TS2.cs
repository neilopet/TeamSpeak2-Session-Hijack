using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TS2Terrorist
{
    class TS2
    {
        /* Class */
        public const ushort STANDARD = 0xbef0;
        public const ushort ACK = 0xbef1;
        public const ushort CLIENT_VOICE = 0xbef2;
        public const ushort SERVER_VOICE = 0xbef3;
        public const ushort CONNECTION = 0xbef4;

        /* Type */
        public const ushort PING = 0x0001;
        public const ushort PING_REPLY = 0x0002;
        public const ushort LOGIN = 0x0003;
        public const ushort LOGINREPLY = 0x0004;
        public const ushort LOGIN2 = 0x0005;
        public const ushort CHANNELLIST = 0x0006;
        public const ushort PLAYERLIST = 0x0007;
        public const ushort LOGINEND = 0x0008;
        public const ushort NEWPLAYER = 0x0064;
        public const ushort PLAYERQUIT = 0x0065;
        public const ushort GIVESA = 0x0133;
        public const ushort REGISTER = 0x0134;
        public const ushort ADMIN_REGISTER = 0x0136;
        public const ushort DISCONNECT = 0x12c;
        public const ushort KICKPLAYER = 0x12d;
        public const ushort BANPLAYER = 0x145;
        public const ushort MSGPLAYER = 0x1ae;
    }
}
