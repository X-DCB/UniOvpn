using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.IO;
using System.Text.RegularExpressions;
using System.Net.Sockets;
using System.Diagnostics;
using System.Timers;
using c_timer;
using net_stat;

namespace UniOvpn
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        #region Variables
        string host = "127.0.0.1";
        int port = 25350;
        sock_c sockx;
        Process procx = new Process();
        readonly string exepath = "bin\\openvpn.exe";
        string locx = Environment.GetEnvironmentVariable("AppData") + "\\UniOvpn";
        OpenFileDialog[] xopen = new OpenFileDialog[2];
        string cmdx;
        Regex[] match = { new Regex("Start.+for.+"), null };
        string prev_text = "";
        timer timerz = new timer();
        stats nstat = new stats();
        #endregion
        partial class sock_c : Socket
        {
            static public bool connected = false;
            public delegate void stat(MainWindow x = null);
            public event stat disconnect = (MainWindow x) => connected = false;
            public event stat connected_ = (MainWindow x) => connected = true;
            public sock_c(MainWindow x, string host = "127.0.0.1", int port = 25350) : base(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                x.procx.Start();
                this.Connect(host, port);
                connected_(x);
            }
            public void lose(MainWindow x)
            {
                disconnect(x);
            }
            public void sendm(string cmd)
            {
                if (connected) Send(Encoding.Default.GetBytes(cmd + "\r\n"));
            }
        }
        private void import_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog open = new OpenFileDialog();
            open.DefaultExt = "ovpn";
            if (open.ShowDialog().Value)
            {
                xopen[0] = open;
                get_details(false);
            }
        }

        string[] parse_details()
        {
            OpenFileDialog loc = xopen[0];
            string cont2 = File.ReadAllText(loc.FileName);
            MatchCollection res = new Regex("<.+").Matches(cont2);
            string[] outx = new string[10];
            for (int i = 0; i < res.Count; i++)
            {
                if (i % 2 == 0)
                {
                    int start = cont2.IndexOf(res[i].Value);
                    int end = cont2.IndexOf(res[i + 1].Value) - start + res[i + 1].Value.Length;
                    string output = cont2.Substring(start, end);
                    switch (res[i].Value)
                    {
                        case "<auth-user-pass>":
                            outx[1] = output; break;
                        case "<ca>":
                            outx[2] = output; break;
                        case "<tls-auth>":
                            outx[3] = output; break;
                        case "<cert>":
                            outx[4] = output; break;
                    }
                }
            }
            outx[0] = string.Join("\n", File.ReadAllLines(loc.FileName).Where(x => !string.Join("\n", outx).Contains(x)));
            return outx;
        }
        private string[] get_details(bool cre)
        {
            if (xopen[0] == null) return new string[] { };
            OpenFileDialog loc = xopen[0];
            string[] outx = parse_details();
            if (string.IsNullOrEmpty(outx[1]) && (outx[3] == null || outx[4] == null))
            {
                logx("Config must be in Android format.");
                if (!string.IsNullOrEmpty(config.Text))
                {
                    logx("Using the previously imported config.");
                    xopen[0] = xopen[1];
                    outx = parse_details();
                }
            }
            else
            {
                xopen[1] = xopen[0];
                config.Text = loc.FileName;
                Registry.CurrentUser.CreateSubKey("Software\\JustPlay\\UniOvpn").SetValue("CurrentConfig", loc.FileName, RegistryValueKind.String);
                if (!cre) logx(loc.SafeFileName + " [ Successfully imported. ]");
            }
            if (!Directory.Exists(locx)) { Directory.CreateDirectory(locx); }
            if (cre)
            {
                File.WriteAllText(locx + "\\shit-1.ovpn", string.Join("\n", outx.Where(x => x != outx[1])).Trim());
                cmdx = "--config " + locx + "\\shit-1.ovpn";
                if (outx[1] != null)
                {
                    File.WriteAllText(locx + "\\creds.txt", string.Format("{1}\n{2}", new Regex("\n").Split(outx[1])));
                    cmdx += " --auth-user-pass " + locx + "\\creds.txt";
                };
            }
            return new string[] { };
        }
        void logx(string msg)
        {
            Regex regdns = new Regex("End.+for.+");
            if (msg.IndexOf("Initialization Sequence Completed") > 0) timerz.strt();
            if (regdns.IsMatch(msg) && msg != prev_text)
            {
                var splt = regdns.Match(msg).Value.Split(' ');
                msg = regdns.Replace(msg, splt[splt.Length - 1].Replace("...", "") + " finished.");
            }
            else
            {
                if (match[0].IsMatch(msg))
                {
                    match[1] = new Regex(match[0].Match(msg).Value.Split(' ')[1]);
                }
                if (match[1] != null)
                {
                    if (match[1].IsMatch(msg)) msg = "";
                }
            }
            response.AppendText(msg + (msg != "" ? "\n" : ""));
            response.ScrollToEnd();
        }

        void start_proc()
        {
            procx.StartInfo = new ProcessStartInfo(
                exepath,
                string.Format(
                    "--management {0} {1} {2}",
                    host, port, cmdx))
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true
            };
            procx.EnableRaisingEvents = true;
            procx.Exited += (sendz, zzx) =>
            {
                procx.CancelOutputRead();
                timerz.stp("");
                this.Dispatcher.Invoke(new Action(() => {
                    connect.Content = "Connect";
                    connect.IsEnabled = true;
                    import.IsEnabled = true;
                    nstat.stop();
                }));
            };
            procx.OutputDataReceived += (sendx, exz) => {
                if (!string.IsNullOrEmpty(exz.Data))
                {
                    response.Dispatcher.Invoke(new Action(() => {
                        Directory.EnumerateFiles(locx).ToList().ForEach(x => File.Delete(x));
                        if (!response.Text.Contains(exz.Data))
                        {
                            logx(exz.Data);
                            prev_text = exz.Data;
                        };
                    }));
                }
            };
            nstat.byte_event += (recv, sent) => {
                Dispatcher.Invoke(new Action(() => {
                    received_l.Content = recv;
                    sent_l.Content = sent;
                }));
            };
            timerz.time += (x) =>
            {
                Dispatcher.Invoke(new Action(() => timerx.Content = x));
            };
            sockx = new sock_c(this, host, port);
            sockx.connected_ += (x) => sendConfigs();
            procx.BeginOutputReadLine();
            nstat.start();
        }
        void sendCommand(string cmd)
        {
            if (!import.IsEnabled) sockx.sendm(cmd);
        }

        void greetingx()
        {
            var buf = new byte[1024];
            int res = sockx.Receive(buf, 0, buf.Length, SocketFlags.None);
            //response.Text = Encoding.UTF8.GetString(buf).Replace("\0", "");
        }
        void sendConfigs()
        {
            sendCommand("verb 3");
            sendCommand("log all on");
            sendCommand("echo all on");
            sendCommand("bytecount 5");
            sendCommand("hold off");
            sendCommand("hold release");
            sendCommand("state on");
            /*sendCommand("config shit-1.ovpn");
            sendCommand("auth-user-pass creds.txt");*/
        }
        private void connect_Click(object sender, RoutedEventArgs e)
        {
            if (config.Text == "")
            { logx("Please select a file to import."); }
            else
            {
                if (connect.Content.ToString() == "Connect")
                {
                    if (Process.GetProcessesByName("openvpn").Count() > 0)
                    { logx("Please stop an existing openvpn connection."); }
                    else
                    {
                        connect.Content = "Disconnect";
                        import.IsEnabled = false;
                        get_details(true);
                        start_proc();
                    }
                }
                else
                {
                    connect.IsEnabled = false;
                    connect.Content = "Disconnecting...";
                    disconnect();
                }
            }
        }
        void disconnect()
        {
            sendCommand("signal SIGTERM");
        }
        private void errh(object sender, UnhandledExceptionEventArgs y)
        {
            string ErrLoc = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\UniOvpnErr.log";
            File.WriteAllText(ErrLoc, y.ExceptionObject.ToString());
            MessageBox.Show("Please send the error log (" + ErrLoc + ") to the Creator.", "UniOvpn Error");
            Environment.Exit(1);
        }
        private void loader(object sender, RoutedEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(errh);
            if (Process.GetProcessesByName("UniOvpn").Count() > 1)
            {
                MessageBox.Show("Application is alreay running.");
                this.Close();
            }
            RegistryKey susi = Registry.CurrentUser.OpenSubKey("Software\\JustPlay\\UniOvpn");
            if (susi != null)
            {
                xopen[0] = new OpenFileDialog()
                {
                    FileName = susi.GetValue("CurrentConfig").ToString()
                };
                get_details(false);
            }
        }

        private void closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = !import.IsEnabled;
            disconnect();
            import.IsEnabledChanged += (x, y) => {
                if ((bool)y.NewValue)
                {
                    Environment.Exit(0);
                }
            };
        }
    }
}
