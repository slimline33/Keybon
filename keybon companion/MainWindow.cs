using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO.Ports;
using System.IO;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;
using System.Drawing.Imaging;
using System.Configuration;

namespace keybon
{
    public partial class MainWindow : Form
    {
        private NotifyIcon notifyIcon; // NotifyIcon für Systemtray
        private bool balloonTipShown = false; // Verhindert mehrfaches Anzeigen des BalloonTips
        String portName = "COM14";
        public SerialPort _serialPort;
        public ScreenLayout[] Layouts = new ScreenLayout[8];

        int selectedButton = 0;
        int currentLayout = 0;
        String currentApp;
        string[] ports;

        public int CurrentLayout
        {
            get { return currentLayout; }
            set
            {
                if (!value.Equals(currentLayout))
                {
                    currentLayout = value;
                    UpdateLayoutVisibility(currentLayout != 0);
                }
            }
        }

        public String CurrentApp
        {
            get { return currentApp; }
            set
            {
                if (!value.Equals(currentApp) && !value.Equals("keybon companion"))
                {
                    currentApp = value;
                    int nextLayout = Layouts.ToList().FindIndex(layout => layout.Apps.Contains(currentApp));
                    switchToLayout(nextLayout >= 0 ? nextLayout : 0);
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            // Fenster nicht in der Taskleiste anzeigen
            this.ShowInTaskbar = false;

            // NotifyIcon initialisieren
            try
            {
                notifyIcon = new NotifyIcon
                {
                    Icon = new Icon("path-to-icon.ico"), // Pfad zu deinem Icon
                    Visible = true,
                    Text = "Keybon Companion"
                };
                notifyIcon.MouseDoubleClick += NotifyIcon_MouseDoubleClick;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden des Icons: {ex.Message}", "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // Minimierungsereignis registrieren
            this.Resize += MainWindow_Resize;

            InitializeComponents();
        }

        private void MainWindow_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized && !balloonTipShown)
            {
                this.Hide();
                notifyIcon.ShowBalloonTip(1000, "Keybon Companion", "Das Programm wurde in den Hintergrund verschoben.", ToolTipIcon.Info);
                balloonTipShown = true;
            }
            else if (this.WindowState != FormWindowState.Minimized)
            {
                balloonTipShown = false;
            }
        }

        private void NotifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        private void InitializeComponents()
        {
            pictureBox01.AllowDrop = true;
            pictureBox02.AllowDrop = true;
            pictureBox03.AllowDrop = true;
            pictureBox04.AllowDrop = true;
            pictureBox05.AllowDrop = true;
            pictureBox06.AllowDrop = true;
            pictureBox07.AllowDrop = true;
            pictureBox08.AllowDrop = true;
            pictureBox09.AllowDrop = true;

            for (int i = 0; i < Layouts.Length; i++)
            {
                Layouts[i] = new ScreenLayout { name = $"Layout {i}" };
            }
            Layouts[0].name = "Default";

            comboBox1.DataSource = Layouts;
            comboBox1.DisplayMember = "Name";

            loadSettings();

            // SerialPort initialisieren
            _serialPort = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One)
            {
                DtrEnable = true
            };
            ports = SerialPort.GetPortNames();
            comboBox2.DataSource = ports;

            if (ports.Contains(portName))
            {
                comboBox2.SelectedItem = portName;
                try
                {
                    _serialPort.Open();
                }
                catch { }
            }
            _serialPort.DataReceived += portDataReceived;

            Timer timer1 = new Timer { Interval = 250 };
            timer1.Tick += OnTimerEvent;
            timer1.Enabled = true;
        }

        private void UpdateLayoutVisibility(bool visible)
        {
            pictureBox01.Visible = visible;
            pictureBox02.Visible = visible;
            pictureBox03.Visible = visible;
            pictureBox04.Visible = visible;
            pictureBox05.Visible = visible;
            pictureBox06.Visible = visible;
            pictureBox07.Visible = visible;
            pictureBox08.Visible = visible;
            pictureBox09.Visible = visible;
            AddBox.Enabled = visible;
            RemoveBox.Enabled = visible;
            hotkeyBox.Enabled = visible;
            listBox1.Enabled = visible;
        }

        private void switchToLayout(int layoutNum)
        {
            CurrentLayout = layoutNum;
            UpdatePictureBoxes();
            listBox1.DataSource = Layouts[currentLayout].Apps;
            comboBox1.SelectedIndex = layoutNum;
            hotkeyBox.Text = Layouts[currentLayout].keyCommand[selectedButton];

            if (layoutNum == 0)
            {
                SendCommandToSerial((Byte)'D');
            }
            else
            {
                Layouts[currentLayout].drawAll(_serialPort);
            }
        }

        private void UpdatePictureBoxes()
        {
            pictureBox01.Image = Layouts[currentLayout].oleds[0];
            pictureBox02.Image = Layouts[currentLayout].oleds[1];
            pictureBox03.Image = Layouts[currentLayout].oleds[2];
            pictureBox04.Image = Layouts[currentLayout].oleds[3];
            pictureBox05.Image = Layouts[currentLayout].oleds[4];
            pictureBox06.Image = Layouts[currentLayout].oleds[5];
            pictureBox07.Image = Layouts[currentLayout].oleds[6];
            pictureBox08.Image = Layouts[currentLayout].oleds[7];
            pictureBox09.Image = Layouts[currentLayout].oleds[8];
        }

        private void portDataReceived(object sender, EventArgs args)
        {
            SerialPort port = sender as SerialPort;
            if (port == null) return;

            try
            {
                int keyReceived = _serialPort.ReadChar();
                SendKeys.SendWait(Layouts[currentLayout].keyCommand[keyReceived - 49]);
            }
            catch { }
        }

        private void OnTimerEvent(object sender, EventArgs e)
        {
            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();
            GetWindowText(handle, Buff, nChars);
            Process[] AllProcess = Process.GetProcesses();

            foreach (Process pro in AllProcess)
            {
                if (!string.IsNullOrEmpty(pro.MainWindowTitle) && Buff.ToString().Equals(pro.MainWindowTitle))
                {
                    CurrentApp = pro.ProcessName;
                    break;
                }
            }
        }

        private void loadSettings()
        {
            Properties.Settings.Default.Reload();
            if (!string.IsNullOrEmpty(Properties.Settings.Default.portName)) portName = Properties.Settings.Default.portName;

            for (int i = 1; i < Layouts.Length; i++)
            {
                var layout = typeof(Properties.Settings.Default).GetProperty($"Layout{i}")?.GetValue(Properties.Settings.Default);
                if (layout != null) Layouts[i] = (ScreenLayout)layout;
            }
        }

        private void SendCommandToSerial(byte command)
        {
            try
            {
                if (_serialPort?.IsOpen == true)
                {
                    _serialPort.Write(new byte[] { command }, 0, 1);
                }
            }
            catch { }
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    }

    [SettingsSerializeAs(SettingsSerializeAs.Xml)]
    public class ScreenLayout
    {
        [XmlIgnore]
        public Bitmap[] oleds = new Bitmap[9];
        public string[] keyCommand = new string[9];
        public List<string> Apps = new List<string>();
        public string name { get; set; }

        public ScreenLayout()
        {
            for (int i = 0; i < oleds.Length; i++)
            {
                oleds[i] = new Bitmap(64, 48);
                keyCommand[i] = $"{{{i + 1}}}";
            }
        }

        public void drawAll(SerialPort _serialPort)
        {
            for (int i = 0; i < oleds.Length; i++)
            {
                // Logic to send image to Serial
            }
        }
    }
}
