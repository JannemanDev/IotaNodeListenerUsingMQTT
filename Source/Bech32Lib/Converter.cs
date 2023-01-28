using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bech32Lib
{
    public static class Converter
    {
        public static int[] Bits(int[] data, int inLen, int fromBits, int toBits, bool pad = true)
        {
            int acc = 0;
            int bits = 0;
            List<int> ret = new List<int>();
            int maxv = (1 << toBits) - 1;
            int maxacc = (1 << (fromBits + toBits - 1)) - 1;

            for (int i = 0; i < inLen; i++)
            {
                int value = data[i];

                if (value < 0 || ((value >> fromBits) != 0)) throw new ArgumentException("Invalid value for convert bits");

                acc = ((acc << fromBits) | value) & maxacc;
                bits += fromBits;

                while (bits >= toBits)
                {
                    bits -= toBits;
                    int j = (acc >> bits) & maxv;
                    ret.Add(j);
                }
            }

            if (pad)
            {
                if (bits != 0)
                {
                    int j = (acc << toBits - bits) & maxv;
                    ret.Add(j);
                }
            }
            else if (bits >= fromBits || (((acc << (toBits - bits))) & maxv) != 0)
            {
                throw new Exception("Invalid data");
            }

            return ret.ToArray();
        }

        public static string ByteArray2Hex(int[] data)
        {
            string result = String.Join("",data.Select(i => i.ToString("x2")).ToList());

            return result;
        }

        public static int[] Hex2ByteArray(string val)
        {
            int[] result = val.Select((c, index) => new { c, index })
                .GroupBy(x => x.index / 2)
                .Select(group => group.Select(elem => elem.c))
                .Select(chars => int.Parse(new string(chars.ToArray()), System.Globalization.NumberStyles.HexNumber))
                .ToArray();

            return result;
        }
    }
}
