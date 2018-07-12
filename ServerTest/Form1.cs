using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Network_Core;
namespace ServerTest
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        TcpServer server;
        public void PrintLine(string str)
        {
            Action<string> actiondelegate = (x) => { richTextBox1.AppendText(x + "\n"); };
            richTextBox1.BeginInvoke(actiondelegate, str);
        }
        private void ReceivedMessage(TcpConnection connection, object obj)
        {
            string m = obj as string;
            string fm = string.Format("{0} : {1}", connection.ToString(), m);
            PrintLine(fm);
            server.Broadcast(fm);
        }
        private void LostConnection(TcpConnection connection)
        {
            PrintLine(string.Format("{0} 已断开", connection.ToString()));
            server.RemoveConnection(connection);
        }
        private void AcceptConnection(TcpConnection connection)
        {
            PrintLine(string.Format("{0} 已连接", connection.ToString()));
            connection.ReceiveObjectDoneEvent += ReceivedMessage;
            connection.LostConnectionEvent += LostConnection;
            connection.StartReceivingAsync();
        }
        private void button2_Click(object sender, EventArgs e)
        {
            string ip = textBox2.Text;
            string strport = textBox3.Text;
            int port = Convert.ToInt32(strport);
            server = new TcpServer(ip, port);
            server.AcceptConnectionEvent += AcceptConnection;
            server.StartListeningAsync();
            PrintLine("开始监听");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            server.Broadcast(String.Format("{0} : {1}", "server", textBox1.Text));
        }
    }
}
