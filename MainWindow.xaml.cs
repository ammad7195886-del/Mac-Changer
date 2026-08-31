using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SimpleMACChanger
{
    public partial class MainWindow : Window
    {
        private readonly Random _random = new Random();

        private NetworkInterface? _wifiAdapter;

        private string _originalMac = "Not available";

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


                // First look for a connected Wi-Fi adapter.

                _wifiAdapter = adapters
                    .Where(a =>
                        a.NetworkInterfaceType ==
                            NetworkInterfaceType.Wireless80211 &&
                        a.OperationalStatus ==
                            OperationalStatus.Up)
                    .FirstOrDefault();


                // If Wi-Fi isn't connected,
                // find an installed Wi-Fi adapter.

                if (_wifiAdapter == null)
                {
                    _wifiAdapter = adapters
                        .Where(a =>
                            a.NetworkInterfaceType ==
                                NetworkInterfaceType.Wireless80211)
                        .FirstOrDefault();
                }


                // No Wi-Fi adapter.

                if (_wifiAdapter == null)
                {
                    AdapterNameText.Text =
                        "No Wi-Fi adapter detected";

                    OldMacText.Text =
                        "Not available";

                    CurrentMacText.Text =
                        "Not available";

                    StatusText.Text =
                        "●  NOT FOUND";

                    StatusText.Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(255, 100, 100));

                    return;
                }


                // ====================================================
                // ADAPTER NAME
                // ====================================================

                string adapterName =
                    _wifiAdapter.Name;

                string description =
                    _wifiAdapter.Description;


                AdapterNameText.Text =
                    $"{adapterName} - {description}";


                // ====================================================
                // ORIGINAL MAC
                // ====================================================

                _originalMac =
                    FormatMac(
                        _wifiAdapter.GetPhysicalAddress());


                OldMacText.Text =
                    _originalMac;

                CurrentMacText.Text =
                    _originalMac;


                // ====================================================
                // STATUS
                // ====================================================

                if (_wifiAdapter.OperationalStatus ==
                    OperationalStatus.Up)
                {
                    StatusText.Text =
                        "●  CONNECTED";

                    StatusText.Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(101, 232, 174));
                }
                else
                {
                    StatusText.Text =
                        "●  DISCONNECTED";

                    StatusText.Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(255, 190, 80));
                }
            }
            catch
            {
                AdapterNameText.Text =
                    "Unable to detect Wi-Fi adapter";

                StatusText.Text =
                    "●  ERROR";

                StatusText.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(255, 100, 100));

                OldMacText.Text =
                    "Not available";

                CurrentMacText.Text =
                    "Not available";
            }
        }


        // ============================================================
        // FORMAT MAC
        // ============================================================

        private string FormatMac(
            PhysicalAddress address)
        {
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
            byte[] mac =
                new byte[6];


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
        // GENERATE BUTTON
        // ============================================================

        private void Generate_Click(
            object sender,
            RoutedEventArgs e)
        {
            string mac =
                GenerateMac();


            NewMacText.Text =
                mac;


            CurrentMacText.Text =
                mac;


            StatusText.Text =
                "●  GENERATED";


            StatusText.Foreground =
                new SolidColorBrush(
                    Color.FromRgb(
                        143,
                        183,
                        255));


            try
            {
                Clipboard.SetText(mac);
            }
            catch
            {
            }
        }


        // ============================================================
        // RESTORE BUTTON
        // ============================================================

        private void Restore_Click(
            object sender,
            RoutedEventArgs e)
        {
            CurrentMacText.Text =
                _originalMac;


            NewMacText.Text =
                "Not generated yet";


            StatusText.Text =
                "●  CONNECTED";


            StatusText.Foreground =
                new SolidColorBrush(
                    Color.FromRgb(
                        101,
                        232,
                        174));
        }


        // ============================================================
        // COPY BUTTON
        // ============================================================

        private void Copy_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (NewMacText.Text ==
                "Not generated yet")
            {
                return;
            }


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
        // DRAG WINDOW
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
