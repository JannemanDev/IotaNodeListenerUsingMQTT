using NUnit.Framework;
using System;
using System.Linq;

namespace Bech32Lib.Tests
{
    public class Bech32Tests
    {
        readonly Bech32TestCases[] bech32TestCases = new Bech32TestCases[] {
            new Bech32TestCases("atoi",
                new string[] {
                    "atoi1qpszqzadsym6wpppd6z037dvlejmjuke7s24hm95s9fg9vpua7vluehe53e",
                    "atoi1qzfvkkp398v7hhvu89fu88hxctf7snwc9sf3a3nd7msfv77jk7qk2ah07s3",
                    "atoi1qrhacyfwlcnzkvzteumekfkrrwks98mpdm37cj4xx3drvmjvnep6x8x4r7t",
                },
                new string[] {
                    "60200bad8137a704216e84f8f9acfe65b972d9f4155becb4815282b03cef99fe",
                    "92cb583129d9ebdd9c3953c39ee6c2d3e84dd82c131ec66df6e0967bd2b78165",
                    "efdc112efe262b304bcf379b26c31bad029f616ee3ec4aa6345a366e4c9e43a3",
                }
            ),
            new Bech32TestCases("iota",
                new string[] {
                    "iota1qpw6k49dedaxrt854rau02talgfshgt0jlm5w8x9nk5ts6f5x5m759nh2ml",
                    "iota1qrhacyfwlcnzkvzteumekfkrrwks98mpdm37cj4xx3drvmjvnep6xqgyzyx",
                    "iota1qzh2qpy03j4lgf9x85e37nhtwyzluhlgvzyyl04j9fdx5f3kcldr26p4je4",
                    "iota1qqynj4u227rywgtsh06dm4h8n9nvejyy9geffujpg7pzm9exc6usjqw46rj",
                    "iota1qr9gqqkf9kqlwydz9m4t7vvvt6t4rj35af523emwlnl0yhkggacjvupgckf",
                    "iota1qq5hvsvzljufx5r9tjer46h0t0wvw4yam0sg5gae0yrvymv5eydfkkaynau",
                    "iota1qqggnkfvte7hcd9qc83kds00jkkqjjsx203fgd9w7hshglk2qr5lvgd25x6",
                    "iota1qql67es70lqh3qtht00gnfzrwusrna8jpy7u375ff2yuqqv9he43xluasum",
                    "iota1qzcu634e3my9q8dp4pd2nn5unffwzln89d43syf2qst5vyj9rfe4jrmfep6",
                },
                new string[] {
                    "5dab54adcb7a61acf4a8fbc7a97dfa130ba16f97f7471cc59da8b869343537ea",
                    "efdc112efe262b304bcf379b26c31bad029f616ee3ec4aa6345a366e4c9e43a3",
                    "aea0048f8cabf424a63d331f4eeb7105fe5fe860884fbeb22a5a6a2636c7da35",
                    "0939578a5786472170bbf4ddd6e79966ccc8842a3294f24147822d9726c6b909",
                    "ca8002c92d81f711a22eeabf318c5e9751ca34ea68a8e76efcfef25ec8477126",
                    "29764182fcb89350655cb23aeaef5bdcc7549ddbe08a23b97906c26d94c91a9b",
                    "1089d92c5e7d7c34a0c1e366c1ef95ac094a0653e29434aef5e1747eca00e9f6",
                    "3faf661e7fc17881775bde89a443772039f4f2093dc8fa894a89c00185be6b13",
                    "b1cd46b98ec8501da1a85aa9ce9c9a52e17e672b6b18112a04174612451a7359",
                }
            ),
        };

        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void TestPolyMod1()
        {
            int[] values = new int[] {
                3, 3, 3, 3, 0, 1,20,15, 9, 0,
                1,16, 2, 0, 2,29,13,16, 4,27,
               26,14, 1, 1, 1,13,26, 2,15,17,
               30,13,12,31,25,18,27,18,28,22,
               25,30,16,10,21,23,27, 5,20,16,
                5, 9, 8, 5,12, 1,28,29,30,12,
               31,28, 0, 0, 0, 0, 0, 0};

            int numValues = 68;

            Assert.IsTrue(values.Length == numValues);

            int chk = Bech32.PolyMod(values, numValues);
            Assert.IsTrue(chk == 863818296);
        }

