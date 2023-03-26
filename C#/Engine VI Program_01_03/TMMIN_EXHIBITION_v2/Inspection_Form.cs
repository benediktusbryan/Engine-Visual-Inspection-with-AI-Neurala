using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Ports;
using System.Net.Sockets;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using System.Diagnostics;
using System.Security;
using System.Text.RegularExpressions;
using System.Net.Http;
using Basler.Pylon;
using Newtonsoft.Json;
using EasyModbus;
using System.Reflection.Emit;
using System.Data.SQLite;
using System.Xml;
using System.Data.Common;
using System.Data.Odbc;
//using System.Windows.Forms.AxHost;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using OmronFinsTCP.Net;
//using static System.Net.Mime.MediaTypeNames;


namespace TMMIN_EXHIBITION_v2
{
    public partial class Inspection_Form : Form
    {
        Image_Form Image_Form = new Image_Form();
        Camera cam;
        List<String> cam_id;
        List<String> cam_model;
        ModbusClient modbusClient;
        EtherNetPLC PLC = new EtherNetPLC();
        int flagCapture;
        int flagMove;
        public string barcodeEngineNumber = "-";
        string partName;
        string engineTR;
        string barcodeengineType = "-";
        Boolean statusAllNG = false;
        Boolean flagOverride = false;
        Boolean flagDoneReset = false;
        Boolean flagDoneAddColumn = false;
        Boolean robotError;
        Boolean robotRunning;
        Boolean robotPause;
        public Boolean flagManualBarcode;
        string ImageFilenameSwitchOli;
        string ImageFilenamePlugDrainCock;
        string ImageFilenameOilFilter;
        string ImageFilenameCoverExManifold;
        string ImageFilenameOCV = "";
        string ImageFilenameTestbenchStamp = "";
        string SwitchOliStatus = "";
        string PlugDrainCockStatus = "";
        string OilFilterStatus = "";
        string CoverExManifoldStatus = "";
        string OCVStatus = "";
        string TestbenchStampStatus = "";
        string JudgementStatus = "";
        SQLiteConnection DBSQLConnection = new SQLiteConnection();
        SQLiteDataAdapter DBSQLAdapter;
        DataSet ds;
        DataTable dt;
        int SelectedRowIndex;


