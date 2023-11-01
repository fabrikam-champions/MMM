using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MMM.Api
{
    internal static class PlantUmlHelper
    {
        public static string EncodeP(string text)
        {
            byte[] data = Encoding.UTF8.GetBytes(text);
            byte[] compressed;

            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (DeflateStream deflateStream = new DeflateStream(memoryStream, CompressionMode.Compress, true))
                {
                    deflateStream.Write(data, 0, data.Length);
                }

                compressed = memoryStream.ToArray();
            }

            return Encode64(compressed);
        }

        public static char Encode6Bit(int b)
        {
            if (b < 10)
            {
                return (char)(48 + b);
            }
            else if (b < 36)
            {
                return (char)(65 + (b - 10));
            }
            else if (b < 62)
            {
                return (char)(97 + (b - 36));
            }
            else if (b == 62)
            {
                return '-';
            }
            else if (b == 63)
            {
                return '_';
            }
            else
            {
                return '?';
            }
        }

        public static string Append3Bytes(int b1, int b2, int b3)
        {
            int c1 = b1 >> 2;
            int c2 = ((b1 & 0x3) << 4) | (b2 >> 4);
            int c3 = ((b2 & 0xF) << 2) | (b3 >> 6);
            int c4 = b3 & 0x3F;

            string result = "";
            result += Encode6Bit(c1 & 0x3F);
            result += Encode6Bit(c2 & 0x3F);
            result += Encode6Bit(c3 & 0x3F);
            result += Encode6Bit(c4 & 0x3F);

            return result;
        }

        public static string Encode64(byte[] c)
        {
            string str = "";
            int len = c.Length;

            for (int i = 0; i < len; i += 3)
            {
                if (i + 2 == len)
                {
                    str += Append3Bytes(c[i], c[i + 1], 0);
                }
                else if (i + 1 == len)
                {
                    str += Append3Bytes(c[i], 0, 0);
                }
                else
                {
                    str += Append3Bytes(c[i], c[i + 1], c[i + 2]);
                }
            }

            return str;
        }

    }
}