        [Test]
        public void TestPolyMod2()
        {
            int[] values = new int[] {
                3, 3, 3, 3, 0, 1,20,15, 9, 0,
                1,16, 2, 0, 2,29,13,16, 4,27,
               26,14, 1, 1, 1,13,26, 2,15,17,
               30,13,12,31,25,18,27,18,28,22,
               25,30,16,10,21,23,27, 5,20,16,
                5, 9, 8, 5,12, 1,28,29,30,12,
               31,28,25,23,25,20,17,25
            };

            int numValues = 68;

            Assert.IsTrue(values.Length == numValues);

            int chk = Bech32.PolyMod(values, numValues);
            Assert.IsTrue(chk == 1);
        }

        [Test]
        public void TestHrpExpand1()
        {
            string hrp = "atoi";
            int hrpLen = 4;

            Assert.IsTrue(hrp.Length == hrpLen);

            int[] expectedResult = new int[] { 3, 3, 3, 3, 0, 1, 20, 15, 9 };
            int[] actualResult = Bech32.HrpExpand(hrp, hrpLen);

            CollectionAssert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public void TestCreateChecksum1()
        {
            string hrp = "atoi";
            int[] convertedDataChars = new int[] {
                0, 1,16, 2, 0, 2,29,13,16, 4,
               27,26,14, 1, 1, 1,13,26, 2,15,
               17,30,13,12,31,25,18,27,18,28,
               22,25,30,16,10,21,23,27, 5,20,
               16, 5, 9, 8, 5,12, 1,28,29,30,
               12,31,28
            };

            Assert.IsTrue(convertedDataChars.Length == 53);

            int[] expectedResult = new int[] { 25, 23, 25, 20, 17, 25 };

            int[] actualResult = Bech32.CreateChecksum(hrp, convertedDataChars);

            CollectionAssert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public void TestVerifyChecksum1()
        {
            string hrp = "atoi";
            int[] convertedDataChars = new int[] {
                0, 1,16, 2, 0, 2,29,13,16, 4,
               27,26,14, 1, 1, 1,13,26, 2,15,
               17,30,13,12,31,25,18,27,18,28,
               22,25,30,16,10,21,23,27, 5,20,
               16, 5, 9, 8, 5,12, 1,28,29,30,
               12,31,28,25,23,25,20,17,25
            };

            Assert.IsTrue(Bech32.VerifyChecksum(hrp, convertedDataChars));
        }

        [Test]
        public void TestDecode()
        {
            for (int i = 0; i < bech32TestCases.Length; i++)
            {
                for (int j = 0; j < bech32TestCases[i].Count; j++)
                {
                    string[] decodeResult = Bech32.Decode(bech32TestCases[i].Bech32Addresses[j]);
                    string hrp = decodeResult[0];
                    string ed25519Address = decodeResult[1];

                    Assert.IsTrue(hrp.Equals(bech32TestCases[i].Hrp));
                    Assert.IsTrue(ed25519Address.Equals(bech32TestCases[i].Ed25519Addresses[j]));
                }
            }
        }

        [Test]
        public void TestEncode()
        {
            for (int i = 0; i < bech32TestCases.Length; i++)
            {
                for (int j = 0; j < bech32TestCases[i].Count; j++)
                {
                    string actualResult = Bech32.Encode(bech32TestCases[i].Hrp, bech32TestCases[i].Ed25519Addresses[j]);

                    Assert.IsTrue(actualResult.Equals(bech32TestCases[i].Bech32Addresses[j]));
                }
            }
        }
    }

    public class Bech32TestCases
    {
        /// <summary>
        /// Human Readable Part: iota (mainnet), atoi (testnet)
        /// </summary>
        public string Hrp { get; set; }
        public string[] Bech32Addresses { get; set; }
        public string[] Ed25519Addresses { get; set; }
        public int Count => Bech32Addresses.Length;

        public Bech32TestCases(string hrp, string[] bech32Addresses, string[] ed25519Addresses)
        {
            if (bech32Addresses.Length != ed25519Addresses.Length) throw new ArgumentException($"Number of bech32Addresses {bech32Addresses.Length} is not same as ed25519Addresses {ed25519Addresses.Length}!");

            Hrp = hrp;
            Bech32Addresses = bech32Addresses;
            Ed25519Addresses = ed25519Addresses;
        }
    }
}