        public Inspection_Form()
        {
            InitializeComponent();
        }
        private async void get_brain_API_Neurala()
        {

            try
            {
                HttpClient client = new HttpClient();
                HttpResponseMessage response = await client.GetAsync("http://localhost:9002/v1/brains");
                Console.WriteLine("Sent");
                string res = await response.Content.ReadAsStringAsync();
                Console.WriteLine(res);
            }
            catch (Exception ex)
            { }
        }
        private void Inspection_Form_Load(object sender, EventArgs e)
        {
            comboBox1.SelectedText = "Engine Number";
            comboBox1.Text = "Engine Number";
            DBSQLConnection = DBCreateConnection(DBSQLConnection, "Engine_VI_Database");

            get_brain_API_Neurala();

            short statusPLC;
            connectPLC();

            //while (true)
            //{
            //    try
            //    {
            //        connectPLC();
            //        PLC.ReadWord(PlcMemory.WR, 5, out statusPLC);
            //        if (statusPLC == 1)
            //        {
            //            label41.Text = "V";
            //            label41.ForeColor = Color.Lime;
            //            break;
            //        }
            //    }
            //    catch
            //    {

            //    }
            //}
            //PLC.ReadWord(PlcMemory.WR, 5, out statusPLC);
            //textBox19.Text = statusPLC.ToString();

            while (true)
            {
                textBox17.Text = "1";
                try
                {
                    connectRobot();
                    Console.WriteLine(modbusClient.Connected);
                    if (modbusClient.Connected)
                    {
                        label40.Text = "V";
                        label40.ForeColor = Color.Lime;
                        timer1.Enabled = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    reconnectRobot(ex.Message);
                }
            }            
            //scan_camera();
            //con_cam();
            textBox17.Text = "2";
        }

        private void connectPLC()       //connect FINS PLC
        {
            short PLCLinkStatus=0;
            try
            {
                PLCLinkStatus = PLC.Link("172.16.1.2", 9600, 100);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error Finding PLC, Err Code : {0}", ex.Message);
            }

            if (PLCLinkStatus == 0)
            {
                label41.Text = "V";
                label41.ForeColor = Color.Lime;                
            }
        }

        private SQLiteConnection DBCreateConnection(SQLiteConnection DBConnection, string databaseName)
        {
            

            // Create a new database connection:
            string DBFilename = @"URI=file:C:\" + databaseName + ".db";
            DBConnection = new SQLiteConnection(DBFilename);
            // Open the connection:
            try
            {
                DBConnection.Open();
                DBConnection.Close();
            }
            catch (Exception ex)
            {

            }
            Console.WriteLine("DB Connect");

            return DBConnection;
        }

        private void DBCreateTable(SQLiteConnection DBConnection)
        {
            SQLiteCommand DBCommand;
            string CreateTable = "CREATE TABLE Traceability_Table (Date DATE, Time VARCHAR, Engine_Number VARCHAR, Engine_Type VARCHAR," +
                "OCV_Status VARCHAR, OCV_Filename VARCHAR, " +
                "Testbench_Stamp_Status VARCHAR, Testbench_Stamp_Filename VARCHAR, " +
                "Switch_Oli_Status VARCHAR, Switch_Oli_Filename VARCHAR, " +
                "Plug_Drain_Cock_Status VARCHAR, Plug_Drain_Cock_Filename VARCHAR, " +
                "Oil_Filter_Status VARCHAR, Oil_Filter_Filename VARCHAR, " +
                "Cover_Ex_Manifold_Status VARCHAR, Cover_Ex_Manifold_Filename VARCHAR, " +
                "Judgement VARCHAR)";
            try
            {
                DBConnection.Open();
                DBCommand = DBConnection.CreateCommand();
                DBCommand.CommandText = "DROP TABLE IF EXISTS Traceability_Table";
                DBCommand.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
            }
            DBCommand = DBConnection.CreateCommand();
            DBCommand.CommandText = CreateTable;
            DBCommand.ExecuteNonQuery();
            DBConnection.Close();
            Console.WriteLine("DB Table Create");
        }

        private void DBInsertData(SQLiteConnection DBConnection)
        {
            SQLiteCommand DBCommand;
            DBConnection.Open();
            DBCommand = new SQLiteCommand(DBConnection);
            DBCommand.CommandText = "INSERT INTO Traceability_Table(Date, Time, Engine_Number, Engine_Type, " +
                "OCV_Status, OCV_Filename, " +
                "Testbench_Stamp_Status, Testbench_Stamp_Filename, " +
                "Switch_Oli_Status, Switch_Oli_Filename, " +
                "Plug_Drain_Cock_Status, Plug_Drain_Cock_Filename, " +
                "Oil_Filter_Status, Oil_Filter_Filename, " +
                "Cover_Ex_Manifold_Status, Cover_Ex_Manifold_Filename, " +
                "Judgement) " +

                "VALUES(@Date, @Time, @Engine_Number, @Engine_Type, " +
                "@OCV_Status, @OCV_Filename, " +
                "@Testbench_Stamp_Status, @Testbench_Stamp_Filename, " +
                "@Switch_Oli_Status, @Switch_Oli_Filename, " +
                "@Plug_Drain_Cock_Status, @Plug_Drain_Cock_Filename, " +
                "@Oil_Filter_Status, @Oil_Filter_Filename, " +
                "@Cover_Ex_Manifold_Status, @Cover_Ex_Manifold_Filename, " +
                "@Judgement)";

            DBCommand.Parameters.AddWithValue("@Date", DateTime.Now.ToString("yyyy-MM-dd"));
            DBCommand.Parameters.AddWithValue("@Time", DateTime.Now.ToString("HH:mm:ss"));
            DBCommand.Parameters.AddWithValue("@Engine_Number", barcodeEngineNumber);
            DBCommand.Parameters.AddWithValue("@Engine_Type", barcodeengineType);
            DBCommand.Parameters.AddWithValue("@OCV_Status", OCVStatus);
            DBCommand.Parameters.AddWithValue("@OCV_Filename", ImageFilenameOCV);
            DBCommand.Parameters.AddWithValue("@Testbench_Stamp_Status", TestbenchStampStatus);
            DBCommand.Parameters.AddWithValue("@Testbench_Stamp_Filename", ImageFilenameTestbenchStamp);
            DBCommand.Parameters.AddWithValue("@Switch_Oli_Status", SwitchOliStatus);
            DBCommand.Parameters.AddWithValue("@Switch_Oli_Filename", ImageFilenameSwitchOli);
            DBCommand.Parameters.AddWithValue("@Plug_Drain_Cock_Status", PlugDrainCockStatus);
            DBCommand.Parameters.AddWithValue("@Plug_Drain_Cock_Filename", ImageFilenamePlugDrainCock);
            DBCommand.Parameters.AddWithValue("@Oil_Filter_Status", OilFilterStatus);
            DBCommand.Parameters.AddWithValue("@Oil_Filter_Filename", ImageFilenameOilFilter);
            DBCommand.Parameters.AddWithValue("@Cover_Ex_Manifold_Status", CoverExManifoldStatus);
            DBCommand.Parameters.AddWithValue("@Cover_Ex_Manifold_Filename", ImageFilenameCoverExManifold);
            DBCommand.Parameters.AddWithValue("@Judgement", JudgementStatus);
            DBCommand.Prepare();

            DBCommand.ExecuteNonQuery();
            DBConnection.Close();
            Console.WriteLine("DB Insert");
        }

        private void DBReadData(SQLiteConnection DBConnection)
        {
            string sql = "SELECT * FROM Traceability_Table ORDER BY Date DESC, Time DESC";
            DBConnection.Open();
            SQLiteCommand DBCommand = new SQLiteCommand(sql, DBConnection);
            DBSQLAdapter = new SQLiteDataAdapter(DBCommand);

            ds = new DataSet();
            DBSQLAdapter.Fill(ds, "Traceability_Table");
            dt = ds.Tables["Traceability_Table"];
            DBConnection.Close();

            dataGridView1.DataSource = ds.Tables["Traceability_Table"];
            dataGridView1.ReadOnly = true;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            //if (!flagDoneAddColumn)
            //{
            //    DataGridViewButtonColumn buttonImage1 = new DataGridViewButtonColumn();
            //    dataGridView1.Columns.Add(buttonImage1);
            //    buttonImage1.HeaderText = "Switch Oli Image";
            //    buttonImage1.Text = "View Image";
            //    buttonImage1.Name = "buttonImage1";
            //    buttonImage1.UseColumnTextForButtonValue = true;

            //    DataGridViewButtonColumn buttonImage2 = new DataGridViewButtonColumn();
            //    dataGridView1.Columns.Add(buttonImage2);
            //    buttonImage2.HeaderText = "Plug Drain Cock Image";
            //    buttonImage2.Text = "View Image";
            //    buttonImage2.Name = "buttonImage2";
            //    buttonImage2.UseColumnTextForButtonValue = true;

            //    DataGridViewButtonColumn buttonImage3 = new DataGridViewButtonColumn();
            //    dataGridView1.Columns.Add(buttonImage3);
            //    buttonImage3.HeaderText = "Oil Filter Image";
            //    buttonImage3.Text = "View Image";
            //    buttonImage3.Name = "buttonImage3";
            //    buttonImage3.UseColumnTextForButtonValue = true;

            //    DataGridViewButtonColumn buttonImage4 = new DataGridViewButtonColumn();
            //    dataGridView1.Columns.Add(buttonImage4);
            //    buttonImage4.HeaderText = "Cover Ex Manifold Image";
            //    buttonImage4.Text = "View Image";
            //    buttonImage4.Name = "buttonImage4";
            //    buttonImage4.UseColumnTextForButtonValue = true;

            //    flagDoneAddColumn = true;
            //}
            

            Console.WriteLine("DB Read");
        }

        private void DBFilterData(SQLiteConnection DBConnection)
        {
            string sql = "";

            if (comboBox1.Text == "Engine Number")
            {
                sql = "SELECT * FROM Traceability_Table WHERE Engine_Number LIKE '%" + textBox4.Text + "%' ORDER BY Date DESC, Time DESC ";
            }
            else if (comboBox1.Text == "Engine Type")
            {
                sql = "SELECT * FROM Traceability_Table WHERE Engine_Type LIKE '%" + textBox4.Text + "%' ORDER BY Date DESC, Time DESC ";
            }
            else if (comboBox1.Text == "Judgement")
            {
                sql = "SELECT * FROM Traceability_Table WHERE Judgement LIKE '%" + textBox4.Text + "%' ORDER BY Date DESC, Time DESC ";
            }
            else if (comboBox1.Text == "OCV Status")
            {
                sql = "SELECT * FROM Traceability_Table WHERE OCV_Status LIKE '%" + textBox4.Text + "%' ORDER BY Date DESC, Time DESC ";
            }
            else if (comboBox1.Text == "Testbench Stamp Status")
            {
                sql = "SELECT * FROM Traceability_Table WHERE Testbench_Stamp_Status LIKE '%" + textBox4.Text + "%' ORDER BY Date DESC, Time DESC ";
            }
            else if (comboBox1.Text == "Switch Oli Status")
            {
                sql = "SELECT * FROM Traceability_Table WHERE Switch_Oli_Status LIKE '%" + textBox4.Text + "%' ORDER BY Date DESC, Time DESC ";
            }
            else if (comboBox1.Text == "Plug Drain Cock Status")
            {
                sql = "SELECT * FROM Traceability_Table WHERE Plug_Drain_Cock_Status LIKE '%" + textBox4.Text + "%' ORDER BY Date DESC, Time DESC ";
            }
            else if (comboBox1.Text == "Oil Filter Status")
            {
                sql = "SELECT * FROM Traceability_Table WHERE Oil_Filter_Status LIKE '%" + textBox4.Text + "%' ORDER BY Date DESC, Time DESC ";
            }
            else if (comboBox1.Text == "Cover Ex Manifold Status")
            {
                sql = "SELECT * FROM Traceability_Table WHERE Cover_Ex_Manifold_Status LIKE '%" + textBox4.Text + "%' ORDER BY Date DESC, Time DESC ";
            }
            else if (comboBox1.Text == "Date")
            {
                sql = "SELECT * FROM Traceability_Table WHERE Date BETWEEN " + dateTimePicker1.Value.ToString("yyyy-MM-dd") + " AND " + dateTimePicker2.Value.ToString("yyyy-MM-dd") + " ORDER BY Date DESC, Time DESC ";
                Console.WriteLine(sql);
            }

            DBConnection.Open();
            SQLiteCommand DBCommand = new SQLiteCommand(sql, DBConnection);
            DBSQLAdapter = new SQLiteDataAdapter(DBCommand);

            ds = new DataSet();
            DBSQLAdapter.Fill(ds, "Traceability_Table");
            dt = ds.Tables["Traceability_Table"];
            DBConnection.Close();

            dataGridView1.DataSource = ds.Tables["Traceability_Table"];
            dataGridView1.ReadOnly = true;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        }

        private void scan_camera()
        {
            try
            {
                List<ICameraInfo> cam_connected = CameraFinder.Enumerate();
                cam_model = new List<string>();
                cam_id = new List<string>();
                foreach (var item in cam_connected)
                {
                    cam_model.Add(item.GetValueOrDefault("ModelName", "").ToString());
                    cam_id.Add(item.GetValueOrDefault("SerialNumber", ""));
                    Console.WriteLine("Found Camera : {0}", item.GetValueOrDefault("ModelName", ""));
                    Console.WriteLine("Found Camera : {0}", item.GetValueOrDefault("SerialNumber", ""));
                }
                cbCamera.DataSource = cam_model;
                cbCamera.Update();
            }
            catch (Exception E)
            {
                MessageBox.Show(E.Message, "Error Scanning Camera", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void con_cam()
        {
            try
            {
                if (cam == null || !cam.IsConnected)
                {
                    cam = new Camera(cam_id[cbCamera.SelectedIndex]);
                    cam.Open();
                    cam.StreamGrabber.ImageGrabbed += OnImageGrabbed;
                    cam.CameraOpened += Configuration.AcquireSingleFrame;
                    cam.Parameters[PLUsbCamera.Height].SetValue(2048);
                    cam.Parameters[PLUsbCamera.Width].SetValue(2448);
                    cam.Parameters[PLUsbCamera.OffsetX].SetValue(8);
                    cam.Parameters[PLUsbCamera.OffsetY].SetValue(4);
                    //cam.Parameters[PLUsbCamera.ExposureTime].SetValue(Convert.ToDouble(108893));    //switch oli

                }
            }
            catch (Exception E)
            {
                MessageBox.Show(E.Message, "Error Connecting Camera", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


int robotPosition;
        public void capture()
        {
            if (cam != null)
            {
                if (!cam.IsOpen)
                {
                    MessageBox.Show("Camera is disconnected");
                    return;
                }
                
                if (partName == "ocv")   //ocv
                {
                    cam.Parameters[PLUsbCamera.ExposureTime].SetValue(Convert.ToDouble(120000));
                }
                else if (partName == "testbench_stamp")   //testbench stamp
                {
                    cam.Parameters[PLUsbCamera.ExposureTime].SetValue(Convert.ToDouble(20000));
                }
                else if (partName == "switch_oli")    //switch oli
                {
                    cam.Parameters[PLUsbCamera.ExposureTime].SetValue(Convert.ToDouble(148893));
                    //cam.Parameters[PLUsbCamera.ExposureTime].SetValue(Convert.ToDouble(183433));
                }
                else if (partName == "plug_drain_cock")   //plug drain cock
                {
                    cam.Parameters[PLUsbCamera.ExposureTime].SetValue(Convert.ToDouble(58893));
                    //cam.Parameters[PLUsbCamera.ExposureTime].SetValue(Convert.ToDouble(53433));
                }
                else if (partName == "oil_filter")   //oil filter
                {
                    cam.Parameters[PLUsbCamera.ExposureTime].SetValue(Convert.ToDouble(65814));
                    //cam.Parameters[PLUsbCamera.ExposureTime].SetValue(Convert.ToDouble(65000));
                }
                else if (partName == "cover_ex_manifold")   //cover ex manifold
                {
                    cam.Parameters[PLUsbCamera.ExposureTime].SetValue(Convert.ToDouble(34478));
                    //cam.Parameters[PLUsbCamera.ExposureTime].SetValue(Convert.ToDouble(38000));
                }
                




                //else if (robotPosition == 5)  //cover ex manifold
                //{
                //    cam.Parameters[PLUsbCamera.ExposureTime].SetValue(Convert.ToDouble(200000));
                //}
                //else if (robotPosition == 6)   //clamp wire
                //{
                //    cam.Parameters[PLUsbCamera.ExposureTime].SetValue(Convert.ToDouble(600000));
                //}
                //richTextBox1.Text += "Capture Pressed!\n";
                cam.StreamGrabber.Start(1, GrabStrategy.LatestImages, GrabLoop.ProvidedByStreamGrabber);
                while (cam.StreamGrabber.IsGrabbing) { }
                cam.StreamGrabber.Stop();
            }
            else
            {
                MessageBox.Show("Camera is disconnected");
            }
        }

        string filename;
        Bitmap bitmap;

        private void OnImageGrabbed(Object sender, ImageGrabbedEventArgs e)
        {
            while (true)
            {
                try
                {
                    modbusClient.WriteSingleRegister(12, 1); //flag move robot
                    break;
                }
                catch (Exception ex)
                {
                    reconnectRobot(ex.Message);
                }
            }

            if (InvokeRequired)
            {
                BeginInvoke(new EventHandler<ImageGrabbedEventArgs>(OnImageGrabbed), sender, e.Clone());
                return;
            }
            try
            {
                IGrabResult grabResult = e.GrabResult;
                //cam.Parameters[PLUsbCamera.ExposureTime].SetValue(Convert.ToDouble(tbExp.Text));
                if (grabResult.IsValid)
                {
                    PixelDataConverter converter = new PixelDataConverter();
                    if (bitmap != null)
                    {
                        bitmap.Dispose();
                    }
                    bitmap = new Bitmap(grabResult.Width, grabResult.Height, PixelFormat.Format32bppRgb);
                    BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, bitmap.PixelFormat);
                    converter.OutputPixelFormat = PixelType.BGRA8packed;
                    IntPtr ptrBmp = bmpData.Scan0;
                    converter.Convert(ptrBmp, bmpData.Stride * bitmap.Height, grabResult);
                    bitmap.UnlockBits(bmpData);

                    if (partName == "ocv")
                    {
                        Rectangle crop = new Rectangle(870, 800, 820, 740);

                        var bmp = new Bitmap(crop.Width, crop.Height);
                        using (var gr = Graphics.FromImage(bmp))
                        {
                            gr.DrawImage(bitmap, new Rectangle(0, 0, bmp.Width, bmp.Height), crop, GraphicsUnit.Pixel);
                        }
                        bitmap = bmp;
                    }
                    else if (partName == "testbench_stamp")
                    {
                        Rectangle crop = new Rectangle(840, 610, 870, 900);

                        var bmp = new Bitmap(crop.Width, crop.Height);
                        using (var gr = Graphics.FromImage(bmp))
                        {
                            gr.DrawImage(bitmap, new Rectangle(0, 0, bmp.Width, bmp.Height), crop, GraphicsUnit.Pixel);
                        }
                        bitmap = bmp;
                    }
                    else if (partName == "switch_oli")
                    {
                        if (engineTR == "2TR")
                        {
                            bitmap.RotateFlip(RotateFlipType.Rotate90FlipNone);
                            Rectangle crop = new Rectangle(840, 1110, 750, 630);

                            var bmp = new Bitmap(crop.Width, crop.Height);
                            using (var gr = Graphics.FromImage(bmp))
                            {
                                gr.DrawImage(bitmap, new Rectangle(0, 0, bmp.Width, bmp.Height), crop, GraphicsUnit.Pixel);
                            }
                            bitmap = bmp;
                            //Console.WriteLine("Crop");
                        }
                        else
                        {
                            bitmap.RotateFlip(RotateFlipType.Rotate90FlipNone);
                            Rectangle crop = new Rectangle(280, 920, 750, 580);

                            var bmp = new Bitmap(crop.Width, crop.Height);
                            using (var gr = Graphics.FromImage(bmp))
                            {
                                gr.DrawImage(bitmap, new Rectangle(0, 0, bmp.Width, bmp.Height), crop, GraphicsUnit.Pixel);
                            }
                            bitmap = bmp;
                            //Console.WriteLine("Crop");
                        }
                    }
                    else if (partName == "plug_drain_cock")
                    {
                        if (engineTR == "2TR")
                        {
                            bitmap.RotateFlip(RotateFlipType.Rotate90FlipNone);
                            Rectangle crop = new Rectangle(240, 1120, 750, 630);

                            var bmp = new Bitmap(crop.Width, crop.Height);
                            using (var gr = Graphics.FromImage(bmp))
                            {
                                gr.DrawImage(bitmap, new Rectangle(0, 0, bmp.Width, bmp.Height), crop, GraphicsUnit.Pixel);
                            }
                            bitmap = bmp;
                        }
                        else
                        {
                            bitmap.RotateFlip(RotateFlipType.Rotate90FlipNone);
                            Rectangle crop = new Rectangle(870, 950, 750, 630);

                            var bmp = new Bitmap(crop.Width, crop.Height);
                            using (var gr = Graphics.FromImage(bmp))
                            {
                                gr.DrawImage(bitmap, new Rectangle(0, 0, bmp.Width, bmp.Height), crop, GraphicsUnit.Pixel);
                            }
                            bitmap = bmp;
                        }
                    }
                    else if (partName == "oil_filter")
                    {
                        //bitmap.RotateFlip(RotateFlipType.Rotate180FlipNone);
                    }
                    else if (partName == "cover_ex_manifold")
                    {
                        //bitmap.RotateFlip(RotateFlipType.Rotate180FlipNone);
                    }

                    filename = "C:\\ENGINE_VI_Image\\" + partName + "\\" + DateTime.Now.ToString("yyyy-MM-dd") + "_" + DateTime.Now.ToString("HH-mm-ss") + "_" + barcodeEngineNumber + ".jpeg";
                    bitmap.Save(filename, ImageFormat.Jpeg);


                    if (partName == "ocv")
                    {
                        ImageFilenameOCV = filename;
                    }
                    else if (partName == "testbench_stamp")
                    {
                        ImageFilenameTestbenchStamp = filename;
                    }
                    else if (partName == "switch_oli")
                    {
                        Console.WriteLine(filename);
                        ImageFilenameSwitchOli = filename;
                    }
                    else if (partName == "plug_drain_cock")
                    {
                        ImageFilenamePlugDrainCock = filename;                        
                    }
                    else if (partName == "oil_filter")
                    {
                        ImageFilenameOilFilter = filename;                        
                    }
                    else if (partName == "cover_ex_manifold")
                    {
                        ImageFilenameCoverExManifold = filename;                        
                    }                   

                    //pictureBox7.Image = bitmap;

                    process(bitmap, filename, partName);
                    GC.Collect();
                }
            }
            catch (Exception E)
            {
                MessageBox.Show(E.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                e.DisposeGrabResultIfClone();
            }
        }

        private void clearpb()
        {
            //Bitmap oldBmp1 = pictureBox1.Image as Bitmap;
            pictureBox1.Image = null;
            //if (oldBmp1 != null) oldBmp1.Dispose();
            //Bitmap oldBmp2 = pictureBox2.Image as Bitmap;
            pictureBox2.Image = null;
            //if (oldBmp2 != null) oldBmp2.Dispose();
            // Bitmap oldBmp3 = pictureBox3.Image as Bitmap;
            pictureBox3.Image = null;
            //if (oldBmp3 != null) oldBmp3.Dispose();
            //Bitmap oldBmp4 = pictureBox4.Image as Bitmap;
            pictureBox4.Image = null;
            //if (oldBmp4 != null) oldBmp4.Dispose();
            //Bitmap oldBmp5 = pictureBox5.Image as Bitmap;
            //pictureBox5.Image = null;
            ////if (oldBmp5 != null) oldBmp5.Dispose();
            ////Bitmap oldBmp6 = pictureBox6.Image as Bitmap;
            //pictureBox6.Image = null;
            ////if (oldBmp6 != null) oldBmp6.Dispose();
            ////Bitmap oldBmp7 = pictureBox7.Image as Bitmap;
            pictureBox7.Image = null;
            //if (oldBmp7 != null) oldBmp7.Dispose();
            pictureBox8.Image = null;

            label24.Text = "-";
            label24.BackColor = Color.Gray;
            label25.Text = "-";
            label25.BackColor = Color.Gray;
            label26.Text = "-";
            label26.BackColor = Color.Gray;
            label27.Text = "-";
            label27.BackColor = Color.Gray;
            label16.Text = "-";
            label16.BackColor = Color.Gray;
            label23.Text = "-";
            label23.BackColor = Color.Gray;
            label29.Text = "-";
            label29.BackColor = Color.Gray;
            label30.Text = "-";
            label30.BackColor = Color.Gray;

            statusAllNG = false;

            ImageFilenameOCV = "";
            ImageFilenameTestbenchStamp = "";
            ImageFilenameSwitchOli = "";
            ImageFilenamePlugDrainCock = "";
            ImageFilenameOilFilter = "";
            ImageFilenameCoverExManifold = "";

            OCVStatus = "";
            TestbenchStampStatus = "";
            SwitchOliStatus = "";
            PlugDrainCockStatus = "";
            OilFilterStatus = "";
            CoverExManifoldStatus = "";            
            JudgementStatus = "";
        }
        int CountImage = 0;

    public void process(Bitmap bmp, String filename, string part)
        {
            byte[] imageIn = System.IO.File.ReadAllBytes(filename);
            if (part == "ocv")
            {
                Bitmap oldBmp = pictureBox7.Image as Bitmap;
                pictureBox7.Image = bmp;

                //label27.Text = "OK";
                //label27.BackColor = Color.Green;
                //label16.Text = "OK";
                //label16.BackColor = Color.Green;

                try
                {
                    call_Scoring_API_Neurala(imageIn, filename, part);
                }
                catch (Exception E)
                {
                    label23.Text = "-";
                    label23.BackColor = Color.Gray;
                }
                if (oldBmp != null) oldBmp.Dispose();
            }
            else if (part == "testbench_stamp")
            {
                Bitmap oldBmp = pictureBox8.Image as Bitmap;
                pictureBox8.Image = bmp;

                //label27.Text = "OK";
                //label27.BackColor = Color.Green;
                //label16.Text = "OK";
                //label16.BackColor = Color.Green;

                try
                {
                    call_Scoring_API_Neurala(imageIn, filename, part);
                }
                catch (Exception E)
                {
                    label29.Text = "-";
                    label29.BackColor = Color.Gray;
                }
                if (oldBmp != null) oldBmp.Dispose();
            }
            else if (part == "switch_oli")
            {
                //clearpb();
                Bitmap oldBmp = pictureBox1.Image as Bitmap;
                pictureBox1.Image = bmp;

                //Boolean result = inspection(bmp);

                //label24.Text = "OK";
                //label24.BackColor = Color.Green;

                try
                {
                    call_Scoring_API_Neurala(imageIn, filename, part);
                }
                catch (Exception E)
                {
                    label24.Text = "-";
                    label24.BackColor = Color.Gray;
                }
                if (oldBmp != null) oldBmp.Dispose();
            }
            else if (part == "plug_drain_cock")
            {
                Bitmap oldBmp = pictureBox2.Image as Bitmap;
                pictureBox2.Image = bmp;

                //label25.Text = "OK";
                //label25.BackColor = Color.Green;

                try
                {
                    call_Scoring_API_Neurala(imageIn, filename, part);
                }
                catch (Exception E)
                {
                    label25.Text = "-";
                    label25.BackColor = Color.Gray;
                }
                if (oldBmp != null) oldBmp.Dispose();
            }
            else if (part == "oil_filter")
            {
                Bitmap oldBmp = pictureBox3.Image as Bitmap;
                pictureBox3.Image = bmp;

                //label26.Text = "OK";
                //label26.BackColor = Color.Green;

                try
                {
                    call_Scoring_API_Neurala(imageIn, filename, part);
                }
                catch (Exception E)
                {
                    label26.Text = "-";
                    label26.BackColor = Color.Gray;
                }
                if (oldBmp != null) oldBmp.Dispose();
            }
            else if (part == "cover_ex_manifold")
            {
                Bitmap oldBmp = pictureBox4.Image as Bitmap;
                pictureBox4.Image = bmp;

                //label27.Text = "OK";
                //label27.BackColor = Color.Green;
                //label16.Text = "OK";
                //label16.BackColor = Color.Green;

                try
                {
                    call_Scoring_API_Neurala(imageIn, filename, part);
                }
                catch (Exception E)
                {
                    label27.Text = "-";
                    label27.BackColor = Color.Gray;
                }
                if (oldBmp != null) oldBmp.Dispose();
            }
            
            //else if (part == 5)
            //{
            //    Bitmap oldBmp = pictureBox5.Image as Bitmap;
            //    pictureBox5.Image = bmp;
            //    //Boolean result = inspection(bmp);
            //    if (oldBmp != null) oldBmp.Dispose();
            //    //call_Scoring_API_OBJ_Dummy(filename);
            //    label28.Visible = true;
            //}
            //else if (part == 6)
            //{
            //    Bitmap oldBmp = pictureBox6.Image as Bitmap;
            //    pictureBox6.Image = bmp;
            //    if (oldBmp != null) oldBmp.Dispose();
            //    //call_Scoring_API_OBJ_Dummy(filename);
            //    label29.Visible = true;
            //}
            //else if (part == 7)
            //{
            //    Bitmap oldBmp = pictureBox7.Image as Bitmap;
            //    pictureBox7.Image = bmp;
            //    if (oldBmp != null) oldBmp.Dispose();
            //    //call_Scoring_API_OBJ_Dummy(filename);
            //    label30.Visible = true;
            //}
        }

        Bitmap imageBitmap;

    private async void call_Scoring_API_Neurala(byte[] imageFiles, String fileName, String part)         //API Scoring Inspector
        {
            string model = "";
            string conf = "";
            string res = "";

            try
            {
                HttpClient client = new HttpClient();

                var content = new MultipartFormDataContent();
                var byteContent = new ByteArrayContent(imageFiles);
                byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                content.Add(byteContent, "files", fileName);

                if (part == "ocv")
                {
                    HttpResponseMessage response = await client.PostAsync(
                        "http://localhost:9002/v1/brains/48be5c5a-1f96-4191-8bc0-50ece0290490/inferences/form", content);       //ocv 1
                    Console.WriteLine("Sent");
                    res = await response.Content.ReadAsStringAsync();
                }
                else if (part == "testbench_stamp")
                {
                    HttpResponseMessage response = await client.PostAsync(
                        "http://localhost:9002/v1/brains/3bea279e-ada6-469d-9540-c75ac7b4a190/inferences/form", content);       //testbench stamp 1
                    Console.WriteLine("Sent");
                    res = await response.Content.ReadAsStringAsync();
                }
                else if (part == "switch_oli")
                {
                    //HttpResponseMessage response = await client.PostAsync(
                    //    "http://localhost:9002/v1/brains/8a722fbe-697f-4ac7-a608-d3cf75aba594/inferences/form", content);     //switch_oli_1
                    //HttpResponseMessage response = await client.PostAsync(
                    //    "http://localhost:9002/v1/brains/18d8440c-4894-4d74-9db5-49e78e90a5c0/inferences/form", content);     //switch_oli_2
                   
                    //Console.WriteLine("Sent");
                    //res = await response.Content.ReadAsStringAsync();

                    if (engineTR == "2TR")
                    {
                        //HttpResponseMessage response = await client.PostAsync(
                        //"http://localhost:9002/v1/brains/518fd302-6415-4497-aef1-29ba8553c953/inferences/form", content);       //switch_oli_2tr_1
                        HttpResponseMessage response = await client.PostAsync(
                        "http://localhost:9002/v1/brains/387d77d6-2bb9-444f-a17d-88191dad3749/inferences/form", content);       //switch_oli_2tr_2                        
                        Console.WriteLine("Sent");
                        res = await response.Content.ReadAsStringAsync();
                    }
                    else
                    {
                        //HttpResponseMessage response = await client.PostAsync(
                        //"http://localhost:9002/v1/brains/3bf63019-2aac-4601-a5d2-b8deb241120f/inferences/form", content);       //switch_oli_1tr_1
                        HttpResponseMessage response = await client.PostAsync(
                        "http://localhost:9002/v1/brains/18edd5dc-5a57-4919-8e0e-52ea994b31aa/inferences/form", content);       //switch_oli_1tr_2                        
                        Console.WriteLine("Sent");
                        res = await response.Content.ReadAsStringAsync();
                    }
                }
                else if (part == "plug_drain_cock")
                {
                    //HttpResponseMessage response = await client.PostAsync(
                    //    "http://localhost:9002/v1/brains/ecf078ae-c292-4cd8-9554-58893517d294/inferences/form", content);     //plug_drain_cock_1
                    //HttpResponseMessage response = await client.PostAsync(
                    //    "http://localhost:9002/v1/brains/c78b5975-f2ca-4022-8580-9739b66d7901/inferences/form", content);     //plug_drain_cock_2
                    
                    //Console.WriteLine("Sent");
                    //res = await response.Content.ReadAsStringAsync();

                    if (engineTR == "2TR")
                    {
                        //HttpResponseMessage response = await client.PostAsync(
                        //"http://localhost:9002/v1/brains/ad8dc3a7-3999-49e0-ac0b-0316a56033ec/inferences/form", content);       //plug_drain_cock_2tr_1
                        HttpResponseMessage response = await client.PostAsync(
                        "http://localhost:9002/v1/brains/7521d80d-720d-4a92-b470-67eab9d3f073/inferences/form", content);       //plug_drain_cock_2tr_2                        
                        Console.WriteLine("Sent");
                        res = await response.Content.ReadAsStringAsync();
                    }
                    else
                    {
                        //HttpResponseMessage response = await client.PostAsync(
                        //"http://localhost:9002/v1/brains/fb265d36-c696-405a-9572-fede2a3a29b0/inferences/form", content);       //plug_drain_cock_1tr_1
                        HttpResponseMessage response = await client.PostAsync(
                        "http://localhost:9002/v1/brains/8c05a8ae-e241-4d8e-8901-9f318ed2f141/inferences/form", content);       //plug_drain_cock_1tr_2                        
                        Console.WriteLine("Sent");
                        res = await response.Content.ReadAsStringAsync();
                    }                                                         
                }
                else if (part == "oil_filter")
                {
                    //HttpResponseMessage response = await client.PostAsync(
                    //    "http://localhost:9002/v1/brains/07f9230f-73c0-4220-8f09-f58de76d224c/inferences/form", content);     //oil_filter_1
                    //HttpResponseMessage response = await client.PostAsync(
                    //    "http://localhost:9002/v1/brains/307b763a-f35b-45d0-92e3-a216fa17b70a/inferences/form", content);     //oil_filter_2
                    //HttpResponseMessage response = await client.PostAsync(
                    //    "http://localhost:9002/v1/brains/10ef21ed-f99f-4cbf-b39d-0b174525a692/inferences/form", content);       //oil_filter_3
                    HttpResponseMessage response = await client.PostAsync(
                        "http://localhost:9002/v1/brains/bbb520d0-b810-47da-b08a-b2fd3e0bc633/inferences/form", content);       //oil_filter_4                    
                    Console.WriteLine("Sent");
                    res = await response.Content.ReadAsStringAsync();
                }
                else if (part == "cover_ex_manifold")
                {
                    //HttpResponseMessage response = await client.PostAsync(                                                    //anomaly
                    //    "http://localhost:9002/v1/brains/976a4412-bc2e-4a9d-b206-d5ce044cf64a/inferences/form", content);
                    //HttpResponseMessage response = await client.PostAsync(
                    //    "http://localhost:9002/v1/brains/60d34528-9fe4-4ccd-bcc1-bcbface822e2/inferences/form", content);       //classification 1
                    //HttpResponseMessage response = await client.PostAsync(
                    //    "http://localhost:9002/v1/brains/4a9aba19-75f3-4fa3-a01c-daa6352b9ba2/inferences/form", content);         //classification 2
                    //HttpResponseMessage response = await client.PostAsync(
                    //    "http://localhost:9002/v1/brains/c4345a06-6cd2-4b32-8a2e-c3a997fe2a91/inferences/form", content);         //classification 2a
                    HttpResponseMessage response = await client.PostAsync(
                        "http://localhost:9002/v1/brains/7a3382de-3fc5-417c-a053-a295da330f2a/inferences/form", content);         //classification 3                                  
                    Console.WriteLine("Sent");
                    res = await response.Content.ReadAsStringAsync();

                    //if (engineTR == "2TR")
                    //{
                    //    HttpResponseMessage response = await client.PostAsync(
                    //    "http://localhost:9002/v1/brains/1838d3af-91d2-4406-b7b2-66b065e1ee29/inferences/form", content);
                    //    Console.WriteLine("Sent");
                    //    res = await response.Content.ReadAsStringAsync();
                    //}
                    //else
                    //{
                    //    HttpResponseMessage response = await client.PostAsync(
                    //    "http://localhost:9002/v1/brains/407b9b03-84e7-4666-8fd9-81def9124fd1/inferences/form", content);
                    //    Console.WriteLine("Sent");
                    //    res = await response.Content.ReadAsStringAsync();
                    //}
                }                

                dynamic JSON = JsonConvert.DeserializeObject(res);
                //TextBoxBrainAPI.Text = res;
                Console.WriteLine(JSON);
                judgement(JSON, fileName, part);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error When Calling Inspector API, Err Code : " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void judgement(dynamic inspection_result, string fileName, string part)                      //anomaly parsing
        {
            String labelName = "";
            String confidenceLevel = "0";
            String highest_confidenceLevel = "0";
            String highest_labelName = "";
            String result = "OK";
            Boolean statusNG = false;

            try
            {
                foreach (var data1 in inspection_result.predictions)
                {
                    foreach (var data2 in data1.results)
                    {
                        labelName = data2.label;
                        confidenceLevel = data2.probability;

                        if (Convert.ToDouble(confidenceLevel) > Convert.ToDouble(highest_confidenceLevel))
                        {
                            highest_confidenceLevel = confidenceLevel;
                            highest_labelName = labelName;
                        }
                    }
                }

                labelName = highest_labelName;
                confidenceLevel = highest_confidenceLevel;
                Console.WriteLine("Label: " + labelName + ", conf: " + confidenceLevel);
                textBox5.Text = labelName;

                if (part == "cover_ex_manifold")
                {
                    label30.Text = labelName;
                    if (labelName == engineTR)
                    {
                        //label ok
                        result = "OK";
                    }
                    else
                    {
                        //result = "OK";
                        result = "NG";
                        statusNG = true;
                        statusAllNG = true;
                    }
                }
                else
                {
                    if (labelName == "Normal")
                    {
                        //label OK
                        result = "OK";
                    }
                    else
                    {
                        //result = "OK";
                        result = "NG";
                        statusNG = true;
                        statusAllNG = true;
                    }
                }                                
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error Getting Results from AI, Err Code : " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            try
            {
                if (part == "ocv")
                {
                    if (statusNG == true)
                    {
                        label23.BackColor = Color.Red;
                    }
                    else
                    {
                        label23.BackColor = Color.Green;
                    }
                    //label23.ForeColor = Color.White;
                    label23.Text = result;
                    OCVStatus = result;
                }
                else if (part == "testbench_stamp")
                {
                    if (statusNG == true)
                    {
                        label29.BackColor = Color.Red;
                    }
                    else
                    {
                        label29.BackColor = Color.Green;
                    }
                    //label29.ForeColor = Color.White;
                    label29.Text = result;
                    TestbenchStampStatus = result;
                }
                else if (part == "switch_oli")
                {
                    if (statusNG == true)
                    {
                        label24.BackColor = Color.Red;
                    }
                    else
                    {
                        label24.BackColor = Color.Green;
                    }
                    //label24.ForeColor = Color.White;
                    label24.Text = result;
                    SwitchOliStatus = result;
                }
                else if (part == "plug_drain_cock")
                {
                    if (statusNG == true)
                    {
                        label25.BackColor = Color.Red; 
                    }
                    else
                    {
                        label25.BackColor = Color.Green;
                    }
                    //label25.ForeColor = Color.White;
                    label25.Text = result;
                    PlugDrainCockStatus = result;
                }
                else if (part == "oil_filter")
                {
                    if (statusNG == true)
                    {
                        label26.BackColor = Color.Red;
                    }
                    else
                    {
                        label26.BackColor = Color.Green;
                    }
                    //label26.ForeColor = Color.White;
                    label26.Text = result;
                    OilFilterStatus = result;
                }
                else if (part == "cover_ex_manifold")
                {
                    if (statusNG == true)
                    {
                        label27.BackColor = Color.Red;
                    }
                    else
                    {
                        label27.BackColor = Color.Green;
                    }
                    //label27.ForeColor = Color.White;
                    label27.Text = result;
                    CoverExManifoldStatus = result;

                    if (statusAllNG == true)
                    {
                        label16.BackColor = Color.Red;
                        label16.Text = "NG";
                        JudgementStatus = "NG";
                        while (true)
                        {
                            try
                            {
                                modbusClient.WriteSingleRegister(20, 2); //flag NG inspection
                                break;
                            }
                            catch (Exception ex)
                            {
                                reconnectRobot(ex.Message);
                            }
                        }
                        PLC.WriteWord(PlcMemory.WR, 2, 2);  //flag NG inspection
                    }
                    else
                    {
                        label16.BackColor = Color.Green;
                        label16.Text = "OK";
                        JudgementStatus = "OK";
                        while (true)
                        {
                            try
                            {
                                modbusClient.WriteSingleRegister(20, 1); //flag OK inspection
                                break;
                            }
                            catch (Exception ex)
                            {
                                reconnectRobot(ex.Message);
                            }
                        }
                        PLC.WriteWord(PlcMemory.WR, 2, 1);  //flag OK inspection
                    }

                    DBInsertData(DBSQLConnection);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error Displaying AI Result Image, Err Code : " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void reconnectRobot(string error)
        {
            //timer1.Enabled = false;
            textBox7.Text = "Error Modbus, Err Code : " + error;
            //if (MessageBox.Show("Error Modbus. Reconnecting...", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error) == DialogResult.OK)
            //{
            modbusClient.Disconnect();
            while (!modbusClient.Connected)
            {
                connectRobot();
                textBox6.Text = "Connecting";
            }
            textBox6.Text = "Connected";
            textBox7.Text = "";
            //timer1.Enabled = true;
            //}
        }

        int count = 0;

        private void timer1_Tick(object sender, EventArgs e)
        {
            textBox17.Text = "3";
            //count++;
            textBox18.Text = count.ToString();

            short triggerRobotStart;
            short inspectionResult;
            short robotErrorStatus;
            short robotProjectStatus;
            int[] readRegisterStatus;
            int[] readRegisterError;
            bool[] readCoilRobot;
            int[] readRegister;
            int[] readRegister13;
            int[] readRegister18;


            PLC.ReadWord(PlcMemory.WR, 1, out triggerRobotStart);   //read from plc
            PLC.ReadWord(PlcMemory.WR, 2, out inspectionResult);   //read from plc
            PLC.ReadWord(PlcMemory.WR, 3, out robotErrorStatus);   //read from plc
            PLC.ReadWord(PlcMemory.WR, 4, out robotProjectStatus);   //read from plc

            while (true)
            {
                try
                {                    
                    //readRegisterStatus = modbusClient.ReadHoldingRegisters(21, 1);   //read robot inspection status
                    //readRegisterError = modbusClient.ReadHoldingRegisters(7320, 2);   //read robot error code
                    //readCoilRobot = modbusClient.ReadCoils(7201, 4);   //read robot status
                    //readRegister = modbusClient.ReadHoldingRegisters(10, 3);   //read robot flag & position
                    readRegister13 = modbusClient.ReadHoldingRegisters(13, 5);   //read engine number barcode string
                    //readRegister18 = modbusClient.ReadHoldingRegisters(18, 2);   //read engine type barcode string
                    textBox17.Text = "4";
                    break;
                }
                catch (Exception ex)
                {
                    reconnectRobot(ex.Message);
                }
            }

                        
            //uint errorCodeRobot = (uint)((readRegisterError[1] << 16) | readRegisterError[0]);

            //robotError = readCoilRobot[0];
            //robotRunning = readCoilRobot[1];
            //robotPause = readCoilRobot[3];

            //textBox12.Text = readRegisterStatus[0].ToString();
            //textBox13.Text = robotError.ToString();
            //textBox14.Text = robotRunning.ToString();
            //textBox15.Text = robotPause.ToString();
            //textBox16.Text = errorCodeRobot.ToString();

            //textBox10.Text = triggerRobotStart.ToString();
            //textBox9.Text = inspectionResult.ToString();
            //textBox8.Text = robotErrorStatus.ToString();
            //textBox11.Text = robotProjectStatus.ToString();


            //if (modbusClient.Connected)
            //{
            //    label40.Text = "V";
            //    label40.ForeColor = Color.Lime;
            //}
            //else
            //{
            //    label40.Text = "X";
            //    label40.ForeColor = Color.Red;
            //}

            ////plc
            //if (robotError)
            //{
            //    PLC.WriteWord(PlcMemory.WR, 3, 1);
            //}
            //else
            //{
            //    PLC.WriteWord(PlcMemory.WR, 3, 0);
            //}

            //if (robotRunning && !robotPause)
            //{
            //    PLC.WriteWord(PlcMemory.WR, 4, 1);
            //}
            //else if (robotPause)
            //{
            //    PLC.WriteWord(PlcMemory.WR, 4, 2);
            //}
            //else
            //{
            //    PLC.WriteWord(PlcMemory.WR, 4, 0);
            //}

            //if (triggerRobotStart == 1 && readRegisterStatus[0] == 0)
            //{
            //    clearpb();
            //    PLC.WriteWord(PlcMemory.WR, 2, 0);  //reset inspection result
            //    while (true)
            //    {
            //        try
            //        {
            //            modbusClient.WriteSingleRegister(21, 1); //trigger robot start
            //            break;
            //        }
            //        catch (Exception ex)
            //        {
            //            reconnectRobot(ex.Message);
            //        }
            //    }
            //    PLC.WriteWord(PlcMemory.WR, 1, 2);  //flag robot running
            //}
            //else if (triggerRobotStart == 2 && readRegisterStatus[0] == 2)
            //{
            //    PLC.WriteWord(PlcMemory.WR, 1, 3);  //flag robot finish
            //}
            //else if (triggerRobotStart == 0 && readRegisterStatus[0] == 2)
            //{
            //    while (true)
            //    {
            //        try
            //        {
            //            modbusClient.WriteSingleRegister(21, 0); //trigger robot idle   
            //            break;
            //        }
            //        catch (Exception ex)
            //        {
            //            reconnectRobot(ex.Message);
            //        }
            //    }
            //}

            //flagCapture = readRegister[0];
            //robotPosition = readRegister[1];
            //flagMove = readRegister[2];

            //textBox1.Text = flagCapture.ToString();
            //textBox2.Text = robotPosition.ToString();
            //textBox3.Text = flagMove.ToString();

            //if (robotPosition == 1)
            //{
            //    partName = "ocv";
            //}
            //else if (robotPosition == 2)
            //{
            //    partName = "testbench_stamp";
            //}
            //else if (robotPosition == 3)
            //{
            //    partName = "switch_oli";
            //}
            //else if(robotPosition == 4)
            //{
            //    partName = "plug_drain_cock";
            //}
            //else if(robotPosition == 5)
            //{
            //    partName = "oil_filter";
            //}
            //else if(robotPosition == 6)
            //{
            //    partName = "cover_ex_manifold";
            //}

            //read engine no barcode
            //Console.WriteLine(readRegister13[0]);
            //string barcodeEngineNumber = new string(Array.ConvertAll(readRegister13, x => (char)x));
            List<byte> bytes = new List<byte>();
            foreach (int i in readRegister13)
            {
                bytes.Add(BitConverter.GetBytes(i)[1]);
                bytes.Add(BitConverter.GetBytes(i)[0]);
            }
            byte[] b = bytes.ToArray();
            barcodeEngineNumber = new ASCIIEncoding().GetString(b);
            if (!flagOverride)
            {
                engineTR = barcodeEngineNumber.Substring(0, 3);
            }



            //edit
            if (barcodeEngineNumber.Substring(0, 1) == "X") //barcodeEngineNumber.Substring(0, 1) == "x" ||
            {
                label7.Text = barcodeEngineNumber;

                //show form
                Scan_Barcode_Form Scan_Barcode_Form = new Scan_Barcode_Form(this);
                
                //hold sequence while scan barcode                
                timer1.Enabled = false;
                var result = Scan_Barcode_Form.ShowDialog();
                Console.WriteLine(result);
                
                if (result.ToString() == "Cancel")
                {
                    timer1.Enabled = true;
                }

                //write new barcode to modbus
                int j, numSpace;

                if (barcodeEngineNumber.Length < 2) // add space
                {
                    numSpace = 2 - barcodeEngineNumber.Length;
                    for (j = 0; j < numSpace; j++)
                    {
                        barcodeEngineNumber = barcodeEngineNumber + " ";
                    }
                }

                int[] writeRegister13 = ConvertStringToRegisters(barcodeEngineNumber);

                while (true)
                {
                    try
                    {
                        modbusClient.WriteMultipleRegisters(13, writeRegister13);
                        break;
                    }
                    catch (Exception ex)
                    {
                        reconnectRobot(ex.Message);
                    }
                }

                //get engine TR
                if (barcodeEngineNumber.Substring(0, 1) != "x")
                {
                    engineTR = barcodeEngineNumber.Substring(0, 3);
                }
                else
                {
                    engineTR = barcodeEngineNumber;
                }
            }
            //edit



            label7.Text = barcodeEngineNumber;

            //read engine type barcode
            //Console.WriteLine(readRegister8[0]);
            //string barcode = new string(Array.ConvertAll(readRegister8, x => (char)x));
            //List<byte> bytes2 = new List<byte>();
            //foreach (int i in readRegister18)
            //{
            //    bytes2.Add(BitConverter.GetBytes(i)[1]);
            //    bytes2.Add(BitConverter.GetBytes(i)[0]);
            //}
            //byte[] b2 = bytes2.ToArray();
            //barcodeengineType = new ASCIIEncoding().GetString(b2);                                    
            //if (barcodeengineType.Substring(0, 1) == "x" || barcodeengineType.Substring(0, 1) == "X")
            //{
            //    barcodeengineType = barcodeengineType.Substring(0, 1);
            //}
            //label8.Text = barcodeengineType;


            //if (barcodeengineType.Substring(0, 1) == "-" && flagDoneReset == false)
            //{
            //    //clearpb();
            //    flagDoneReset = true;
            //}
            //else if (barcodeengineType.Substring(0, 1) != "-" && flagDoneReset == true)
            //{
            //    flagDoneReset = false;
            //}

            //if (flagCapture == 1)
            //{
            //    while (true)
            //    {
            //        try
            //        {
            //            modbusClient.WriteSingleRegister(10, 0); //reset flag capture                        
            //            break;
            //        }
            //        catch (Exception ex)
            //        {
            //            reconnectRobot(ex.Message);
            //        }
            //    }
            //    capture();
            //}
        }

        //edit
        private static int[] ConvertStringToRegisters(string stringToConvert)
        {
            byte[] array = System.Text.Encoding.ASCII.GetBytes(stringToConvert);

            int[] returnarray = new int[stringToConvert.Length / 2 + stringToConvert.Length % 2];
            for (int i = 0; i < returnarray.Length; i++)
            {
                //returnarray[i] = array[i * 2];
                returnarray[i] = ((int)array[i * 2] << 8);
                if (i * 2 + 1 < array.Length)
                {
                    //returnarray[i] = returnarray[i] | ((int)array[i * 2 + 1] << 8);
                    returnarray[i] = returnarray[i] | array[i * 2 + 1];
                }
            }
            return returnarray;
        }
        //edit

        private void connectRobot()     //connect Modbus Robot
        {
            try
            {
                //modbusClient = new ModbusClient("172.16.1.1", 502);    //Ip-Address and Port of Modbus-TCP-Server
                modbusClient = new ModbusClient("127.0.0.1", 502);    //Ip-Address and Port of Modbus-TCP-Server
                modbusClient.Connect();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error Finding Robot, Err Code : {0}", ex.Message);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (cam != null)
            {
                if (!cam.IsOpen)
                {
                    MessageBox.Show("Camera is disconnected");
                    return;
                }
             
                cam.StreamGrabber.Start(1, GrabStrategy.LatestImages, GrabLoop.ProvidedByStreamGrabber);
                while (cam.StreamGrabber.IsGrabbing) { }
                cam.StreamGrabber.Stop();
            }
            else
            {
                MessageBox.Show("Camera is disconnected");
            }
        }

        private void pictureBox5_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            Console.WriteLine(ImageFilenameSwitchOli);
            if(ImageFilenameSwitchOli != "" && ImageFilenameSwitchOli != null)
            {
                Image_Form.show_image(ImageFilenameSwitchOli);
                Image_Form.ShowDialog();
            }            
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (!flagOverride)
            {
                flagOverride = true;
                button2.BackColor = Color.Yellow;
                button3.BackColor = Color.LightGray;
                engineTR = "1TR";
            }
            else if(flagOverride && engineTR == "1TR")
            {
                flagOverride = false;
                button2.BackColor = Color.LightGray;
            }
            else
            {
                flagOverride = true;
                button2.BackColor = Color.Yellow;
                button3.BackColor = Color.LightGray;
                engineTR = "1TR";
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (!flagOverride)
            {
                flagOverride = true;
                button3.BackColor = Color.Yellow;
                button2.BackColor = Color.LightGray;
                engineTR = "2TR";
            }
            else if (flagOverride && engineTR == "2TR")
            {
                flagOverride = false;
                button3.BackColor = Color.LightGray;
            }
            else
            {
                flagOverride = true;
                button3.BackColor = Color.Yellow;
                button2.BackColor = Color.LightGray;
                engineTR = "2TR";
            }
        }

        private void pictureBox2_Click(object sender, EventArgs e)
        {
            if (ImageFilenamePlugDrainCock != "" && ImageFilenamePlugDrainCock != null)
            {
                Image_Form.show_image(ImageFilenamePlugDrainCock);
                Image_Form.ShowDialog();
            }
        }

        private void pictureBox3_Click(object sender, EventArgs e)
        {
            if (ImageFilenameOilFilter != "" && ImageFilenameOilFilter != null)
            {
                Image_Form.show_image(ImageFilenameOilFilter);
                Image_Form.ShowDialog();
            }
        }

        private void pictureBox4_Click(object sender, EventArgs e)
        {
            if (ImageFilenameCoverExManifold != "" && ImageFilenameCoverExManifold != null)
            {
                Image_Form.show_image(ImageFilenameCoverExManifold);
                Image_Form.ShowDialog();
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (ImageFilenameOCV != "" && ImageFilenameOCV != null)
            {
                pictureBox7.Image = new Bitmap(Image.FromFile(@ImageFilenameOCV));
            }

            if (ImageFilenameTestbenchStamp != "" && ImageFilenameTestbenchStamp != null)
            {
                pictureBox8.Image = new Bitmap(Image.FromFile(@ImageFilenameTestbenchStamp));
            }

            if (ImageFilenameSwitchOli != "" && ImageFilenameSwitchOli != null)
            {
                pictureBox1.Image = new Bitmap(Image.FromFile(@ImageFilenameSwitchOli));
            }

            if (ImageFilenamePlugDrainCock != "" && ImageFilenamePlugDrainCock != null)
            {
                pictureBox2.Image = new Bitmap(Image.FromFile(@ImageFilenamePlugDrainCock));
            }

            if (ImageFilenameOilFilter != "" && ImageFilenameOilFilter != null)
            {
                pictureBox3.Image = new Bitmap(Image.FromFile(@ImageFilenameOilFilter));
            }

            if (ImageFilenameCoverExManifold != "" && ImageFilenameCoverExManifold != null)
            {
                pictureBox4.Image = new Bitmap(Image.FromFile(@ImageFilenameCoverExManifold));
            }

            tabControl1.SelectedIndex = 0;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            try
            {
                DBReadData(DBSQLConnection);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error Read Database, Err Code : " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            tabControl1.SelectedIndex = 1;
        }

        private void button6_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Clear all data?", "Confirmation", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
            {
                DBCreateTable(DBSQLConnection);
                DBReadData(DBSQLConnection);
                MessageBox.Show("All data has been cleared successfully.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }           
        }

        private void button7_Click(object sender, EventArgs e)
        {
            ImageFilenamePlugDrainCock = "C:\\ENGINE_VI_Image\\" + "oil1_" + "13-09-2022" + "_" + "13-00-00" + ".jpeg";
            ImageFilenameOilFilter = "C:\\ENGINE_VI_Image\\" + "oil2_" + "13-09-2022" + "_" + "13-00-00" + ".jpeg";
            ImageFilenameCoverExManifold = "C:\\ENGINE_VI_Image\\" + "oil3_" + "13-09-2022" + "_" + "13-00-00" + ".jpeg";
            ImageFilenameSwitchOli = "C:\\ENGINE_VI_Image\\" + "oil4_" + "13-09-2022" + "_" + "13-00-00" + ".jpeg";
            ImageFilenameOCV = "C:\\ENGINE_VI_Image\\" + "oil4_" + "13-09-2022" + "_" + "13-00-00" + ".jpeg";
            ImageFilenameTestbenchStamp = "C:\\ENGINE_VI_Image\\" + "oil4_" + "13-09-2022" + "_" + "13-00-00" + ".jpeg";
            barcodeEngineNumber = "QWER";
            barcodeengineType = "TY";
            JudgementStatus = "NG";

            DBInsertData(DBSQLConnection);
            DBReadData(DBSQLConnection);
        }

        private void button8_Click(object sender, EventArgs e)
        {
            DBReadData(DBSQLConnection);
        }


        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)    //show image from database
        {
            //MessageBox.Show((e.RowIndex) + "  Row  " + (e.ColumnIndex) + "  Column button clicked ");
            string DBimageFileName = "";
            if (e.ColumnIndex == 4)
            {
                DBimageFileName = dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex+1].Value.ToString();
                if (File.Exists(DBimageFileName))
                {
                    Image_Form.show_image(DBimageFileName);
                    Image_Form.ShowDialog();
                }
            }
            else if (e.ColumnIndex == 6)
            {
                DBimageFileName = dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex+1].Value.ToString();
                if (File.Exists(DBimageFileName))
                {
                    Image_Form.show_image(DBimageFileName);
                    Image_Form.ShowDialog();
                }
            }
            else if (e.ColumnIndex == 8)
            {
                DBimageFileName = dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex+1].Value.ToString();
                if (File.Exists(DBimageFileName))
                {
                    Image_Form.show_image(DBimageFileName);
                    Image_Form.ShowDialog();
                }
            }
            else if (e.ColumnIndex == 10)
            {
                DBimageFileName = dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex+1].Value.ToString();
                if (File.Exists(DBimageFileName))
                {
                    Image_Form.show_image(DBimageFileName);
                    Image_Form.ShowDialog();
                }
            }
            else if (e.ColumnIndex == 12)
            {
                DBimageFileName = dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex+1].Value.ToString();
                if (File.Exists(DBimageFileName))
                {
                    Image_Form.show_image(DBimageFileName);
                    Image_Form.ShowDialog();
                }
            }
            else if (e.ColumnIndex == 14)
            {
                DBimageFileName = dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex+1].Value.ToString();
                if (File.Exists(DBimageFileName))
                {
                    Image_Form.show_image(DBimageFileName);
                    Image_Form.ShowDialog();
                }
            }
        }

        private void dataGridView1_CellMouseUp(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                dataGridView1.Rows[e.RowIndex].Selected = true;
                SelectedRowIndex = e.RowIndex;
                dataGridView1.CurrentCell = dataGridView1.Rows[e.RowIndex].Cells[1];
                contextMenuStrip1.Show(dataGridView1, e.Location);
                contextMenuStrip1.Show(Cursor.Position);
            }
        }

        private void contextMenuStrip1_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Delete this data?", "Confirmation", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
            {
                if (!dataGridView1.Rows[SelectedRowIndex].IsNewRow)
                {
                    DateTime date = Convert.ToDateTime(dataGridView1.Rows[SelectedRowIndex].Cells[0].Value.ToString());
                    string time = dataGridView1.Rows[SelectedRowIndex].Cells[1].Value.ToString();
                    Console.WriteLine(date);

                    SQLiteCommand DBCommand;
                    DBSQLConnection.Open();
                    DBCommand = new SQLiteCommand(DBSQLConnection);
                    DBCommand.CommandText = "DELETE FROM Traceability_Table WHERE Date = @Date AND Time = @Time";

                    DBCommand.Parameters.AddWithValue("@Date", date.ToString("yyyy-MM-dd"));
                    DBCommand.Parameters.AddWithValue("@Time", time);
                    DBCommand.Prepare();

                    DBCommand.ExecuteNonQuery();
                    DBSQLConnection.Close();
                    Console.WriteLine("DB Delete Row");

                    DBReadData(DBSQLConnection);
                    MessageBox.Show("Data has been deleted successfully.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }                         
        }

        private void button9_Click(object sender, EventArgs e)
        {
            foreach( System.Diagnostics.Process process in System.Diagnostics.Process.GetProcessesByName("osk"))
            {
                process.Kill();
            }
            DBFilterData(DBSQLConnection);
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox1.Text == "Date")
            {
                textBox4.Visible = false;
                label19.Text = "From";
                label20.Visible = true;
                dateTimePicker1.Value = DateTime.Now;
                dateTimePicker2.Value = DateTime.Now;
                dateTimePicker1.Visible = true;
                dateTimePicker2.Visible = true;
            }
            else
            {
                dateTimePicker1.Visible = false;
                dateTimePicker2.Visible = false;
                label19.Text = "Keyword";
                label20.Visible = false;
                textBox4.Visible = true;
            }
        }

        private void textBox4_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("osk.exe");    //show virtual keyboard
            textBox4.Select();
        }

        private void textBox4_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                foreach (System.Diagnostics.Process process in System.Diagnostics.Process.GetProcessesByName("osk"))
                {
                    process.Kill();
                }
                DBFilterData(DBSQLConnection);
            }
        }

        private void pictureBox7_Click(object sender, EventArgs e)
        {
            if (ImageFilenameOCV != "" && ImageFilenameOCV != null)
            {
                Image_Form.show_image(ImageFilenameOCV);
                Image_Form.ShowDialog();
            }
        }

        private void pictureBox8_Click(object sender, EventArgs e)
        {
            if (ImageFilenameTestbenchStamp != "" && ImageFilenameTestbenchStamp != null)
            {
                Image_Form.show_image(ImageFilenameTestbenchStamp);
                Image_Form.ShowDialog();
            }
        }

        private void button10_Click(object sender, EventArgs e)
        {
            SQLiteCommand DBCommand;
            DBSQLConnection.Open();
            DBCommand = DBSQLConnection.CreateCommand();
            DBCommand.CommandText = "ALTER TABLE Tracebility_Table ADD OCV_Filename VARCHAR AFTER Engine_Type";
            //" ADD OCV_Filename VARCHAR ADD Testbench_Stamp_Status VARCHAR ADD Testbench_Stamp_Filename VARCHAR BEFORE Switch_Oli_Status";
            DBCommand.ExecuteNonQuery();
            DBSQLConnection.Close();
        }

        private void button11_Click(object sender, EventArgs e)
        {
            tabControl1.SelectedIndex = 2;
        }
    }
}
