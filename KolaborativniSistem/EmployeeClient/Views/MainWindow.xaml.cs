using System;
using System.Linq;
using System.Windows;
using EmployeeClient.Networking;
using EmployeeClient.ViewModels;

namespace EmployeeClient.Views;

public partial class MainWindow : Window
{
    private readonly TcpLoginClient _tcpLoginClient = new TcpLoginClient();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new LoginViewModel();

        // auto-popunjavanje poslednjeg username
        if (DataContext is LoginViewModel vm)
        {
            var last = EmployeeClient.Properties.Settings.Default.LastEmployeeUsername;
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

        EmployeeClient.Properties.Settings.Default.LastEmployeeUsername = vm.Username;
        EmployeeClient.Properties.Settings.Default.Save();

        try
        {
            // UDP login -> dobij TCP port
            string response = UdpLoginClient.Login(vm.ServerIp, vm.UdpPort, vm.Username);

            var trimmed = response.Trim();
            if (!trimmed.StartsWith("TCP:"))
            {
                vm.LoginHint = $"Unexpected UDP response: {trimmed}";
                return;
            }

            if (!int.TryParse(trimmed.Substring("TCP:".Length), out int tcpPort))
            {
                vm.LoginHint = $"Invalid TCP port in response: {trimmed}";
                return;
            }

            // TCP connect + identifikacija
            await _tcpLoginClient.ConnectAndIdentifyAsync(vm.ServerIp, tcpPort, vm.Username);

            // UI switch
            LoginCard.Visibility = Visibility.Collapsed;
            DashboardPanel.Visibility = Visibility.Visible;

            Width = 980;
            Height = 560;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            // Dashboard VM (bez ListHint/CompletionComment)
            var dash = new EmployeeDashboardViewModel
            {
                Username = vm.Username,
                ConnectionStatus = $"TCP:{vm.ServerIp}:{tcpPort}",
                FormHint = ""
            };

            DataContext = dash;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Login error");
        }
    }

    // Dugme "UDP zahtev" -> učitava zadatke (jer Refresh dugme ne radi)
    private void UdpFetch_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not EmployeeDashboardViewModel dash)
            return;

        try
        {
            var reply = _tcpLoginClient.SendAndReceive("TASKS\n");

            dash.AssignedTasks.Clear();

            if (string.IsNullOrWhiteSpace(reply) || reply == "NO_TASKS")
            {
                dash.FormHint = "Nema dodeljenih zadataka.";
                return;
            }

            var lines = reply.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0);
            foreach (var line in lines)
            {
                var p = line.Split('|');
                if (p.Length < 5) continue;

                dash.AssignedTasks.Add(new TaskRow
                {
                    Naziv = p[0],
                    Menadzer = p[1],
                    Rok = p[2],
                    Prioritet = p[3],
                    Status = p[4]
                });
            }

            dash.FormHint = $"Učitano: {dash.AssignedTasks.Count}";
        }
        catch (Exception ex)
        {
            dash.FormHint = ex.Message;
        }
    }

    private void StartTask_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not EmployeeDashboardViewModel dash)
            return;

        if (dash.SelectedTask == null)
        {
            dash.FormHint = "Izaberi zadatak.";
            return;
        }

        try
        {
            var reply = _tcpLoginClient.SendAndReceive($"START:{dash.SelectedTask.Naziv}\n");
            dash.FormHint = reply;

            // osveži listu nakon starta
            UdpFetch_Click(sender, e);
        }
        catch (Exception ex)
        {
            dash.FormHint = ex.Message;
        }
    }

    private void FinishTask_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not EmployeeDashboardViewModel dash)
            return;

        if (dash.SelectedTask == null)
        {
            dash.FormHint = "Izaberi zadatak.";
            return;
        }

        try
        {
            // bez komentara (jer si izbacio textbox)
            var reply = _tcpLoginClient.SendAndReceive($"FINISH:{dash.SelectedTask.Naziv}\n");
            dash.FormHint = reply;

            // osveži listu nakon završetka
            UdpFetch_Click(sender, e);
        }
        catch (Exception ex)
        {
            dash.FormHint = ex.Message;
        }
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        _tcpLoginClient.Dispose();

        DashboardPanel.Visibility = Visibility.Collapsed;
        LoginCard.Visibility = Visibility.Visible;

        Width = 480;
        Height = 500;

        DataContext = new LoginViewModel();
    }

    protected override void OnClosed(EventArgs e)
    {
        _tcpLoginClient.Dispose();
        base.OnClosed(e);
    }
}
