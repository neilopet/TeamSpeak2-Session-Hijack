using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TS2Terrorist
{
    class TS2Packet
    {
        public ushort Class;
        public ushort Type;
        public uint SessionID;
        public uint ClientID;
        public uint SequenceNumber;
        public uint CRC32;
        public byte[] Data;

        public void Create( ushort Class, ushort Type, uint SessionID, uint ClientID, uint SequenceNumber )
        {
            this.Class = Class;
            this.Type = Type;
            this.SessionID = SessionID;
            this.ClientID = ClientID;
            this.CRC32 = (uint)0x00;
            this.SequenceNumber = SequenceNumber;
        }

        public void ReadPacket(byte[] recv)
        {
            Stream stream = new MemoryStream(recv);
            BinaryReader reader = new BinaryReader(stream);

            this.Class = reader.ReadUInt16();
            if (this.Class == TS2.ACK)
            {
                reader.ReadInt16();
                this.SessionID = reader.ReadUInt32();
                this.ClientID = reader.ReadUInt32();
                this.SequenceNumber = reader.ReadUInt32();
            }
            else if (this.Class == TS2.CONNECTION)
            {
                this.Type = reader.ReadUInt16();
                this.SessionID = reader.ReadUInt32();
                this.ClientID = reader.ReadUInt32();
                this.SequenceNumber = reader.ReadUInt32();
                this.CRC32 = reader.ReadUInt32();
                this.Data = reader.ReadBytes(recv.Length - 20);
            }
            else
            {
                this.Type = reader.ReadUInt16();
                this.SessionID = reader.ReadUInt32();
                this.ClientID = reader.ReadUInt32();
                this.SequenceNumber = reader.ReadUInt32();
                reader.ReadUInt32();
                this.CRC32 = reader.ReadUInt32();
                this.Data = reader.ReadBytes(recv.Length - 24);
            }
        }

        public void Login( string Nickname, string Username, string Password, string Protocol, string Platform )
        {
            /* Version :P */
            byte[] Unknown = new byte[]{ 0x03, 0x00, 0x01, 0x00, 0x21, 0x00, 0x07, 0x00, 0x00, 0x02 };
            byte ProtocolLength = (byte)Protocol.Length;
            byte PlatformLength = (byte)Platform.Length;
            byte UsernameLength = (byte)Username.Length;
            byte PasswordLength = (byte)Password.Length;
            byte NicknameLength = (byte)Nickname.Length;

            this.Data = this.combine(
                new byte[] { ProtocolLength },
                this.Pad(Protocol, 29),
                new byte[] { PlatformLength },
                this.Pad(Platform, 29),
                Unknown,
                new byte[] { UsernameLength },
                this.Pad(Username, 29),
                new byte[] { PasswordLength },
                this.Pad(Password, 29),
                new byte[] { NicknameLength },
                this.Pad(Nickname, 29)
            );
        }

        public void Register(string Username, string Password)
        {
            /* 
             * Request:
             * Username: 1 byte Length + Padded 29 bytes
             * Password: 1 byte Length + Padded 29 bytes
             f0be340100000000000000000100000000000000a9bc8076087465737431323334000000000000000000000000000000000000000000076675636b796f7500000000000000000000000000000000000000000000
             f0be3401b60db70001000000020000000000000063a13a3106676f766e61320000000000f01ee101a771420000000000ac1ee101382e0561646d696e0000000013274800ffffffff372748003f274800b49ce401
             
             f0be36010000000000000000010000000000000017ed8606087465737431323334000000000000000000000000000000000000000000076675636b796f750000000000000000000000000000000000000000000001
             f0be360166b3720061000000010000000000000064a13f24087465737431323334000000000000000000000000000000000000000000076675636b796f750000000000000000000000000000000000000000000001
             f0be360166b3720061000000010000000000000020aecd2108746573743132333400000000000000000000000000000000000000000007274800ffffffff372748003f274800b49ce40190ab4601
             f0be360175a4a9006c00000001000000000000002a658cf6087465737431323334000000000000000000000000000000000000000000076675636b796f75274800ffffffff372748003f274800b49ce40190ab4601
             f0be360129eabb005300000002000000000000009e761812087465737431323334f01ee101a771420000000000ac1ee101382e480000076675636b796f75274800ffffffff372748003f274800b49ce40190ab4601
             f0be360129eabb00530000000300000000000000061ad392087465737431323334f01ee101a771420000000000ac1ee101382e480000076675636b796f75274800ffffffff372748003f274800b49ce40190ab4600
             
             * Response:
             f0be6b00b60db7000100000004000000000000000a1fd09c01000000010200000000 
             */
            this.Class = TS2.STANDARD;
            this.Type = TS2.ADMIN_REGISTER;
            this.Data = this.combine(
                   new byte[] { (byte)Username.Length },
                   this.Pad(Username, 29),
                   //new byte[] { 0xf0, 0x1e, 0xe1, 0x01, 0xa7, 0x71, 0x42, 0x00, 0x00, 0x00, 0x00, 0x00, 0xac, 0x1e, 0xe1, 0x01, 0x38, 0x2e, 0x48, 0x00, 0x00 },
                   new byte[] { (byte)Password.Length },
                   this.Pad(Password, 29),
                   new byte[] { 0x01 } /* server admin */
                   //new byte[] { 0x27, 0x48, 0x00, 0xff, 0xff, 0xff, 0xff, 0x37, 0x27, 0x48, 0x00, 0x3f, 0x27, 0x48, 0x00, 0xb4, 0x9c, 0xe4, 0x01, 0x90, 0xab, 0x46, 0x01 }
            );
        }

        public void Raw(byte[] data)
        {
            this.Data = data;
        }

        public byte[] Pad(string value, int size)
        {
            byte[] byteArray = new byte[ size ];
            Array.Copy(Encoding.ASCII.GetBytes(value), byteArray, System.Math.Min(size, value.Length));
            return byteArray;
        }

        public byte[] combine(params byte[][] arr)
        {
            int szBuf = 0;
            foreach (byte[] b in arr)
                szBuf += b.Length;

            byte[] ret = new byte[szBuf];
            int offset = 0;
            foreach (byte[] b in arr)
            {
                System.Buffer.BlockCopy(b, 0, ret, offset, b.Length);
                offset += b.Length;
            }
            return ret;
        }

        public byte[] toByteArray()
        {
            byte[] ret = null;
            if (this.Class == TS2.CONNECTION)
            {
                if (this.Type == TS2.PING)
                {
                    ret = this.combine(
                        BitConverter.GetBytes(this.Class),
                        BitConverter.GetBytes(this.Type),
                        BitConverter.GetBytes(this.SessionID),
                        BitConverter.GetBytes(this.ClientID),
                        BitConverter.GetBytes(this.SequenceNumber),
                        BitConverter.GetBytes(this.CRC32)
                    );
                }
                else
                {
                    ret = this.combine(
                        BitConverter.GetBytes(this.Class),
                        BitConverter.GetBytes(this.Type),
                        BitConverter.GetBytes(this.SessionID),
                        BitConverter.GetBytes(this.ClientID),
                        BitConverter.GetBytes(this.SequenceNumber),
                        BitConverter.GetBytes(this.CRC32),
                        this.Data
                    );
                }
            }
            else if (this.Class == TS2.STANDARD)
            {
                 ret = this.combine(
                    BitConverter.GetBytes(this.Class),
                    BitConverter.GetBytes(this.Type),
                    BitConverter.GetBytes(this.SessionID),
                    BitConverter.GetBytes(this.ClientID),
                    BitConverter.GetBytes(this.SequenceNumber),
                    new byte[] { 0x00, 0x00, 0x00, 0x00 }, /* fragment & resent count */
                    BitConverter.GetBytes(this.CRC32),
                    this.Data
                );
            }
            else if (this.Class == TS2.ACK)
            {
                /* 0050f1000000001fbc08374b08004500002c75f80000801100000a00000236c92f72d5f7223f00187066f1be0000adbe9d000100000002000000 */
                /* 0050f1000000001fbc08374b08004500002b76a50000801100000a00000236c92f72d3bc223f00177065f1be00c9359e000100000001000000 */
                /* 0050f1000000001fbc08374b08004500002b78750000801100000a00000236c92f72d3bc223f00177065f1be00691312000100000001000000 */
                ret = this.combine(
                    BitConverter.GetBytes(this.Class),
                    new byte[] { 0x00, 0x00 },
                    BitConverter.GetBytes(this.SessionID),
                    BitConverter.GetBytes(this.ClientID),
                    BitConverter.GetBytes(this.SequenceNumber)
                );
            }

            /* CRC32 has not been computed */
            if (this.CRC32.Equals(0))
            {
                this.CRC32 = Crc32.Crc32Algorithm.Compute(ret);
                return this.toByteArray();
            }

            //this.printByteArray(ret);

            /* CRC32 has been computed */
            return ret;
        }

        public void printByteArray(byte[] ba)
        {
            foreach (byte b in ba)
                Console.Write(String.Format("{0:X2}", b));
            Console.WriteLine("\r\n--------------");
        }
    }

}
