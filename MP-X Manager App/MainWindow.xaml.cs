using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;

namespace MP_X_Manager_App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string[] Bin;
        private IList<string> DevicePaths = new List<string>();
        //private IList<Process> procList = new List<Process>();
        private ObservableCollection<Process> ViewBinding { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            ViewBinding = new ObservableCollection<Process>();
        }

        private void MakeButton_Click(object Sender, RoutedEventArgs E)
        {
            foreach (char C in QuantityBox.Text)
                if (!char.IsDigit(C))
                {
                    MessageBox.Show("Only positive integer values may be entered for device quantity: 1, 2, 3...");
                    return;
                }
            
            if (int.Parse(QuantityBox.Text) > 999)
            {
                MessageBox.Show("Too many devices!");
                return;
            }

            for (int I = 1; I <= int.Parse(QuantityBox.Text); I++)
            {
                string SubPath = $"c:\\_Temp\\_{I:D3}";
                DirectoryInfo Dir;

                if (!Directory.Exists(SubPath))
                {
                    try
                    {
                        Dir = Directory.CreateDirectory(SubPath);
                    }
                    catch (Exception Ex)
                    {
                        MessageBox.Show($"Failed to create device #{I}: {Ex.Message}");

                        return;
                    }
                }
                TrashRemoval(SubPath, "*.exe");
                TrashRemoval(SubPath, "*.dll");

                RebuildDevices(SubPath, "*.exe");
                RebuildDevices(SubPath, "*.dll");
                DevicePaths.Add(Path.Combine(SubPath, "mpc.exe"));
            }
                QuantityBox.Clear();
                MakeButton.IsEnabled = QuantityBox.IsEnabled = false;
                PrecursorBox.IsEnabled = StartButton.IsEnabled = StopButton.IsEnabled = true;
                return;
        }

        private void TrashRemoval(string SubPath, string Ext)
        {
            Bin = Directory.GetFiles(SubPath, Ext);
            if (Bin.Length > 0)
                foreach (string Dir in Bin)
                {
                    File.SetAttributes(Dir, FileAttributes.Normal);
                    File.Delete(Dir);
                }
        }

        private void RebuildDevices(string SubPath, string Ext)
        {
            Bin = Directory.GetFiles("c:\\_Temp\\_0", Ext);
            string NewFile;
            foreach (string Dir in Bin)
            {
                NewFile = Path.Combine(SubPath, Path.GetFileName(Dir));
                if (!File.Exists(NewFile))
                {
                    File.Copy(Dir, NewFile);
                    File.SetAttributes(NewFile, FileAttributes.Normal);
                }
            }
        }

        private void StartButton_Click(object Sender, RoutedEventArgs E)
        {
            foreach (char C in PrecursorBox.Text)
                if (!char.IsDigit(C))
                {
                    MessageBox.Show("Only positive integer values may be entered for the ID precursor: 1, 2, 3...");
                    return;
                }

            foreach (string Path in DevicePaths)
            {
                ProcessStartInfo StartInfo = new ProcessStartInfo()
                {
                FileName = Path,
                Arguments = $"--id={int.Parse(PrecursorBox.Text)}{DevicePaths.IndexOf(Path) + 1} --webport=0",

                RedirectStandardError = true,
                RedirectStandardOutput = true,

                UseShellExecute = false,
                CreateNoWindow = true,
                };

                try
                {
                    Process MPX = Process.Start(StartInfo);
                    new Thread(() => { MPX.WaitForExit(); }).Start();
                    ViewBinding.Add(MPX);
                }
                catch (Exception Ex)
                {
                    MessageBox.Show($"Device #{DevicePaths.IndexOf(Path) + 1} failed to start: {Ex.Message}");
                    throw new OperationCanceledException();
                }
            }
            PrecursorBox.Clear();
            StartButton.IsEnabled = PrecursorBox.IsEnabled = false;
            return;
        }

        private void DataHandler(Object Sender, DataReceivedEventArgs E)
        {
            if (!Dispatcher.CheckAccess())
            {
                object[] Args = { Sender, E };
                Dispatcher.Invoke(new DataReceivedEventHandler(DataHandler), Args);
            }
            else if (!string.IsNullOrEmpty(E.Data))
            {
                ConsoleBox.Text += ("> " + E.Data + Environment.NewLine);
                ConsoleBox.Focus();
                ConsoleBox.CaretIndex = ConsoleBox.Text.Length;
                ConsoleBox.ScrollToEnd();
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewBinding.Count > 0)
            {
                foreach (Process Proc in ViewBinding)
                    Proc.Kill();
                ViewBinding.Clear();
            }
            if (DevicePaths.Count > 0)
            {
                string Device;
                foreach (string Dir in DevicePaths)
                {
                    Device = Path.GetDirectoryName(Dir);
                    Directory.Delete(Device, true);
                }
            }

            DevicePaths.Clear();
            StartButton.IsEnabled = StopButton.IsEnabled = false;
            MakeButton.IsEnabled = QuantityBox.IsEnabled = true;
        }

        private void SynchButton_Click(object Sender, RoutedEventArgs E)
        {

        }
    }
}
