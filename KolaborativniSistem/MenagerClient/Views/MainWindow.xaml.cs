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

using ManagerClient.ViewModels;
using ManagerClient.Networking;

namespace ManagerClient.Views;

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
        ManagerClient.Properties.Settings.Default.LastManagerUsername = vm.Username;
        ManagerClient.Properties.Settings.Default.Save();


        try
        {
            string response = UdpLoginClient.Login(
                vm.ServerIp,
                vm.UdpPort,
                vm.Username
            );

            //MessageBox.Show( $"Server response: {response}", "UDP login OK");

            var trimmed = response.Trim();
            if (!trimmed.StartsWith("TCP:"))
            {
                MessageBox.Show($"Unexpected UDP response: {trimmed}");
                return;
            }
            //TCP identification

            if (!int.TryParse(trimmed.Substring("TCP:".Length), out int tcpPort))
            {
                MessageBox.Show($"Invalid TCP port in response: {trimmed}");
                return;
            }

            using var tcp = new System.Net.Sockets.TcpClient(vm.ServerIp, tcpPort);
            using var stream = tcp.GetStream();

            var idMsg = $"MENADZER:{vm.Username}\n";
            var idBytes = System.Text.Encoding.UTF8.GetBytes(idMsg);
            stream.Write(idBytes, 0, idBytes.Length);

            var buf = new byte[128];
            int read = stream.Read(buf, 0, buf.Length);
            var tcpReply = System.Text.Encoding.UTF8.GetString(buf, 0, read).Trim();

            //MessageBox.Show($"TCP reply: {tcpReply}");

            if(tcpReply == "ID_OK")//login successfull
            {
                LoginCard.Visibility = Visibility.Collapsed;
                DashboardPanel.Visibility = Visibility.Visible;

                //povecavanje prozora za dashboard
                this.Width = 900;
                this.Height = 600;

                //this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                return;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "UDP error"
            );
        }
    }
}
