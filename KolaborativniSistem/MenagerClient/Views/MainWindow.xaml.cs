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
using System;


using ManagerClient.ViewModels;
using ManagerClient.Networking;
using Shared.Protocol;

namespace ManagerClient.Views;

public partial class MainWindow : Window
{
    private readonly TcpLoginClient _tcpLoginClient = new TcpLoginClient();
    private int _tcpPort;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new LoginViewModel();

        // automatsko popunjavanje poslednjeg usernamea
        if (DataContext is LoginViewModel vm)
        {
            var last = ManagerClient.Properties.Settings.Default.LastManagerUsername;
            if (!string.IsNullOrWhiteSpace(last))
                vm.Username = last;
        }
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LoginViewModel vm)
            return;

        if (string.IsNullOrWhiteSpace(vm.Username))
        {
            MessageBox.Show("Username empty.");
            return;
        }

        // cuvanje useranamea
        ManagerClient.Properties.Settings.Default.LastManagerUsername = vm.Username;
        ManagerClient.Properties.Settings.Default.Save();

        try
        {
            // Udp login
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

            _tcpPort = tcpPort;

            // tcp konekcija i dentifikacija
            await _tcpLoginClient.ConnectAndIdentifyAsync(vm.ServerIp, tcpPort, vm.Username);

            // zatvaranje login panela i otvaranje dashboarda
            LoginCard.Visibility = Visibility.Collapsed;
            DashboardPanel.Visibility = Visibility.Visible;

            // povecavanje prozora za dashboard
            Width = 900;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Login error");
        }
    }

    private void SendTask_Click(object sender, RoutedEventArgs e)
    {
        if(DataContext is not LoginViewModel vm)
            return;

        if (!_tcpLoginClient.IsConnected)
        {
            MessageBox.Show("Nema konekcije sa serverom!");
            return;
        }

        if(string.IsNullOrWhiteSpace(vm.NewTaskName) || string.IsNullOrWhiteSpace(vm.NewTaskEmployee))
        {
            MessageBox.Show("Kosisnicko ime zaposlenog i/ili naziv zadatka su prazni");
            return;
        }

        string msg = ProtocolConstants.TcpSendPrefix + $"{vm.NewTaskName}|{vm.NewTaskEmployee}|{vm.NewTaskDueDate:yyyy-MM-dd}|{vm.NewTaskPriority}";
        try
        {
            _tcpLoginClient.SendLine(msg);
            MessageBox.Show("Zadatak uspesno poslat!");
        }

        catch(Exception ex)
        {
            MessageBox.Show($"Greska prilikom slanja zadatka: {ex.Message}");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _tcpLoginClient.Dispose();
        base.OnClosed(e);
    }
}