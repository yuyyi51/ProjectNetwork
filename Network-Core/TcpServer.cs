using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;

namespace Network_Core
{
    public class TcpServer
    {
        public delegate void AcceptConnectionHandler(TcpConnection tcon);
        protected List<TcpConnection> connectionList;
        protected Thread listeningThread;
        protected bool listening;
        protected TcpListener listener;
        protected Packager packager;

        public event AcceptConnectionHandler AcceptConnectionEvent;

        public List<TcpConnection> ConnectionList
        {
            get { return connectionList; }
        }

        public TcpServer(string ip, int port)
        {
            connectionList = new List<TcpConnection>();
            listener = new TcpListener(IPAddress.Parse(ip), port);
            listening = false;
            packager = new Packager();
        }

        public bool StartListening()
        {
            if (listeningThread != null)
                return false;
            listeningThread = new Thread(ListeningThread);
            listeningThread.IsBackground = true;
            listeningThread.Start();
            return true;
        }
        protected void ListeningThread()
        {
            listener.Start();
            while(listening)
            {
                if(listener.Pending())
                {
                    TcpConnection tc = new TcpConnection(listener.AcceptTcpClient(), packager);
                    connectionList.Add(tc);
                    AcceptConnectionEvent?.Invoke(tc);
                }
            }
            listener.Stop();
        }
        public void StopListening()
        {
            listening = false;
            listeningThread = null;
        }
        public void Send(TcpConnection tc,object obj)
        {
            tc.Send(obj);
        }
        public void Broadcast(object obj)
        {
            foreach(TcpConnection tc in connectionList)
            {
                tc.Send(obj);
            }
        }
    }
}
