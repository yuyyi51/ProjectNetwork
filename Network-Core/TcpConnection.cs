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
        protected ManualResetEvent receiving;
        protected bool connected;
        protected Thread receivingThread;

        public event TcpConnectionEventHandler ConnectDoneEvent;
        public event TcpConnectionEventHandler ConnectFailedEvent;
        public event TcpConnectionEventHandler LostConnectionEvent;
        public event TcpConnectionEventHandler ConnectionCloseEvent;
        public event TcpConnectionEventHandler SendDoneEvent;
        public event TcpConnectionEventHandler ReceiveHeaderFailEvent;
        public event TcpConnectionEventHandler ReceivingThreadStartEvent;
        public event TcpConnectionEventHandler ReceivingThreadStopEvent;
        public event TcpConnectionReceiveDoneHandler ReceiveDoneEvent;
        public event TcpConnectionReceiveObjectDoneHandler ReceiveObjectDoneEvent;
        public TcpConnection()
        {
            bufferSize = 1024;
            buffer = new byte[bufferSize];
            packager = new Packager();
            receiving = new ManualResetEvent(false);
            client = new TcpClient();
            receivedData = null;
            remainReceiveLength = 0;
            connected = false;
        }
        public TcpConnection(TcpClient cli)
        {
            bufferSize = 1024;
            buffer = new byte[bufferSize];
            packager = new Packager();
            receiving = new ManualResetEvent(false);
            client = cli;
            receivedData = null;
            remainReceiveLength = 0;
            connected = cli.Connected;
        }
        public TcpConnection(byte[] header)
        {
            buffer = new byte[bufferSize];
            packager = new Packager(header);
            receiving = new ManualResetEvent(false);
            client = new TcpClient();
            receivedData = null;
            remainReceiveLength = 0;
            connected = false;
        }
        public TcpConnection(TcpClient cli, byte[] header)
        {
            buffer = new byte[bufferSize];
            packager = new Packager(header);
            receiving = new ManualResetEvent(false);
            client = cli;
            receivedData = null;
            remainReceiveLength = 0;
            connected = cli.Connected;
        }
        public TcpConnection(Packager pk)
        {
            buffer = new byte[bufferSize];
            packager = new Packager(pk);
            receiving = new ManualResetEvent(false);
            client = new TcpClient();
            receivedData = null;
            remainReceiveLength = 0;
            connected = false;
        }
        public TcpConnection(TcpClient cli, Packager pk)
        {
            buffer = new byte[bufferSize];
            packager = new Packager(pk);
            receiving = new ManualResetEvent(false);
            client = cli;
            receivedData = null;
            remainReceiveLength = 0;
            connected = false;
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
        public ManualResetEvent Receiving
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
            receivingThread?.Abort();
            receiving.Set();
            connected = false;
            ConnectionCloseEvent?.Invoke(this);
            client.Close();
        }
        public bool StartReceivingAsync()
        {
            ReceivingAsync();
            return true;
        }
        protected async void ReceivingAsync()
        {
            while(true)
            {
                await ReceiveAsync();
            }
        }
        public void StopReceiving()
        {
            
        }
        public async Task ReceiveAsync()
        {
            NetworkStream netstream = client.GetStream();
            receiving.Reset();
            int receivedNum;
            try
            {
                receivedNum = await netstream.ReadAsync(buffer, 0, packager.Length + sizeof(int));
            }
            catch(SocketException se)
            {
                throw se;
            }
            if(receivedNum == 0)
            {
                //Lost connection
                LostConnectionEvent?.Invoke(this);
                return;
            }
            if (receivedNum < packager.Length + sizeof(int))
            {
                ReceiveHeaderFailEvent?.Invoke(this);
                return;
            }
            byte[] tem = new byte[packager.Length + sizeof(int)];
            Array.Copy(buffer, 0, tem, 0, packager.Length + sizeof(int));
            if (!packager.Check(tem))
            {
                ReceiveHeaderFailEvent?.Invoke(this);
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
                    throw se;
                }
                if(receivedNum == 0)
                {
                    //Lost connection
                    LostConnectionEvent?.Invoke(this);
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
            receiving.Set();
        }
        public override string ToString()
        {
            if (!connected)
                return "unconnected";
            return (((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString() + ":" + ((IPEndPoint)client.Client.RemoteEndPoint).Port.ToString());
        }
    }
}
