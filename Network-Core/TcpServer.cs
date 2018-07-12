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

        public bool StartListeningAsync()
        {
            if (listening == true)
                return false;
            listening = true;
            ListeningAsync();
            return true;
        }
        protected async void ListeningAsync()
        {
            listener.Start();
            while(listening)
            {
                TcpConnection tc = new TcpConnection(await listener.AcceptTcpClientAsync(), packager);
                connectionList.Add(tc);
                AcceptConnectionEvent?.Invoke(tc);
            }
            listener.Stop();
        }
        public void StopListeningAsync()
        {
            listening = false;
        }
        public void RemoveConnection(TcpConnection tc)
        {
            connectionList.Remove(tc);
        }
        public async void Send(TcpConnection tc,object obj)
        {
            await tc.Send(obj);
        }
        public void Broadcast(object obj)
        {
            foreach(TcpConnection tc in connectionList)
            {
                Send(tc, obj);
            }
        }
    }
}
