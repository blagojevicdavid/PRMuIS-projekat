using System;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using EmployeeClient.ViewModels;
using EmployeeClient.Networking;

namespace EmployeeClient.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new LoginViewModel();
    }

    private void Connect_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LoginViewModel vm)
            return;

        if (string.IsNullOrWhiteSpace(vm.Username))
        {
            MessageBox.Show("Username empty.");
            return;
        }

        EmployeeClient.Properties.Settings.Default.LastEmployeeUsername = vm.Username;
        EmployeeClient.Properties.Settings.Default.Save();

        try
        {
            string response = UdpLoginClient.Login(vm.ServerIp, vm.UdpPort, vm.Username);

            var trimmed = response.Trim();
            if (!trimmed.StartsWith("TCP:"))
            {
                MessageBox.Show($"Unexpected UDP response: {trimmed}");
                return;
            }

            if (!int.TryParse(trimmed.Substring("TCP:".Length), out int tcpPort))
            {
                MessageBox.Show($"Invalid TCP port in response: {trimmed}");
                return;
            }

            using var tcp = new TcpClient(vm.ServerIp, tcpPort);
            using var stream = tcp.GetStream();

           var idMsg = $"ZAPOSLENI:{vm.Username}";
            var idBytes = Encoding.UTF8.GetBytes(idMsg);
            stream.Write(idBytes, 0, idBytes.Length);

            var buf = new byte[128];
            int read = stream.Read(buf, 0, buf.Length);
            var tcpReply = Encoding.UTF8.GetString(buf, 0, read).Trim();

            if (tcpReply == "ID_OK")
            {
                LoginCard.Visibility = Visibility.Collapsed;
                DashboardPanel.Visibility = Visibility.Visible;

                this.Width = 900;
                this.Height = 600;
                return;
            }

            MessageBox.Show($"TCP identification failed: {tcpReply}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Login error");
        }
    }
}
