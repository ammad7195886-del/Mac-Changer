using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace SimpleMACChanger
{
    public partial class MainWindow : Window
    {
        private readonly Random _random = new Random();

        private NetworkInterface? _wifiAdapter;
        private string _originalMac = "Not available";

        private const string NetworkClassGuid =
            "4D36E972-E325-11CE-BFC1-08002BE10318";

        public MainWindow()
        {
            InitializeComponent();
            DetectWifiAdapter();
        }

        // ============================================================
        // DETECT WI-FI ADAPTER
        // ============================================================

        private void DetectWifiAdapter()
        {
            try
            {
                var adapters =
                    NetworkInterface.GetAllNetworkInterfaces();

                _wifiAdapter = adapters
                    .Where(a =>
                        a.NetworkInterfaceType ==
                        NetworkInterfaceType.Wireless80211 &&
                        a.OperationalStatus ==
                        OperationalStatus.Up)
                    .FirstOrDefault();

                if (_wifiAdapter == null)
                {
                    _wifiAdapter = adapters
                        .Where(a =>
                            a.NetworkInterfaceType ==
                            NetworkInterfaceType.Wireless80211)
                        .FirstOrDefault();
                }

                if (_wifiAdapter == null)
                {
                    AdapterNameText.Text =
                        "No Wi-Fi adapter detected";

                    OldMacText.Text = "Not available";
                    CurrentMacText.Text = "Not available";

                    SetStatus(
                        "●  NOT FOUND",
                        Color.FromRgb(255, 100, 100));

                    return;
                }

                AdapterNameText.Text =
                    $"{_wifiAdapter.Name} - {_wifiAdapter.Description}";

                _originalMac =
                    GetCurrentMac();

                OldMacText.Text = _originalMac;
                CurrentMacText.Text = _originalMac;

                if (_wifiAdapter.OperationalStatus ==
                    OperationalStatus.Up)
                {
                    SetStatus(
                        "●  CONNECTED",
                        Color.FromRgb(101, 232, 174));
                }
                else
                {
                    SetStatus(
                        "●  DISCONNECTED",
                        Color.FromRgb(255, 190, 80));
                }
            }
            catch (Exception ex)
            {
                AdapterNameText.Text =
                    "Unable to detect Wi-Fi adapter";

                SetStatus(
                    "●  ERROR",
                    Color.FromRgb(255, 100, 100));

                MessageBox.Show(
                    ex.Message,
                    "Adapter Detection Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ============================================================
        // GET CURRENT MAC
        // ============================================================

        private string GetCurrentMac()
        {
            if (_wifiAdapter == null)
                return "Not available";

            var address =
                _wifiAdapter.GetPhysicalAddress();

            byte[] bytes =
                address.GetAddressBytes();

            if (bytes.Length != 6)
                return "Not available";

            return string.Join(
                "-",
                bytes.Select(
                    b => b.ToString("X2")));
        }

        // ============================================================
        // GENERATE LOCALLY ADMINISTERED MAC
        // ============================================================

        private string GenerateMac()
        {
            byte[] mac = new byte[6];

            // Locally administered + unicast.
            mac[0] = 0x02;

            for (int i = 1; i < 6; i++)
            {
                mac[i] =
                    (byte)_random.Next(0, 256);
            }

            return string.Join(
                "-",
                mac.Select(
                    b => b.ToString("X2")));
        }

        // ============================================================
        // FIND WINDOWS NETWORK DRIVER REGISTRY KEY
        // ============================================================

        private RegistryKey? FindAdapterRegistryKey()
        {
            if (_wifiAdapter == null)
                return null;

            string interfaceGuid =
                _wifiAdapter.Id.ToString();

            using RegistryKey? baseKey =
                Registry.LocalMachine.OpenSubKey(
                    $@"SYSTEM\CurrentControlSet\Control\Class\{{{NetworkClassGuid}}}",
                    writable: true);

            if (baseKey == null)
                return null;

            foreach (string subName in baseKey.GetSubKeyNames())
            {
                using RegistryKey? subKey =
                    baseKey.OpenSubKey(
                        subName,
                        writable: true);

                if (subKey == null)
                    continue;

                object? instanceId =
                    subKey.GetValue(
                        "NetCfgInstanceId");

                if (instanceId == null)
                    continue;

                if (string.Equals(
                    instanceId.ToString(),
                    interfaceGuid,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return baseKey.OpenSubKey(
                        subName,
                        writable: true);
                }
            }

            return null;
        }

        // ============================================================
        // SET MAC OVERRIDE
        // ============================================================

        private bool SetMacOverride(string mac)
        {
            using RegistryKey? key =
                FindAdapterRegistryKey();

            if (key == null)
                return false;

            string cleanMac =
                mac.Replace("-", "")
                   .Replace(":", "")
                   .ToUpperInvariant();

            key.SetValue(
                "NetworkAddress",
                cleanMac,
                RegistryValueKind.String);

            return true;
        }

        // ============================================================
        // REMOVE MAC OVERRIDE
        // ============================================================

        private bool RemoveMacOverride()
        {
            using RegistryKey? key =
                FindAdapterRegistryKey();

            if (key == null)
                return false;

            if (key.GetValue("NetworkAddress") != null)
            {
                key.DeleteValue(
                    "NetworkAddress",
                    throwOnMissingValue: false);
            }

            return true;
        }

        // ============================================================
        // RESTART NETWORK ADAPTER
        // ============================================================

        private async Task<bool> RestartAdapter()
        {
            if (_wifiAdapter == null)
                return false;

            string adapterName =
                _wifiAdapter.Name;

            try
            {
                await RunPowerShell(
                    $"Disable-NetAdapter -Name '{EscapePowerShell(adapterName)}' -Confirm:$false");

                await Task.Delay(2500);

                await RunPowerShell(
                    $"Enable-NetAdapter -Name '{EscapePowerShell(adapterName)}' -Confirm:$false");

                await Task.Delay(3500);

                return true;
            }
            catch
            {
                return false;
            }
        }

        // ============================================================
        // POWERSHELL
        // ============================================================

        private async Task<string> RunPowerShell(
            string command)
        {
            return await Task.Run(() =>
            {
                var psi =
                    new ProcessStartInfo
                    {
                        FileName =
                            "powershell.exe",

                        Arguments =
                            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{command}\"",

                        UseShellExecute = false,

                        RedirectStandardOutput = true,

                        RedirectStandardError = true,

                        CreateNoWindow = true
                    };

                using Process process =
                    new Process();

                process.StartInfo = psi;

                process.Start();

                string output =
                    process.StandardOutput.ReadToEnd();

                string error =
                    process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new Exception(
                        string.IsNullOrWhiteSpace(error)
                            ? "Windows could not change the adapter."
                            : error);
                }

                return output;
            });
        }

        private string EscapePowerShell(string value)
        {
            return value.Replace(
                "'",
                "''");
        }

        // ============================================================
        // GENERATE / APPLY
        // ============================================================

        private async void Generate_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_wifiAdapter == null)
            {
                MessageBox.Show(
                    "No Wi-Fi adapter was detected.",
                    "MAC Changer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            try
            {
                string newMac =
                    GenerateMac();

                NewMacText.Text =
                    newMac;

                SetStatus(
                    "●  APPLYING...",
                    Color.FromRgb(143, 183, 255));

                // Apply registry override.
                if (!SetMacOverride(newMac))
                {
                    throw new Exception(
                        "Could not find the Windows registry entry for this Wi-Fi adapter.");
                }

                // Restart adapter.
                if (!await RestartAdapter())
                {
                    throw new Exception(
                        "Windows could not restart the Wi-Fi adapter.");
                }

                // Refresh adapter information.
                await Task.Delay(1000);

                DetectWifiAdapter();

                string actualMac =
                    GetCurrentMac();

                CurrentMacText.Text =
                    actualMac;

                // Verify.
                if (string.Equals(
                    actualMac,
                    newMac,
                    StringComparison.OrdinalIgnoreCase))
                {
                    SetStatus(
                        "●  CHANGED",
                        Color.FromRgb(101, 232, 174));

                    try
                    {
                        Clipboard.SetText(actualMac);
                    }
                    catch
                    {
                    }
                }
                else
                {
                    SetStatus(
                        "●  NOT CHANGED",
                        Color.FromRgb(255, 190, 80));

                    MessageBox.Show(
                        "Windows restarted the adapter, but the adapter did not report the requested MAC address.\n\nThis usually means the Wi-Fi driver does not support NetworkAddress overrides.",
                        "MAC Change Not Applied",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                SetStatus(
                    "●  ERROR",
                    Color.FromRgb(255, 100, 100));

                MessageBox.Show(
                    ex.Message,
                    "MAC Change Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                DetectWifiAdapter();
            }
        }

        // ============================================================
        // RESTORE
        // ============================================================

        private async void Restore_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_wifiAdapter == null)
                return;

            try
            {
                SetStatus(
                    "●  RESTORING...",
                    Color.FromRgb(255, 190, 80));

                // Remove the override instead of writing the
                // original MAC into NetworkAddress.
                if (!RemoveMacOverride())
                {
                    throw new Exception(
                        "Could not find the Windows registry entry for this Wi-Fi adapter.");
                }

                if (!await RestartAdapter())
                {
                    throw new Exception(
                        "Windows could not restart the Wi-Fi adapter.");
                }

                await Task.Delay(1000);

                DetectWifiAdapter();

                string actualMac =
                    GetCurrentMac();

                CurrentMacText.Text =
                    actualMac;

                NewMacText.Text =
                    "Not generated yet";

                SetStatus(
                    "●  CONNECTED",
                    Color.FromRgb(101, 232, 174));
            }
            catch (Exception ex)
            {
                SetStatus(
                    "●  ERROR",
                    Color.FromRgb(255, 100, 100));

                MessageBox.Show(
                    ex.Message,
                    "Restore Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                DetectWifiAdapter();
            }
        }

        // ============================================================
        // COPY
        // ============================================================

        private void Copy_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (NewMacText.Text ==
                "Not generated yet")
                return;

            try
            {
                Clipboard.SetText(
                    NewMacText.Text);
            }
            catch
            {
            }
        }

        // ============================================================
        // STATUS
        // ============================================================

        private void SetStatus(
            string text,
            Color color)
        {
            StatusText.Text = text;

            StatusText.Foreground =
                new SolidColorBrush(color);
        }

        // ============================================================
        // CLOSE
        // ============================================================

        private void Close_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }

        // ============================================================
        // MINIMIZE
        // ============================================================

        private void Minimize_Click(
            object sender,
            RoutedEventArgs e)
        {
            WindowState =
                WindowState.Minimized;
        }

        // ============================================================
        // MAXIMIZE
        // ============================================================

        private void Maximize_Click(
            object sender,
            RoutedEventArgs e)
        {
            WindowState =
                WindowState == WindowState.Normal
                    ? WindowState.Maximized
                    : WindowState.Normal;
        }

        // ============================================================
        // MOVE WINDOW
        // ============================================================

        private void TitleBar_MouseDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (e.LeftButton ==
                MouseButtonState.Pressed)
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
