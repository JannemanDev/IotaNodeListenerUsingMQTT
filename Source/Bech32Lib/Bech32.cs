namespace Bech32Lib
{
    public static class Bech32
    {
        private static int[] CharsetKey => new int[]
        {
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            15,
            -1,
            10,
            17,
            21,
            20,
            26,
            30,
            7,
            5,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            29,
            -1,
            24,
            13,
            25,
            9,
            8,
            23,
            -1,
            18,
            22,
            31,
            27,
            19,
            -1,
            1,
            0,
            3,
            16,
            11,
            28,
            12,
            14,
            6,
            4,
            2,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            29,
            -1,
            24,
            13,
            25,
            9,
            8,
            23,
            -1,
            18,
            22,
            31,
            27,
            19,
            -1,
            1,
            0,
            3,
            16,
            11,
            28,
            12,
            14,
            6,
            4,
            2,
            -1,
            -1,
            -1,
            -1,
            -1
        };
        public static string Charset => "qpzry9x8gf2tvdw0s3jn54khce6mua7l";
        private static int[] Generator => new int[]
        {
            0x3b6a57b2,
            0x26508e6d,
            0x1ea119fa,
            0x3d4233dd,
            0x2a1462b3
        };
        public static int PolyMod(int[] values, int numValues)
        {
            int chk = 1;
            for (int i = 0; i < numValues; i++)
            {
                int t = chk >> 25;
                chk = (chk & 0x1ffffff) << 5 ^ values[i];
                for (int j = 0; j < 5; j++)
                {
                    int v = ((t >> j) & 1) != 0 ? Generator[j] : 0;
                    chk ^= v;
                }
            }

            return chk;
        }
        public static int[] HrpExpand(string hrp, int hrpLength)
        {
            int[] ep1 = new int[hrpLength];
            int[] ep2 = new int[hrpLength];

            for (int i = 0; i < hrpLength; i++)
            {
                int ord = hrp[i];
                ep1[i] = ord >> 5;
                ep2[i] = ord & 31;
            }

            return ArrayHelper.JoinArrays(ep1, new int[] { 0 }, ep2);
        }

        public static int[] CreateChecksum(string hrp, int[] convertedDataChars)
        {
            int[] hrpExpanded = HrpExpand(hrp, hrp.Length);
            int[] values = ArrayHelper.JoinArrays(hrpExpanded, convertedDataChars);

            int polyMod = PolyMod(ArrayHelper.JoinArrays(values, new int[] { 0, 0, 0, 0, 0, 0 }), values.Length + 6) ^ 1;

            int[] result = new int[6];

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = (polyMod >> 5 * (5 - i)) & 31;
            }

            return result;
        }

        public static bool VerifyChecksum(string hrp, int[] convertedDataChars)
        {
            int[] a = ArrayHelper.JoinArrays(HrpExpand(hrp, hrp.Length), convertedDataChars);

            return PolyMod(a, a.Length) == 1;
        }

        /// <summary>
        /// Encodes a hrp (Human Readable Part) and ED25519Address into a wallet address
        /// </summary>
        /// <param name="hrp">iota (mainnet) or atoi (testnet)</param>
        /// <param name="addressEd25519">53 characters string Bech32 encoded</param>
        /// <returns></returns>
        public static string Encode(string hrp, string addressEd25519)
        {
            int[] combinedData = Converter.Hex2ByteArray(addressEd25519).Prepend(0).ToArray();
            int[] bits = Converter.Bits(combinedData, combinedData.Length, 8, 5, true);

            int[] chars = ArrayHelper.JoinArrays(bits, CreateChecksum(hrp, bits));
            int n = chars.Length;
            char[] encoded = new char[n];

            for (int i = 0; i < n; i++)
            {
                encoded[i] = Charset[chars[i]];
            }

            string s = new string(encoded);

            return $"{hrp}1{s}";
        }

        /// <summary>
        /// Decode a wallet address into hrp (Human Readable Part) and ED25519Address
        /// </summary>
        /// <param name="sBech">Wallet address starting with iota (mainnet) or atoi (testnet)</param>
        /// <returns>
        /// array where<br></br>
        ///  element 0: hrp (Human Readable Part)<br></br>
        ///  element 1: ED25519Address
        /// </returns>
        /// <exception cref="ArgumentException"></exception>
        public static string[] Decode(string sBech)
        {
            char[] chars = sBech.ToCharArray();

            int len = chars.Length;

            if (len < 8) throw new ArgumentException("Bech32 is too short");

            bool hUpper = false;
            bool hLower = false;
            int pos = -1;

            for (int i = 0; i < len; i++)
            {
                int x = chars[i];
                if (x < 33 || x > 126) throw new ArgumentException($"Character {x} at index {i} of string {sBech} is out of range");
                if (x >= 0x61 && x <= 0x7A) hLower = true;
                if (x >= 0x41 && x <= 0x5A)
                {
                    hUpper = true;
                    chars[i] = (char)(x + 0x20);
                    x = chars[i];
                }
                if (x == 0x31) pos = i;
            }

            if (hUpper && hLower) throw new ArgumentException($"Data {sBech} contains mixture of higher/lower case characters");

            if (pos == -1) throw new ArgumentException($"No separator character 1 found in {sBech}");

            if (pos < 1) throw new ArgumentException($"HRP is empty");

            if ((pos + 7) > len) throw new ArgumentException($"Checksum is too short");

            string hrp = new string(chars.Take(pos).ToArray());

            List<int> data = new List<int>();

            for (int i = pos + 1; i < len; i++)
            {
                int c = (chars[i] & 0x80) != 0 ? -1 : CharsetKey[chars[i]];
                data.Add(c);
            }

            if (!VerifyChecksum(hrp, data.ToArray())) throw new ArgumentException("Invalid checksum");

            string dataAsStr = new string(data
                .Take(data.Count - 6)
                .Select(i => (char)i).ToArray());

            int[] dataAsChars = dataAsStr.Select(c => (int)c).ToArray();
            int[] result = Converter.Bits(dataAsChars, dataAsChars.Length, 5, 8, false);
            string hexString = Converter.ByteArray2Hex(result).Substring(2);

            return new string[] { hrp, hexString };
        }
    }
}