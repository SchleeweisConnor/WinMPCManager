using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;

namespace MP_X_Manager_App
{
    public partial class MainWindow : Window
    {
        private string[] Bin;
        private IList<string> DevicePaths = new List<string>();
        public ObservableCollection<Process> ProcList { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            ProcList = new ObservableCollection<Process>();
        }

        private void MakeButton_Click(object Sender, RoutedEventArgs E)
        {
            if (QuantityBox.Text.Length == 0)
                return;
            else if (int.Parse(QuantityBox.Text) > 999)
            {
                MessageBox.Show("Too many devices!");
                return;
            }

            foreach (char C in QuantityBox.Text)
                if (!char.IsDigit(C))
                {
                    MessageBox.Show("Only positive integers may be entered for device quantity: 1, 2, 3...");
                    return;
                }

            for (int I = 1; I <= int.Parse(QuantityBox.Text); I++)
            {
                string SubPath = $"c:\\_Temp\\_{I:D3}";

                if (!Directory.Exists(SubPath))
                {
                    try
                    {
                        DirectoryInfo Dir = Directory.CreateDirectory(SubPath);
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
            if (PrecursorBox.Text.Length == 0)
                return;

            foreach (char C in PrecursorBox.Text)
                if (!char.IsDigit(C))
                {
                    MessageBox.Show("Only positive integers may be entered for the ID precursor: 1, 2, 3...");
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
                    ProcList.Add(MPX);
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
            if (ProcList.Count > 0)
                foreach (Process Proc in ProcList)
                    Proc.Kill();

            ProcList.Clear();
            DevicePaths.Clear();
            StartButton.IsEnabled = StopButton.IsEnabled = PrecursorBox.IsEnabled = false;
            MakeButton.IsEnabled = QuantityBox.IsEnabled = true;
        }

        private void SynchButton_Click(object Sender, RoutedEventArgs E)
        {

        }
    }
}
