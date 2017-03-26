using System;
using System.Collections.Generic;

namespace Common
{
    public sealed class Bytes
    {
        public static readonly byte[] AlphaUpper = new byte[256];
        public static readonly byte[] Digits = new byte[256];
        public static readonly byte[] Empty = new byte[0];

        static Bytes()
        {
            for (int i = 0; i <= 255; i++)
            {
                AlphaUpper[i] = (i >= 97 && i <= 122) ? (byte)(i - 32) : (byte)i;
                Digits[i] = (i >= 48 && i <= 57) ? (byte)(i - 48) : (byte)255;
            }
        }

        private Bytes()
        {
        }


        public static void ToUpper(ArraySegment<byte> data)
        {
            ToUpper(data.Array, data.Offset, data.Count);
        }

        public static void ToUpper(byte[] data, int offset, int length)
        {
            var i = offset;
            var end = offset + length;
            while (i < end)
            {
                data[i] = AlphaUpper[data[i]];
                i++;
            }
        }

        public static bool IsSame(byte[] search, ArraySegment<byte> data)
        {
            return IsSame(search, data.Array, data.Offset, data.Count);
        }

        public static bool IsSame(byte[] search, byte[] data, int offset, int length)
        {
            if (search.Length != length) return false;
            for (var i = 0; i < length; i++)
            {
                if (search[i] != data[offset + i]) return false;
            }
            return true;
        }

        public static bool StartsWith(byte[] search, ArraySegment<byte> data)
        {
            return StartsWith(search, data.Array, data.Offset, data.Count);
        }

        public static bool StartsWith(byte[] search, byte[] data, int offset, int length)
        {
            if (length >= search.Length)
            {
                for (var i = 0; i < search.Length; i++)
                {
                    if (data[offset + i] != search[i]) return false;
                }
                return true;
            }
            return false;
        }

        public static bool EndsWith(byte[] search, ArraySegment<byte> data)
        {
            return EndsWith(search, data.Array, data.Offset, data.Count);
        }

        public static bool EndsWith(byte[] search, byte[] data, int offset, int length)
        {
            if (length >= search.Length)
            {
                var beginOffset = offset + length - search.Length;
                for (var i = 0; i < search.Length; i++)
                {
                    if (data[beginOffset + i] != search[i]) return false;
                }
                return true;
            }
            return false;
        }

        public static int OffsetOf(byte[] search, ArraySegment<byte> data)
        {
            return OffsetOf(search, data.Array, data.Offset, data.Count);
        }

        public static int OffsetOf(byte[] search, byte[] data, int offset, int length)
        {
            for (var dataOffset = offset; dataOffset <= offset + length - search.Length; dataOffset++)
            {
                var found = true;
                for (var i = 0; i < search.Length; i++)
                {
                    if (data[dataOffset + i] != search[i])
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                {
                    return dataOffset;
                }
            }
            return -1;
        }

        // This is like string.Split for byte array with option to ignore consecutive delimiters
        public static ArraySegment<byte>[] Split(ArraySegment<byte> data, byte[] delimiters)
        {
            var segments = new List<ArraySegment<byte>>(4);

            int segmentBegin = 0;
            var delimitersLength = delimiters.Length;
            bool previousByteWasDelimiter = false;
            for (var i = 0; i < data.Count; i++)
            {
                var thisByte = data.Array[data.Offset + i];

                var isDelimiter = false;
                for (var j = 0; j < delimitersLength; j++)
                {
                    if (delimiters[j] == thisByte)
                    {
                        isDelimiter = true;
                        break;
                    }
                }

                if (isDelimiter)
                {
                    if (!previousByteWasDelimiter && segmentBegin < i)
                    {
                        segments.Add(new ArraySegment<byte>(data.Array, data.Offset + segmentBegin, i - segmentBegin));
                    }
                    previousByteWasDelimiter = true;
                    segmentBegin = i + 1;
                }
                else
                {
                    previousByteWasDelimiter = false;
                }
            }

            if (!previousByteWasDelimiter && segmentBegin < data.Count)
            {
                segments.Add(new ArraySegment<byte>(data.Array, data.Offset + segmentBegin, data.Count - segmentBegin));
            }

            return segments.ToArray();
        }
    }
}
