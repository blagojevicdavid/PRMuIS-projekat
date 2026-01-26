using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using EmployeeClient.Networking;
using EmployeeClient.ViewModels;
using Shared.Protocol;

namespace EmployeeClient.Views;

public partial class MainWindow : Window
{
    private readonly TcpLoginClient _tcpLoginClient = new TcpLoginClient();

    private readonly DispatcherTimer _refreshTimer;
    private bool _isRefreshing;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new LoginViewModel();

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;

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

            var dash = new EmployeeDashboardViewModel
            {
                Username = vm.Username,
                ConnectionStatus = $"TCP:{vm.ServerIp}:{tcpPort}",
                FormHint = ""
            };

            DataContext = dash;

            // Prvi refresh odmah + start timer
            RefreshTasks();
            _refreshTimer.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Login error");
        }
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        // Ne refreshuj ako nisi na dashboardu ili nisi konektovana
        if (!_tcpLoginClient.IsConnected)
            return;

        RefreshTasks();
    }

    private void RefreshTasks()
    {
        if (_isRefreshing) return;
        if (!_tcpLoginClient.IsConnected) return;

        if (DataContext is not EmployeeDashboardViewModel dash)
            return;

        _isRefreshing = true;

        try
        {
            // TCP zahtev za listu (server: LIST)
            var reply = _tcpLoginClient.SendAndReceive("LIST\n");

            dash.AssignedTasks.Clear();

            if (string.IsNullOrWhiteSpace(reply) || reply == "NO_TASKS")
            {
                dash.FormHint = "Nema dodeljenih zadataka.";
                return;
            }

            // server salje jednu liniju: task^task^task
            var items = reply.Split('^').Select(x => x.Trim()).Where(x => x.Length > 0);
            foreach (var item in items)
            {
                var p = item.Split('|');
                if (p.Length < 5) continue;

                dash.AssignedTasks.Add(new TaskRow
                {
                    Naziv = p[0].Trim(),
                    Menadzer = p[1].Trim(),
                    Rok = p[2].Trim(),
                    Prioritet = p[3].Trim(),
                    Status = p[4].Trim()
                });
            }

            dash.FormHint = $"Učitano: {dash.AssignedTasks.Count}";
        }
        catch (Exception ex)
        {
            // Ako pukne konekcija ili server ne odgovara, samo prikaži poruku (timer nastavlja da pokušava)
            if (DataContext is EmployeeDashboardViewModel d2)
                d2.FormHint = ex.Message;
        }
        finally
        {
            _isRefreshing = false;
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
            var reply = _tcpLoginClient.SendAndReceive($"{ProtocolConstants.TcpTakePrefix}{dash.SelectedTask.Naziv}\n");
            dash.FormHint = reply;

            RefreshTasks();
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
            var reply = _tcpLoginClient.SendAndReceive($"{ProtocolConstants.TcpDonePrefix}{dash.SelectedTask.Naziv}\n");
            dash.FormHint = reply;

            RefreshTasks();
        }
        catch (Exception ex)
        {
            dash.FormHint = ex.Message;
        }
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Stop();
        _tcpLoginClient.Dispose();

        DashboardPanel.Visibility = Visibility.Collapsed;
        LoginCard.Visibility = Visibility.Visible;

        Width = 480;
        Height = 500;

        DataContext = new LoginViewModel();
    }

    protected override void OnClosed(EventArgs e)
    {
        _refreshTimer.Stop();
        _tcpLoginClient.Dispose();
        base.OnClosed(e);
    }
}
