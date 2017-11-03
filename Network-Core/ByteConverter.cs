using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Network_Core
{
    public class ByteConverter
    {
        public static byte[] Object2Byte(object obj)
        {
            byte[] re;
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(ms, obj);
                re = ms.GetBuffer();
            }

            return re;
        }
        public static object Byte2Object(byte[] b)
        {
            object re;
            using (MemoryStream ms = new MemoryStream(b))
            {
                BinaryFormatter bf = new BinaryFormatter();
                re = bf.Deserialize(ms);
            }
            return re;
        }
        public static string Byte2Hex(byte[] b)
        {
            string s = "";
            int len = b.Length;
            if (len == 0)
                return s;
            s += b[0].ToString("x2");
            for (int i = 1; i < len; ++i)
            {
                s += "-";
                s += b[i].ToString("x2");
            }
            return s;
        }
        public static byte[] Int2Byte(int t)
        {
            byte[] re;
            re = BitConverter.GetBytes(t);
            return re;
        }
        public static int Byte2Int(byte[] b)
        {
            int re = BitConverter.ToInt32(b, 0);
            return re;
        }
    }
}
