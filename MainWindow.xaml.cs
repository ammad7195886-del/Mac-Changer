using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SimpleMACChanger
{
    public partial class MainWindow : Window
    {
        private readonly Random _random = new Random();

        private const string OriginalMac = "00-E0-38-8F-DC-C6";

        public MainWindow()
        {
            InitializeComponent();
        }

        private string GeneratePreviewMac()
        {
            byte[] mac = new byte[6];

            // Locally administered address for the preview.
            mac[0] = 0x02;

            for (int i = 1; i < mac.Length; i++)
                mac[i] = (byte)_random.Next(0, 256);

            return string.Join("-", Array.ConvertAll(mac, b => b.ToString("X2")));
        }

        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            string mac = GeneratePreviewMac();

            NewMacText.Text = mac;
            CurrentMacText.Text = mac;

            StatusText.Text = "●  PREVIEW";
            StatusText.Foreground =
                new SolidColorBrush(Color.FromRgb(143, 183, 255));

            try
            {
                Clipboard.SetText(mac);
            }
            catch
            {
                // Clipboard can be unavailable in unusual desktop states.
            }
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            CurrentMacText.Text = OriginalMac;
            NewMacText.Text = "Not generated yet";

            StatusText.Text = "●  CONNECTED";
            StatusText.Foreground =
                new SolidColorBrush(Color.FromRgb(101, 232, 174));
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (NewMacText.Text == "Not generated yet")
                return;

            try
            {
                Clipboard.SetText(NewMacText.Text);
            }
            catch
            {
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Normal
                ? WindowState.Maximized
                : WindowState.Normal;
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
