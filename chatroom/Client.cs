using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace chatroom
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private TcpClient tcpClient;
        private Thread clientThread;
        private bool connecting = true;
        private delegate void SafeCallDelegate(string username, string data);

        private void UpdateChatHistorySafeCall(string username, string data)
        {
            if (flowLayoutPanel1.InvokeRequired)
            {
                var method = new SafeCallDelegate(UpdateChatHistorySafeCall);
                flowLayoutPanel1.Invoke(method, new object[] { username, data });
            }
            else
            {
                Label label = new Label
                {
                    Text = username == null ? data : $"{username}: {data}",
                    AutoSize = true
                };
                flowLayoutPanel1.Controls.Add(label);
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                IPEndPoint tcpServer = new IPEndPoint(IPAddress.Parse(txtServerIP.Text), int.Parse(txtServerPort.Text));
                tcpClient = new TcpClient();
                tcpClient.Connect(tcpServer);

                // Send username to server
                NetworkStream net_stream = tcpClient.GetStream();
                byte[] message = Encoding.UTF8.GetBytes(txtUsername.Text);
                net_stream.Write(message, 0, message.Length);
                net_stream.Flush();

                // Start client thread
                clientThread = new Thread(Receive);
                clientThread.IsBackground = true;
                clientThread.Start();
                txtServerIP.ReadOnly = txtServerPort.ReadOnly = txtUsername.ReadOnly = true;
            }
            catch
            {
                MessageBox.Show("Connection failed!");
            }
        }

        private Image selectedImage;

        private void btnSelectImage_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Images|*.jpg;*.png;*.bmp";
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                selectedImage = Image.FromFile(openFileDialog.FileName);
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            NetworkStream net_stream = tcpClient.GetStream();
            if (selectedImage != null)
            {
                MemoryStream ms = new MemoryStream();
                selectedImage.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                byte[] imageBytes = ms.ToArray();
                net_stream.Write(imageBytes, 0, imageBytes.Length);
                PictureBox pictureBox = new PictureBox
                {
                    Image = selectedImage,
                    SizeMode = PictureBoxSizeMode.AutoSize
                };
                flowLayoutPanel1.Controls.Add(pictureBox);
                selectedImage = null;
            }
            else
            {
                byte[] message = Encoding.UTF8.GetBytes(txtMessage.Text);
                net_stream.Write(message, 0, message.Length);
                UpdateChatHistorySafeCall("Me", txtMessage.Text);
            }
            net_stream.Flush();
            txtMessage.Text = string.Empty;
        }

        private void Receive()
        {
            NetworkStream net_stream = tcpClient.GetStream();
            byte[] data = new byte[1024];
            try
            {
                while (connecting && tcpClient.Connected)
                {
                    int byte_count = net_stream.Read(data, 0, data.Length);
                    if (IsImageData(data, byte_count))
                    {
                        MemoryStream ms = new MemoryStream(data, 0, byte_count);
                        Image image = Image.FromStream(ms);
                        PictureBox pictureBox = new PictureBox
                        {
                            Image = image,
                            SizeMode = PictureBoxSizeMode.AutoSize
                        };
                        flowLayoutPanel1.Controls.Add(pictureBox);
                    }
                    else
                    {
                        string message = Encoding.UTF8.GetString(data, 0, byte_count);
                        UpdateChatHistorySafeCall(null, message);
                    }
                    if (byte_count == 0)
                    {
                        connecting = false;
                    }
                }
            }
            catch
            {
                tcpClient.Close();
            }
        }

        private bool IsImageData(byte[] data, int byteCount)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream(data, 0, byteCount))
                {
                    Image image = Image.FromStream(ms);
                }
                return true;
            }
            catch (ArgumentException)
            {
                // The data cannot be converted into an image
                return false;
            }
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            clientThread = null;
            tcpClient.Close();
            UpdateChatHistorySafeCall(null, "Disconnected now...");
        }
    }
}

