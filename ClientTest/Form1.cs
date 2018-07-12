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
namespace ClientTest
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        public void PrintLine(string str)
        {
            Action<string> actiondelegate = (x) => { richTextBox1.AppendText(x + "\n"); };
            richTextBox1.BeginInvoke(actiondelegate, str);
        }
        TcpConnection connection;
        private void ConDone(TcpConnection con)
        {
            PrintLine(string.Format("连接 {0} 成功", con.ToString()));
        }
        private void ReceivedMessage(TcpConnection connection, object obj)
        {
            string m = obj as string;
            PrintLine(m);
        }
        private async void button1_Click(object sender, EventArgs e)
        {
            string ip = textBox1.Text;
            string strport = textBox2.Text;
            int port = Convert.ToInt32(strport);
            connection = new TcpConnection();
            connection.ConnectDoneEvent += ConDone;
            connection.ReceiveObjectDoneEvent += ReceivedMessage;
            await connection.Connect(ip, port);
            connection.StartReceivingAsync();
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            try
            {
                await connection.Send(textBox3.Text);
            }
            catch (Exception)
            {
                PrintLine("发送失败，服务器可能已关闭");
            }
        }
    }
}
