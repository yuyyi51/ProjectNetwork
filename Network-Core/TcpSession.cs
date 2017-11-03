using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.IO;

namespace Network_Core
{
    public class TcpSession
    {
        public delegate void TcpSessionEventHandler(TcpSession sender);
        public delegate void TcpSessionReceiveDoneHandler(TcpSession sender, byte[] data);
        public delegate void TcpSessionReceiveObjectDoneHandler(TcpSession sender, object obj);
        
        protected TcpClient client;
        protected byte[] buffer;
        protected int bufferSize = 1024;
        protected byte[] receivedData;
        protected int remainReceiveLength;
        protected Packager packager;
        protected ManualResetEvent receiving;
        protected bool connected;

        public event TcpSessionEventHandler ConnectDoneEvent;
        public event TcpSessionEventHandler ConnectFailedEvent;
        public event TcpSessionEventHandler LostConnectionEvent;
        public event TcpSessionEventHandler ConnectionCloseEvent;
        public event TcpSessionEventHandler SendDoneEvent;
        public event TcpSessionEventHandler ReceiveHeaderFailEvent;
        public event TcpSessionReceiveDoneHandler ReceiveDoneEvent;
        public event TcpSessionReceiveObjectDoneHandler ReceiveObjectDoneEvent;
        public TcpSession()
        {
            buffer = new byte[bufferSize];
            packager = new Packager();
            receiving = new ManualResetEvent(false);
            client = null;
            receivedData = null;
            remainReceiveLength = 0;
            connected = false;
        }
        public TcpSession(TcpClient cli)
        {
            buffer = new byte[bufferSize];
            packager = new Packager();
            receiving = new ManualResetEvent(false);
            client = cli;
            receivedData = null;
            remainReceiveLength = 0;
            connected = cli.Connected;
        }
        public TcpSession(byte[] header)
        {
            buffer = new byte[bufferSize];
            packager = new Packager(header);
            receiving = new ManualResetEvent(false);
            client = null;
            receivedData = null;
            remainReceiveLength = 0;
            connected = false;
        }
        public TcpSession(TcpClient cli, byte[] header)
        {
            buffer = new byte[bufferSize];
            packager = new Packager(header);
            receiving = new ManualResetEvent(false);
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
        public ManualResetEvent Receiving
        {
            get { return receiving; }
        }
        public void Connect(string ip, int port)
        {
            client.BeginConnect(ip, port, ConnectCallback, client);
        }
        protected void ConnectCallback(IAsyncResult ar)
        {
            TcpClient t = ar.AsyncState as TcpClient;
            try
            {
                t.EndConnect(ar);
            }
            catch(SocketException)
            {
                //Fail to connect
                ConnectFailedEvent?.Invoke(this);
                return;
            }
            connected = true;
            ConnectDoneEvent?.Invoke(this);
        }
        public void Send(object message)
        {
            byte[] obj = packager.Pack(message);
            NetworkStream ns = client.GetStream();
            ns.BeginWrite(obj, 0, obj.Length, SendCallback, ns);
        }
        protected void SendCallback(IAsyncResult ar)
        {
            NetworkStream ns = ar.AsyncState as NetworkStream;
            try
            {
                ns.EndWrite(ar);
            }
            catch(SocketException se)
            {

                throw se;
            }
            SendDoneEvent?.Invoke(this);
        }
        public void ReceiveAndWait()
        {
            Receive();
            receiving.WaitOne();
        }
        public void Receive()
        {
            NetworkStream netstream = client.GetStream();
            receiving.Reset();
            netstream.BeginRead(buffer, 0, packager.Length + sizeof(int), ReceiveHeader, netstream);
        }
        protected void ReceiveHeader(IAsyncResult ar)
        {
            NetworkStream netstream = ar.AsyncState as NetworkStream;
            int receivedNum;
            try
            {
                receivedNum = netstream.EndRead(ar);
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
            if(receivedNum < packager.Length+sizeof(int))
            {
                ReceiveHeaderFailEvent?.Invoke(this);
                return;
            }
            byte[] tem = new byte[packager.Length + sizeof(int)];
            Array.Copy(buffer, 0, tem, 0, packager.Length + sizeof(int));
            if(!packager.Check(tem))
            {
                ReceiveHeaderFailEvent?.Invoke(this);
                return;
            }
            byte[] len = new byte[4];
            Array.Copy(tem, packager.Length, len, 0, sizeof(int));
            int length = ByteConverter.Byte2Int(len);
            remainReceiveLength = length;
            int readsize = remainReceiveLength < bufferSize ? remainReceiveLength : bufferSize;
            netstream.BeginRead(buffer, 0, readsize, ReceiveCallback, netstream);
        }
        protected void ReceiveCallback(IAsyncResult ar)
        {
            NetworkStream netstream = ar.AsyncState as NetworkStream;
            int receivedNum;
            try
            {
                receivedNum = netstream.EndRead(ar);
            }
            catch (SocketException se)
            {

                throw se;
            }
            if (receivedNum == 0)
            {
                //Lost connection
                connected = false;
                LostConnectionEvent?.Invoke(this);
                return;
            }
            remainReceiveLength -= receivedNum;
            byte[] tem = new byte[receivedNum];
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
            if(remainReceiveLength > 0)
            {
                int readsize = remainReceiveLength < bufferSize ? remainReceiveLength : bufferSize;
                netstream.BeginRead(buffer, 0, readsize, ReceiveCallback, netstream);
            }
            else
            {
                ReceiveObjectDoneEvent?.Invoke(this, packager.UnPack(receivedData));
                ReceiveDoneEvent?.Invoke(this,receivedData);
                receivedData = null;
            }
        }
        public override string ToString()
        {
            if (!connected)
                return "unconnected";
            return (((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString() + ":" + ((IPEndPoint)client.Client.RemoteEndPoint).Port.ToString());
        }
    }
}
