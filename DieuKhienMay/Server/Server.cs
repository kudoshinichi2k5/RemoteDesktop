﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Data;
using System.Drawing.Imaging;
using System.Timers;
using System.Data.SqlClient;

namespace Server
{
    public partial class Server : Form
    {
        // KHAI BAO HERE
        private TcpListener listener;
        private TcpClient client;
        private NetworkStream stream;
        private bool isConnected = false;
        private SqlConnection sqlConnection;
        private System.Timers.Timer timer;
        private bool isListening = false;
        // Chuỗi kết nối đến cơ sở dữ liệu
        private string connectionString = "Server=your_server;Database=RemoteDesktopDB;User Id=your_user;Password=your_password;";

        public Server()
        {
            InitializeComponent();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            StopListening();
        }

        private void btnListen_Click(object sender, EventArgs e)
        {
            StartListening();
        }

        /// <summary>
        /// Xu li ket noi
        /// </summary>
        // Bắt đầu lắng nghe từ client (sd bat dong bo)
        private void StartListening()
        {
            StopListening();  // Dọn dẹp mọi kết nối cũ
            int port;
            if (!int.TryParse(txbPort.Text, out port))
            {
                MessageBox.Show("Port không hợp lệ. Vui lòng kiểm tra lại.");
                return;
            }
            try
            {
                listener = new TcpListener(IPAddress.Parse(txbIP.Text), port);
                listener.Start();
                isListening = true;
                UpdateStatus("Đang lắng nghe...");

                Task.Run(() =>
                {
                    try
                    {
                        if (!isListening) return;  // Kiểm tra nếu server đã dừng

                        client = listener.AcceptTcpClient(); // Chấp nhận kết nối từ client
                        stream = client.GetStream();
                        isConnected = true;
                        UpdateStatus("Đã kết nối với client.");

                        var clientEndPoint = (IPEndPoint)client.Client.RemoteEndPoint;
                        LogConnection("Connected", clientEndPoint.Address.ToString(), clientEndPoint.Port);

                        // Xử lý dữ liệu từ client
                        //HandleClientInput();
                    }
                    catch (Exception ex)
                    {
                        if (isListening)  // Chỉ báo lỗi nếu server đang lắng nghe
                            MessageBox.Show($"Lỗi khi chấp nhận kết nối từ client: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi bắt đầu lắng nghe: {ex.Message}");
                isConnected = false;
            }

            // Ghi lại thông tin kết nối
            /*LogConnection("Connected", ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString(),
                ((IPEndPoint)client.Client.RemoteEndPoint).Port);*/
        }



        // Ket thuc ket noi
        private void StopListening()
        {
            timer?.Stop();  // Dừng timer nếu đang chạy

            if (stream != null)
            {
                stream.Close();  // Đóng stream
                stream = null;
            }

            if (client != null)
            {
                client.Close();  // Đóng kết nối client
                client = null;
            }

            if (listener != null)
            {
                listener.Stop();  // Dừng lắng nghe
                listener = null;
            }

            isListening = false;  // Đặt lại trạng thái lắng nghe
            isConnected = false;  // Đặt lại trạng thái kết nối
            UpdateStatus("Đã dừng lắng nghe.");
        }


        /// <summary>
        /// Nhận thông tin điều khiển từ client
        /// </summary>
        private void HandleClientInput()
        {
            try
            {
                // Xử lí gói tin nhận được từ client
                while (client.Connected)
                {
                    byte[] headerBytesRecv = new byte[6];
                    stream.Read(headerBytesRecv, 0, headerBytesRecv.Length);

                    // Phân loại dữ liệu theo header

                    // 0: dữ liệu ảnh, 1: dữ liệu input
                    int dataType = BitConverter.ToInt32(headerBytesRecv, 0);
                    // độ dài dữ liệu input
                    int dataLength = BitConverter.ToInt32(headerBytesRecv, 2);

                    if (dataType == 0)   // client yêu cầu gửi ảnh màn hình từ server
                    {
                        if (isConnected)
                        {
                            timer = new System.Timers.Timer(100);
                            timer.Elapsed += (sender, e) => SendImage();
                            timer.Start();
                        }
                    }
                    else if (dataType == 1) // xử lí dữ liệu input từ client
                    {
                        byte[] dataBytesRecv = new byte[dataLength];
                        stream.Read(dataBytesRecv, 0, dataLength);
                        HandleInputBytes(dataBytesRecv);
                    }
                    else if (dataType == 2)  //yeu cau xem log tu server
                    {
                        DataTable logs = LoadLogs();
                        //SendLogs(logs);
                    }
                    else
                    {
                        // Kết nối thất bại
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Exception: {ex.Message}");
            }
        }


        /// <summary>
        /// Xu li du lieu 
        /// </summary>
        /// <param name="inputBytes"></param>
        private void HandleInputBytes(byte[] inputBytes)
        {
            // code something here
        }


        /// <summary>
        /// Gui anh
        /// </summary>
        private void SendImage()
        {
            try
            {
                // Chụp ảnh màn hình với độ phân giải và chất lượng giảm
                Bitmap screenshot = CaptureScreen(0.5f, 40L); // Scale ảnh xuống 50% và chất lượng JPEG 40%
                using (MemoryStream ms = new MemoryStream())
                {
                    screenshot.Save(ms, ImageFormat.Jpeg);
                    byte[] imageData = ms.ToArray();

                    byte[] header = BitConverter.GetBytes((ushort)0); // 0 là mã loại dữ liệu ảnh
                    byte[] length = BitConverter.GetBytes(imageData.Length);

                    stream.Write(header, 0, header.Length);
                    stream.Write(length, 0, length.Length);
                    stream.Write(imageData, 0, imageData.Length);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi gửi ảnh: {ex.Message}");
            }
        }

        // Hàm chụp ảnh với độ phân giải thấp và chất lượng JPEG giảm
        private Bitmap CaptureScreen(float scaleFactor = 0.5f, long quality = 50L)
        {
            // Xác định kích thước ảnh với scaleFactor để giảm độ phân giải
            Rectangle screenSize = Screen.PrimaryScreen.Bounds;
            int scaledWidth = (int)(screenSize.Width * scaleFactor);
            int scaledHeight = (int)(screenSize.Height * scaleFactor);

            Bitmap bmp = new Bitmap(scaledWidth, scaledHeight);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

                // Chụp màn hình với kích thước nhỏ hơn
                g.CopyFromScreen(0, 0, 0, 0, new Size(scaledWidth, scaledHeight));
            }

            // Giảm chất lượng ảnh khi lưu JPEG
            return CompressImage(bmp, quality);
        }

        // Hàm nén ảnh bằng cách giảm chất lượng JPEG
        private Bitmap CompressImage(Bitmap bmp, long quality)
        {
            ImageCodecInfo jpegCodec = ImageCodecInfo.GetImageDecoders()
                                                     .First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);
            EncoderParameters encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);

            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, jpegCodec, encoderParams);
                return new Bitmap(ms);
            }
        }


        /// <summary>
        /// Cap nhat trang thai, được gọi bởi StartListening(), StopListening(),...
        /// </summary>
        /// <param name="message"></param>
        private void UpdateStatus(string message)
        {
            if (txbStatus.InvokeRequired)
            {
                txbStatus.Invoke(new Action(() => txbStatus.Text = message));
            }
            else
            {
                txbStatus.Text = message;
            }
        }


        /// <summary>
        /// View logs
        /// </summary>

        // Hàm khởi tạo kết nối csdl
        private SqlConnection InitializeDatabase()
        {
            SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            return connection;
        }

        // ghi lại thông tin kết nối
        private void LogConnection(string status, string ip, int port)
        {
            using (SqlConnection connection = InitializeDatabase())
            {
                string query = "INSERT INTO ConnectionLogs (Status, IP, Port, Timestamp) VALUES (@Status, @IP, @Port, @Timestamp)";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Status", status);
                    command.Parameters.AddWithValue("@IP", ip);
                    command.Parameters.AddWithValue("@Port", port);
                    command.Parameters.AddWithValue("@Timestamp", DateTime.Now);
                    command.ExecuteNonQuery();
                }
            }
        }

        // Truy cap co so du lieu để lấy lịch sử kết nối
        private DataTable LoadLogs()
        {
            DataTable logs = new DataTable();
            using (SqlConnection connection = InitializeDatabase())
            {
                string query = "SELECT * FROM ConnectionLogs ORDER BY Timestamp DESC";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(logs);
                    }
                }
            }
            return logs;
        }

        // Gui logs cho client
        private void SendLogs()
        {
            DataTable logs = LoadLogs();
            StringBuilder sb = new StringBuilder();

            foreach (DataRow row in logs.Rows)
            {
                sb.AppendLine($"{row["Timestamp"]} - {row["Status"]} - {row["IP"]}:{row["Port"]}");
            }

            byte[] logData = Encoding.UTF8.GetBytes(sb.ToString());
            byte[] header = BitConverter.GetBytes((ushort)2); // Giả định 2 là loại dữ liệu log
            byte[] length = BitConverter.GetBytes(logData.Length);

            stream.Write(header, 0, header.Length);
            stream.Write(length, 0, length.Length);
            stream.Write(logData, 0, logData.Length);
        }



        /// <summary>
        /// gui file
        /// </summary>

    }
}
