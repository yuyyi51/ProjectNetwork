using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Network_Core
{
    public class Packager
    {
        protected byte[] packageHeader =
           {0xff,0x07,0xcd,0x02,
            0x0c,0xff,0x07,0xcd,
            0x06,0x17,0xff,0xff};
        protected byte[] AddHeader(byte[] data)
        {
            byte[] re;
            int length = data.Length;
            byte[] len = ByteConverter.Int2Byte(length);
            re = PackageHeader.Concat(len).Concat(data).ToArray();
            return re;
        }
        public byte[] PackageHeader
        {
            get
            {
                return packageHeader;
            }
        }
        public int Length
        {
            get { return packageHeader.Length; }
        }
        public Packager()
        {

        }
        public Packager(byte[] header)
        {
            packageHeader = header;
        }

        public byte[] Pack(object obj)
        {
            byte[] data = ByteConverter.Object2Byte(obj);
            return AddHeader(data);
        }
        public object UnPack(byte[] obj)
        {
            object re = ByteConverter.Byte2Object(obj);
            return re;
        }
        public bool Check(byte[] tem)
        {
            if (tem.Length < Length)
                return false;
            for(int i = 0; i < tem.Length; ++i)
            {
                if(tem[i] != packageHeader[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
