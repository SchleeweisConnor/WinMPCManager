using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace WinMPC_Manager
{
    public partial class MainWindow : Window
    {
        private ApplicationSettings AppSettings = new ApplicationSettings();
        public static DeviceCollection Devices;
        private static string MainDir = Directory.GetCurrentDirectory();
        private static string AppFiles = Path.Combine(MainDir, @"AppFiles");
        private static string DeviceDir = Path.Combine(MainDir, @"Devices");

        public MainWindow()
        {
            InitializeComponent();
        }

        private void MakeButton_Click(object Sender, RoutedEventArgs E)
        {
            if (QuantityBox.Text.Length == 0)
            {
                System.Windows.MessageBox.Show("You must enter a device quantity.");
                return;
            }
            foreach (char C in QuantityBox.Text)
                if (!char.IsDigit(C))
                {
                    System.Windows.MessageBox.Show("Only positive integers may be entered for device quantity: 1, 2, 3...");
                    return;
                }

            for (int I = 1; I <= int.Parse(QuantityBox.Text); I++)
            {
                string SubPath = Path.Combine(DeviceDir, $"_{I:D3}");

                if (!Directory.Exists(SubPath))
                {
                    try
                    {
                        DirectoryInfo Dir = Directory.CreateDirectory(SubPath);
                    }
                    catch (Exception Ex)
                    {
                        System.Windows.MessageBox.Show($"Failed to create device #{I}: {Ex.Message}");
                        return;
                    }
                }

                if (ClearCheck.IsChecked == true)
                    RemoveAll(SubPath, "*");
                else
                {
                    RemoveAll(SubPath, "*.exe");
                    RemoveAll(SubPath, "*.dll");
                }

                BuildDevices(AppFiles, SubPath, "*.exe");
                BuildDevices(AppFiles, SubPath, "*.dll");
            }
            return;
        }

        private void RemoveAll(string SubPath, string Ext)
        {
            string[] Bin = Directory.GetFiles(SubPath, Ext);
            if (Bin.Length > 0)
                foreach (string Dir in Bin)
                {
                    File.SetAttributes(Dir, FileAttributes.Normal);
                    File.Delete(Dir);
                }
        }

        private void BuildDevices(string Source, string DestinationSubPath, string Ext)
        {
            string[] Bin = Directory.GetFiles(Source, Ext);
            string NewFile;
            foreach (string Dir in Bin)
            {
                NewFile = Path.Combine(DestinationSubPath, Path.GetFileName(Dir));
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
            {
                System.Windows.MessageBox.Show("You must enter a BACnet ID precursor before you try to start devices.");
                return;
            }
            if (QuantityBox.Text.Length == 0)
            {
                System.Windows.MessageBox.Show("You must enter a device quantity.");
                return;
            }
            foreach (char C in PrecursorBox.Text)
                if (!char.IsDigit(C))
                {
                    System.Windows.MessageBox.Show("Only positive integers may be entered for the ID precursor: 1, 2, 3...");
                    return;
                }

            Devices = new DeviceCollection();
            for (int J = 1; J <= int.Parse(QuantityBox.Text); J++)
            {
                try
                {
                    Devices.Add(new MPX(Path.GetFullPath($@"{DeviceDir}\_{J:D3}\mpc.exe")));
                } catch
                {
                    break;
                }
            }

            int PreID = int.Parse(PrecursorBox.Text), I = 1;
            foreach (MPX Device in Devices.GetList())
            {
                ProcessStartInfo StartInfo = new ProcessStartInfo()
                {
                    FileName = Device.MPXPath,
                    Arguments = $"--id={PreID}{I:D3} --webport=0",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                if (ShowCheck.IsChecked == true)
                    StartInfo.CreateNoWindow = false;

                try
                {
                    Device.Proc = Process.Start(StartInfo);
                }
                catch (Exception Ex)
                {
                    System.Windows.MessageBox.Show($"Device #{I} failed to start: {Ex.Message}");
                    break;
                }
                I++;
            }
            StopButton.IsEnabled = true;
            return;
        }

        private void StopButton_Click(object Sender, RoutedEventArgs E)
        {
            if (Devices.Count > 0 && Devices != null)
                foreach (MPX Device in Devices.GetList())
                    try
                    {
                        Device.End();
                    }
                    catch
                    {
                        continue;
                    }

            StopButton.IsEnabled = false;
            Devices.Count = 0;
        }

        private void Window_Closed(object Sender, EventArgs E)
        {
            if (Devices != null && Devices.Count > 0)
                foreach (MPX Device in Devices.GetList())
                    try
                    {
                        Device.End();
                    }
                    catch
                    {
                        continue;
                    }

            if (QuantityBox.Text != null)
                AppSettings.PrevQuantity = QuantityBox.Text;
            if (PrecursorBox.Text != null)
                AppSettings.PrevBACnetID = PrecursorBox.Text;

            AppSettings.DefaultDirectory = MainDir;
            AppSettings.SaveAppSettings();
        }

        private void Window_Loaded(object Sender, RoutedEventArgs E)
        {
            if (AppSettings.LoadAppSettings())
            {
                QuantityBox.Text = AppSettings.PrevQuantity;
                PrecursorBox.Text = AppSettings.PrevBACnetID;
            }
        }

        private void SynchButton_Click(object Sender, RoutedEventArgs E)
        {
            OpenFileDialog Dialog = new OpenFileDialog()
            {
                InitialDirectory = MainDir,
                Title = "Select WinMPC",
                DefaultExt = ".exe",
                Filter = "WinMPC|mpc.exe",
            };

            if (Dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                try
                {
                    FileInfo WinMPC = new FileInfo(Dialog.FileName);
                    WinMPC.CopyTo(Path.Combine(AppFiles, "mpc.exe"), true);

                    FileInfo NewFile = new FileInfo(Path.Combine(AppFiles, "mpc.exe").ToString());
                    NewFile.CreationTime = WinMPC.CreationTime;
                    NewFile.LastWriteTime = WinMPC.LastWriteTime;
                    NewFile.LastAccessTime = WinMPC.LastAccessTime;
                } catch (IOException Ex)
                {
                    System.Windows.MessageBox.Show($"Failed to move file : {Ex.Message}");
                    return;
                }
            }
        }
    }

    public class DeviceCollection
    {
        private static IList<MPX> MPXList;
        private static int DeviceCount;

        public DeviceCollection()
        {
            MPXList = new List<MPX>();
            DeviceCount = 0;
        }

        public void Add(MPX Device)
        {
            MPXList.Add(Device);
            DeviceCount++;
        }

        public MPX GetMPX(int I)
        {
            return MPXList[I];
        }

        public IList<MPX> GetList()
        {
            return MPXList;
        }

        public IList<string> GetPaths()
        {
            IList<string> Paths = new List<string>();
            foreach (MPX Device in MPXList)
                Paths.Add(Device.MPXPath);

            return Paths;
        }

        public int Count
        {
            get { return DeviceCount; }
            set { DeviceCount = value; }
        }
    }

    public class MPX
    {
        private string DevicePath;
        private Process Device;

        public MPX(string Path)
        {
            DevicePath = Path;
        }

        public string MPXPath
        {
            get { return DevicePath; }
            set { DevicePath = value; }
        }

        public Process Proc
        {
            get { return Device; }
            set { Device = value; }
        }

        public void End()
        {
            if (Device != null)
                Device.Kill();
        }
    }

    public class ApplicationSettings
    {
        private bool AppSettingsChanged;
        private string DefDirectory;
        private string Quantity;
        private string BACnetID;

        public string DefaultDirectory
        {
            get { return DefDirectory; }
            set
            {
                if (value != DefDirectory)
                {
                    DefDirectory = value;
                    AppSettingsChanged = true;
                }
            }
        }

        public string PrevQuantity
        {
            get { return Quantity; }
            set
            {
                if (value != Quantity)
                {
                    Quantity = value;
                    AppSettingsChanged = true;
                }
            }
        }

        public string PrevBACnetID
        {
            get { return BACnetID; }
            set
            {
                if (value != BACnetID)
                {
                    BACnetID = value;
                    AppSettingsChanged = true;
                }
            }
        }

        public bool SaveAppSettings()
        {
            if (AppSettingsChanged)
            {
                StreamWriter SW = null;
                XmlSerializer XMLSZR = null;
                try
                {
                    XMLSZR = new XmlSerializer(typeof(ApplicationSettings));
                    SW = new StreamWriter(System.Windows.Forms.Application.LocalUserAppDataPath + @"\Application.config", false);

                    XMLSZR.Serialize(SW, this);
                } catch (Exception Ex)
                {
                    System.Windows.MessageBox.Show(Ex.Message);
                }
                finally
                {
                    if (SW != null)
                        SW.Close();
                }
            }
            return AppSettingsChanged;
        }

        public bool LoadAppSettings()
        {
            XmlSerializer XMLSZR = null;
            FileStream FS = null;
            bool FileExists = false;

            try
            {
                XMLSZR = new XmlSerializer(typeof(ApplicationSettings));
                FileInfo FI = new FileInfo(System.Windows.Forms.Application.LocalUserAppDataPath + @"\Application.config");
                if (FI.Exists)
                {
                    FS = FI.OpenRead();
                    ApplicationSettings AppSettings = (ApplicationSettings)XMLSZR.Deserialize(FS);

                    Quantity = AppSettings.Quantity;
                    BACnetID = AppSettings.BACnetID;
                    FileExists = true;
                }
            } catch (Exception Ex)
            {
                System.Windows.MessageBox.Show(Ex.Message);
            }
            finally
            {
                if (FS != null)
                {
                    FS.Close();
                }
            }

            if (DefDirectory == null)
            {
                DefDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                AppSettingsChanged = true;
            }
            return FileExists;
        }
    }
}