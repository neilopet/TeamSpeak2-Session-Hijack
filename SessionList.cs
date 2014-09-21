using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace TS2Terrorist
{

    using System;
    using System.Text;
    using System.Numerics;


    public class SessionList
    {
        public uint s1;
        public uint s2;
        public uint clid;
        public uint xn;
        public long modulus;
        public uint seed = 0;
        public uint iter = 0;
        public uint iter_n = 0;

        public SessionList(uint client_id, uint session_id_1, uint session_id_2)
        {
            this.clid = client_id;
            this.s1 = session_id_1;
            this.s2 = session_id_2;
            this.xn = 0;
            this.modulus = 4294967296;
        }

        public BigInteger next()
        {
            if (0 == this.iter)
                this.iter = this.seed;

            if (0 == this.iter || this.iter == 1)
                return -1;

            if (this.iter_n > this.clid)
                return -1;

            this.iter_n++;
            this.iter = this.gen_x(this.iter);

            return this.gen_session(this.iter);
        }

        public void getSeed(uint start)
        {
            uint x = start;
            while (!this.vs1(this.s1, x) || !this.vs2(this.s2, x))
            {
                if (x == (this.modulus - 1))
                    return;
                x++;
            }
            this.xn = x;

            BigInteger seed = x;
            uint multiplier = 3645876429;

            uint n = this.clid;
            while (n > 0)
            {
                seed = BigInteger.Multiply(multiplier, (seed - 1)) % this.modulus;
                n--;
            }

            this.seed = (uint)seed;
        }

        public bool vs1(uint s1, uint i)
        {
            return (s1 == this.gen_session(i));
        }

        public bool vs2(uint s2, uint i)
        {
            return (s2 == this.gen_session(this.gen_x(i)));
        }

        public uint gen_x(BigInteger last)
        {
            return (uint)(
                ((last * 134775813) + 1) % this.modulus
            );
        }

        public uint gen_session(uint x)
        {
            return Convert.ToUInt32((((long)x) * 16777215) >> 32);
        }
    }

}
