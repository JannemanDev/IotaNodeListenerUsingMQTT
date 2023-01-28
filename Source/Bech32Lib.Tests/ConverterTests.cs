using NUnit.Framework;
using System.Linq;

namespace Bech32Lib.Tests
{
    public class ConverterTests
    {
        readonly int[] stringAsByteArray = new int[]
        {
                0, 96, 32, 11,173,129, 55,167,  4, 33,
              110,132,248,249,172,254,101,185,114,217,
              244, 21, 91,236,180,129, 82,130,176, 60,
              239,153,254
        };
        readonly string hexString = "0060200bad8137a704216e84f8f9acfe65b972d9f4155becb4815282b03cef99fe";

        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void TestBits1()
        {
            int[] data = new int[]
            {
                0, 96, 32, 11,173,129, 55,167,  4, 33,
              110,132,248,249,172,254,101,185,114,217,
              244, 21, 91,236,180,129, 82,130,176, 60,
              239,153,254
            };
            int inLen = 33;
            int fromBits = 8;
            int toBits = 5;
            bool pad = true;

            int[] expectedResult = new int[]
            {
                0, 1,16, 2, 0, 2,29,13,16, 4,
               27,26,14, 1, 1, 1,13,26, 2,15,
               17,30,13,12,31,25,18,27,18,28,
               22,25,30,16,10,21,23,27, 5,20,
               16, 5, 9, 8, 5,12, 1,28,29,30,
               12,31,28
            };

            int[] actualResult = Converter.Bits(data, inLen, fromBits, toBits, pad);

            CollectionAssert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public void TestBits2()
        {
            int[] data = new int[]
            {
                0, 1,16, 2, 0, 2,29,13,16, 4,
               27,26,14, 1, 1, 1,13,26, 2,15,
               17,30,13,12,31,25,18,27,18,28,
               22,25,30,16,10,21,23,27, 5,20,
               16, 5, 9, 8, 5,12, 1,28,29,30,
               12,31,28
            };
            int inLen = 53;
            int fromBits = 5;
            int toBits = 8;
            bool pad = false;

            int[] expectedResult = new int[]
            {
                0, 96, 32, 11,173,129, 55,167,  4, 33,
              110,132,248,249,172,254,101,185,114,217,
              244, 21, 91,236,180,129, 82,130,176, 60,
              239,153,254
            };

            int[] actualResult = Converter.Bits(data, inLen, fromBits, toBits, pad);

            CollectionAssert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public void TestHex2ByteArray()
        {
            int[] actualOutput = Converter.Hex2ByteArray(hexString);
            CollectionAssert.AreEqual(stringAsByteArray, actualOutput);
        }

        [Test]
        public void TestByteArray2Hex()
        {
            string actualOutput = Converter.ByteArray2Hex(stringAsByteArray);

            Assert.AreEqual(hexString, actualOutput);
        }
    }
}