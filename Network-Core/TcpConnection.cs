using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Threading.Tasks;

namespace Network_Core
{
    public class TcpConnection
    {
        public delegate void TcpConnectionEventHandler(TcpConnection sender);
        public delegate void TcpConnectionReceiveDoneHandler(TcpConnection sender, byte[] data);
        public delegate void TcpConnectionReceiveObjectDoneHandler(TcpConnection sender, object obj);
        
        protected TcpClient client;
        protected byte[] buffer;
        protected int bufferSize;
        protected byte[] receivedData;
        protected int remainReceiveLength;
        protected Packager packager;
        protected bool receiving;
        protected bool connected;

        public event TcpConnectionEventHandler ConnectDoneEvent;
        public event TcpConnectionEventHandler ConnectFailedEvent;
        public event TcpConnectionEventHandler LostConnectionEvent;
        public event TcpConnectionEventHandler ConnectionCloseEvent;
        public event TcpConnectionEventHandler SendDoneEvent;
        public event TcpConnectionEventHandler ReceiveHeaderFailEvent;
        public event TcpConnectionEventHandler ReceivingAsyncStartEvent;
        public event TcpConnectionEventHandler ReceivingAsyncStopEvent;
        public event TcpConnectionReceiveDoneHandler ReceiveDoneEvent;
        public event TcpConnectionReceiveObjectDoneHandler ReceiveObjectDoneEvent;
        public TcpConnection()
        {
            bufferSize = 1024000;
            buffer = new byte[bufferSize];
            packager = new Packager();
            receiving = false;
            client = new TcpClient();
            receivedData = null;
            remainReceiveLength = 0;
            connected = false;
        }
        public TcpConnection(TcpClient cli)
        {
            bufferSize = 1024000;
            buffer = new byte[bufferSize];
            packager = new Packager();
            receiving = false;
            client = cli;
            receivedData = null;
            remainReceiveLength = 0;
            connected = cli.Connected;
        }
        public TcpConnection(byte[] header)
        {
            bufferSize = 1024000;
            buffer = new byte[bufferSize];
            packager = new Packager(header);
            receiving = false;
            client = new TcpClient();
            receivedData = null;
            remainReceiveLength = 0;
            connected = false;
        }
        public TcpConnection(TcpClient cli, byte[] header)
        {
            bufferSize = 1024000;
            buffer = new byte[bufferSize];
            packager = new Packager(header);
            receiving = false;
            client = cli;
            receivedData = null;
            remainReceiveLength = 0;
            connected = cli.Connected;
        }
        public TcpConnection(Packager pk)
        {
            bufferSize = 1024000;
            buffer = new byte[bufferSize];
            packager = new Packager(pk);
            receiving = false;
            client = new TcpClient();
            receivedData = null;
            remainReceiveLength = 0;
            connected = false;
        }
        public TcpConnection(TcpClient cli, Packager pk)
        {
            bufferSize = 1024000;
            buffer = new byte[bufferSize];
            packager = new Packager(pk);
            receiving = false;
            client = cli;
            receivedData = null;
            remainReceiveLength = 0;
            connected = cli.Connected;
        }
        public TcpClient Client
        {
            get{ return client; }
        }
        public bool Connected
        {
            get { return connected; }
        }
        public Packager Packager
        {
            set { packager = value; }
        }
        public bool Receiving
        {
            get { return receiving; }
        }
        public async Task Connect(string ip, int port)
        {
            try
            {
                await client.ConnectAsync(ip, port);
                connected = true;
                ConnectDoneEvent?.Invoke(this);
            }
            catch(SocketException)
            {
                ConnectFailedEvent?.Invoke(this);
            }
        }
        public int BufferSize
        {
            get { return bufferSize; }
            set
            {
                if(!connected)
                {
                    bufferSize = value;
                    buffer = new byte[bufferSize];
                }
                    
            }
        }
        public async Task Send(object message)
        {
            byte[] obj = packager.Pack(message);
            NetworkStream ns = client.GetStream();
            await ns.WriteAsync(obj, 0, obj.Length);
            SendDoneEvent?.Invoke(this);
        }
        public void Close()
        {
            ConnectionCloseEvent?.Invoke(this);
            receiving = false;
            connected = false;
            
            client.Close();
        }
        public bool StartReceivingAsync()
        {
            if (receiving == false)
            {
                receiving = true;
                ReceivingAsyncStartEvent?.Invoke(this);
                ReceivingAsync();
                return true;
            }
            else
                return false;
        }
        protected async void ReceivingAsync()
        {
            while(receiving)
            {
                try
                {
                    await ReceiveAsync();
                }
                catch(Exception)
                {
                    //断开连接
                    LostConnectionEvent?.Invoke(this);
                    StopReceivingAsync();
                    Close();
                }
            }
        }
        public void StopReceivingAsync()
        {
            ReceivingAsyncStopEvent?.Invoke(this);
            receiving = false;
        }
        public async Task<object> ReceiveOnceAsync()
        {
            NetworkStream netstream = client.GetStream();
            int receivedNum;
            //header
            try
            {
                receivedNum = await netstream.ReadAsync(buffer, 0, packager.Length + sizeof(int));
            }
            catch (SocketException se)
            {
                ReceivingAsyncStopEvent?.Invoke(this);
                throw se;
            }
            if (receivedNum == 0)
            {
                //Lost connection
                LostConnectionEvent?.Invoke(this);
                ReceivingAsyncStopEvent?.Invoke(this);
                receiving = false;
                return null;
            }
            if (receivedNum < packager.Length + sizeof(int))
            {
                ReceiveHeaderFailEvent?.Invoke(this);
                ReceivingAsyncStopEvent?.Invoke(this);
                receiving = false;
                return null;
            }
            byte[] tem = new byte[packager.Length + sizeof(int)];
            Array.Copy(buffer, 0, tem, 0, packager.Length + sizeof(int));
            if (!packager.Check(tem))
            {
                ReceiveHeaderFailEvent?.Invoke(this);
                receiving = false;
                return null;
            }
            byte[] len = new byte[4];
            Array.Copy(tem, packager.Length, len, 0, sizeof(int));
            //header end

            //data
            int length = ByteConverter.Byte2Int(len);
            remainReceiveLength = length;
            int readsize = remainReceiveLength < bufferSize ? remainReceiveLength : bufferSize;
            while (remainReceiveLength > 0)
            {
                Console.Out.WriteLine(remainReceiveLength);
                try
                {
                    receivedNum = await netstream.ReadAsync(buffer, 0, readsize);
                }
                catch (SocketException se)
                {
                    ReceivingAsyncStopEvent?.Invoke(this);
                    receiving = false;
                    throw se;
                }
                if (receivedNum == 0)
                {
                    //Lost connection
                    LostConnectionEvent?.Invoke(this);
                    ReceivingAsyncStopEvent?.Invoke(this);
                    receiving = false;
                    return null;
                }
                remainReceiveLength -= receivedNum;
                tem = new byte[receivedNum];
                Array.Copy(buffer, tem, receivedNum);
                if (receivedData == null)
                {
                    receivedData = new byte[receivedNum];
                    Array.Copy(tem, 0, receivedData, 0, receivedNum);
                }
                else
                {
                    receivedData = receivedData.Concat(tem).ToArray();
                }
                readsize = remainReceiveLength < bufferSize ? remainReceiveLength : bufferSize;
            }
            object obj = packager.UnPack(receivedData);
            ReceiveObjectDoneEvent?.Invoke(this, obj);
            ReceiveDoneEvent?.Invoke(this, receivedData);
            receivedData = null;
            return obj;
        }
        protected async Task ReceiveAsync()
        {
            NetworkStream netstream = client.GetStream();
            int receivedNum;
            try
            {
                receivedNum = await netstream.ReadAsync(buffer, 0, packager.Length + sizeof(int));
            }
            catch(SocketException se)
            {
                ReceivingAsyncStopEvent?.Invoke(this);
                throw se;
            }
            if(receivedNum == 0)
            {
                //Lost connection
                LostConnectionEvent?.Invoke(this);
                ReceivingAsyncStopEvent?.Invoke(this);
                receiving = false;
                return;
            }
            if (receivedNum < packager.Length + sizeof(int))
            {
                ReceiveHeaderFailEvent?.Invoke(this);
                ReceivingAsyncStopEvent?.Invoke(this);
                receiving = false;
                return;
            }
            byte[] tem = new byte[packager.Length + sizeof(int)];
            Array.Copy(buffer, 0, tem, 0, packager.Length + sizeof(int));
            if (!packager.Check(tem))
            {
                ReceiveHeaderFailEvent?.Invoke(this);
                receiving = false;
                return;
            }
            byte[] len = new byte[4];
            Array.Copy(tem, packager.Length, len, 0, sizeof(int));
            int length = ByteConverter.Byte2Int(len);
            remainReceiveLength = length;
            int readsize = remainReceiveLength < bufferSize ? remainReceiveLength : bufferSize;
            while(remainReceiveLength > 0)
            {
                try
                {
                    receivedNum = await netstream.ReadAsync(buffer, 0, readsize);
                }
                catch(SocketException se)
                {
                    ReceivingAsyncStopEvent?.Invoke(this);
                    receiving = false;
                    throw se;
                }
                if(receivedNum == 0)
                {
                    //Lost connection
                    LostConnectionEvent?.Invoke(this);
                    ReceivingAsyncStopEvent?.Invoke(this);
                    receiving = false;
                    return;
                }
                remainReceiveLength -= receivedNum;
                tem = new byte[receivedNum];
                Array.Copy(buffer, tem, receivedNum);
                if (receivedData == null)
                {
                    receivedData = new byte[receivedNum];
                    Array.Copy(tem, 0, receivedData, 0, receivedNum);
                }
                else
                {
                    receivedData = receivedData.Concat(tem).ToArray();
                }
                readsize = remainReceiveLength < bufferSize ? remainReceiveLength : bufferSize;
            }
            ReceiveObjectDoneEvent?.Invoke(this, packager.UnPack(receivedData));
            ReceiveDoneEvent?.Invoke(this, receivedData);
            receivedData = null;
        }
        public override string ToString()
        {
            if (!connected)
                return "unconnected";
            return (((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString() + ":" + ((IPEndPoint)client.Client.RemoteEndPoint).Port.ToString());
        }
    }
}
