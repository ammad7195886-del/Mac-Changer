using System;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace SimpleMACChanger
{
    public partial class MainWindow : Window
    {
        private readonly Random _random = new Random();

        private NetworkInterface? _adapter;
        private string _originalMac = "";
        private string _newMac = "";

        private const string NetworkClassGuid =
            "4d36e972-e325-11ce-bfc1-08002be10318";

        public MainWindow()
        {
            InitializeComponent();

            PositionWindowBottomCenter();

            Loaded += MainWindow_Loaded;
        }

        private void PositionWindowBottomCenter()
        {
            Left = (SystemParameters.WorkArea.Width - Width) / 2;

            Top = SystemParameters.WorkArea.Bottom - Height - 18;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            DetectAdapter();
        }

        // ============================================================
        // FIND ACTIVE WI-FI ADAPTER
        // ============================================================

        private void DetectAdapter()
        {
            try
            {
                _adapter = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n =>
                        n.OperationalStatus == OperationalStatus.Up &&
                        n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                    .FirstOrDefault();

                // Fallback to any active non-loopback adapter.
                if (_adapter == null)
                {
                    _adapter = NetworkInterface.GetAllNetworkInterfaces()
                        .Where(n =>
                            n.OperationalStatus == OperationalStatus.Up &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                        .FirstOrDefault();
                }

                if (_adapter == null)
                {
                    AdapterNameText.Text = "No active adapter found";
                    StatusText.Text = "●  NO ADAPTER";
                    StatusText.Foreground =
                        new SolidColorBrush(Color.FromRgb(255, 100, 100));

                    return;
                }

                _originalMac = FormatMac(
                    _adapter.GetPhysicalAddress().GetAddressBytes());

                string description = _adapter.Description;

                AdapterNameText.Text =
                    $"{_adapter.Name} - {description}";

                OldMacText.Text = _originalMac;
                CurrentMacText.Text = _originalMac;

                StatusText.Text = "●  CONNECTED";
                StatusText.Foreground =
                    new SolidColorBrush(Color.FromRgb(99, 232, 174));
            }
            catch (Exception ex)
            {
                StatusText.Text = "●  ERROR";

                MessageBox.Show(
                    "Unable to detect the network adapter.\n\n" +
                    ex.Message,
                    "Simple MAC Changer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ============================================================
        // GENERATE LOCALLY ADMINISTERED MAC
        // ============================================================

        private string GenerateMac()
        {
            byte[] mac = new byte[6];

            // 02 = locally administered + unicast.
            mac[0] = 0x02;

            for (int i = 1; i < 6; i++)
            {
                mac[i] = (byte)_random.Next(0, 256);
            }

            return FormatMac(mac);
        }

        private string FormatMac(byte[] bytes)
        {
            return string.Join("-",
                bytes.Select(b => b.ToString("X2")));
        }

        private string RegistryMac(string mac)
        {
            return mac.Replace("-", "")
                      .Replace(":", "")
                      .ToUpperInvariant();
        }

        // ============================================================
        // FIND WINDOWS NETWORK CLASS REGISTRY KEY
        // ============================================================

        private string? FindAdapterRegistryPath()
        {
            if (_adapter == null)
                return null;

            string basePath =
                $@"SYSTEM\CurrentControlSet\Control\Class\{{{NetworkClassGuid}}}";

            using RegistryKey? baseKey =
                Registry.LocalMachine.OpenSubKey(basePath, true);

            if (baseKey == null)
                return null;

            foreach (string subName in baseKey.GetSubKeyNames())
            {
                using RegistryKey? subKey =
                    baseKey.OpenSubKey(subName, true);

                if (subKey == null)
                    continue;

                object? value =
                    subKey.GetValue("NetCfgInstanceId");

                if (value == null)
                    continue;

                string instanceId =
                    value.ToString() ?? "";

                if (instanceId.Equals(
                    _adapter.Id,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return $"{basePath}\\{subName}";
                }
            }

            return null;
        }

        // ============================================================
        // APPLY MAC OVERRIDE
        // ============================================================

        private bool ApplyMac(string mac)
        {
            string? path = FindAdapterRegistryPath();

            if (path == null)
            {
                MessageBox.Show(
                    "Windows could not find the registry entry for this adapter.\n\n" +
                    "Your Wi-Fi driver may not support the NetworkAddress override.",
                    "MAC Change",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }

            try
            {
                using RegistryKey? key =
                    Registry.LocalMachine.OpenSubKey(path, true);

                if (key == null)
                    return false;

                key.SetValue(
                    "NetworkAddress",
                    RegistryMac(mac),
                    RegistryValueKind.String);

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Windows could not apply the MAC override.\n\n" +
                    ex.Message,
                    "MAC Change",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return false;
            }
        }

        // ============================================================
        // REMOVE MAC OVERRIDE
        // ============================================================

        private bool RemoveMacOverride()
        {
            string? path = FindAdapterRegistryPath();

            if (path == null)
                return false;

            try
            {
                using RegistryKey? key =
                    Registry.LocalMachine.OpenSubKey(path, true);

                if (key == null)
                    return false;

                if (key.GetValue("NetworkAddress") != null)
                {
                    key.DeleteValue(
                        "NetworkAddress",
                        false);
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Windows could not restore the adapter.\n\n" +
                    ex.Message,
                    "Restore MAC",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return false;
            }
        }

        // ============================================================
        // RESTART NETWORK ADAPTER
        // ============================================================

        private bool RestartAdapter()
        {
            if (_adapter == null)
                return false;

            try
            {
                using ManagementObject adapter =
                    new ManagementObject(
                        $"Win32_NetworkAdapter.DeviceID='{GetDeviceId()}'");

                adapter.InvokeMethod("Disable", null);

                System.Threading.Thread.Sleep(1800);

                adapter.InvokeMethod("Enable", null);

                System.Threading.Thread.Sleep(2500);

                return true;
            }
            catch
            {
                return RestartWithPowerShell();
            }
        }

        private string GetDeviceId()
        {
            try
            {
                using ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(
                        "SELECT DeviceID, NetConnectionID FROM Win32_NetworkAdapter");

                foreach (ManagementObject obj in searcher.Get())
                {
                    string? connection =
                        obj["NetConnectionID"]?.ToString();

                    if (!string.IsNullOrEmpty(connection) &&
                        connection.Equals(
                            _adapter?.Name,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return obj["DeviceID"]?.ToString() ?? "";
                    }
                }
            }
            catch
            {
            }

            return "";
        }

        private bool RestartWithPowerShell()
        {
            try
            {
                string adapterName =
                    _adapter?.Name ?? "";

                if (string.IsNullOrWhiteSpace(adapterName))
                    return false;

                string escaped =
                    adapterName.Replace("'", "''");

                using var process =
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments =
                                $"-NoProfile -ExecutionPolicy Bypass -Command " +
                                $"\"Disable-NetAdapter -Name '{escaped}' -Confirm:$false; " +
                                $"Start-Sleep -Seconds 2; " +
                                $"Enable-NetAdapter -Name '{escaped}' -Confirm:$false\"",
                            UseShellExecute = true,
                            Verb = "runas",
                            CreateNoWindow = true,
                            WindowStyle =
                                System.Diagnostics.ProcessWindowStyle.Hidden
                        });

                process?.WaitForExit();

                System.Threading.Thread.Sleep(2500);

                return true;
            }
            catch
            {
                return false;
            }
        }

        // ============================================================
        // READ CURRENT MAC AFTER RESTART
        // ============================================================

        private string ReadCurrentMac()
        {
            try
            {
                if (_adapter == null)
                    return "--";

                NetworkInterface? refreshed =
                    NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.Id == _adapter.Id);

                if (refreshed != null)
                {
                    _adapter = refreshed;

                    return FormatMac(
                        refreshed.GetPhysicalAddress()
                            .GetAddressBytes());
                }
            }
            catch
            {
            }

            return "--";
        }

        // ============================================================
        // GENERATE BUTTON
        // ============================================================

        private void Generate_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_adapter == null)
            {
                MessageBox.Show(
                    "No active Wi-Fi adapter was found.",
                    "Simple MAC Changer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            try
            {
                _newMac = GenerateMac();

                NewMacText.Text = _newMac;
                NewMacText.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(157, 98, 255));

                bool applied = ApplyMac(_newMac);

                if (!applied)
                    return;

                StatusText.Text = "●  APPLYING";

                StatusText.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(255, 190, 70));

                RestartAdapter();

                string actualMac = ReadCurrentMac();

                CurrentMacText.Text = actualMac;

                if (actualMac.Equals(
                    _newMac,
                    StringComparison.OrdinalIgnoreCase))
                {
                    StatusText.Text = "●  CONNECTED";

                    StatusText.Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(99, 232, 174));

                    try
                    {
                        Clipboard.SetText(_newMac);
                    }
                    catch
                    {
                    }
                }
                else
                {
                    StatusText.Text = "●  DRIVER CHECK";

                    StatusText.Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(255, 190, 70));

                    MessageBox.Show(
                        "Windows accepted the adapter setting, but the driver " +
                        "did not report the requested MAC address.\n\n" +
                        "This Wi-Fi driver may not support MAC overrides.",
                        "MAC Change",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "●  ERROR";

                MessageBox.Show(
                    "MAC change failed.\n\n" +
                    ex.Message,
                    "Simple MAC Changer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ============================================================
        // RESTORE BUTTON
        // ============================================================

        private void Restore_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_adapter == null)
                return;

            try
            {
                bool removed = RemoveMacOverride();

                if (!removed)
                {
                    MessageBox.Show(
                        "No MAC override could be removed.",
                        "Restore MAC",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                StatusText.Text = "●  RESTORING";

                StatusText.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(255, 190, 70));

                RestartAdapter();

                string actualMac = ReadCurrentMac();

                CurrentMacText.Text = actualMac;

                NewMacText.Text = "Not generated yet";

                NewMacText.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(141, 155, 181));

                StatusText.Text = "●  CONNECTED";

                StatusText.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(99, 232, 174));
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Restore failed.\n\n" +
                    ex.Message,
                    "Restore MAC",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ============================================================
        // COPY
        // ============================================================

        private void Copy_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_newMac))
                return;

            try
            {
                Clipboard.SetText(_newMac);
            }
            catch
            {
            }
        }

        // ============================================================
        // WINDOW CONTROLS
        // ============================================================

        private void Close_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }

        private void Minimize_Click(
            object sender,
            RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(
            object sender,
            RoutedEventArgs e)
        {
            // Fixed compact window.
            // Intentionally does nothing.
        }

        private void TitleBar_MouseDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try
                {
                    DragMove();
                }
                catch
                {
                }
            }
        }
    }
}
