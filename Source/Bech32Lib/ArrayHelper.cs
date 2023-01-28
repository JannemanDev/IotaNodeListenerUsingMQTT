using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bech32Lib
{
    internal static class ArrayHelper
    {
        /// <summary>
        ///     Joins multiple arrays together into one array
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arrays">The arrays to be joined</param>
        /// <returns>One array where all arrays are joined</returns>
        public static T[] JoinArrays<T>(params T[][] arrays)
        {
            int totalLength = arrays.Sum(arr => arr.Length);
            T[] result = new T[totalLength];

            int destIndex = 0;
            for (int i = 0; i < arrays.Length; i++)
            {
                int length = arrays[i].Length;
                Array.Copy(arrays[i], 0, result, destIndex, length);
                destIndex += length;
            }

            return result;
        }
    }
}
