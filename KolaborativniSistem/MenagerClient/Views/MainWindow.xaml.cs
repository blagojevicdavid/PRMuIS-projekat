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
using System.Threading.Tasks;
using System.Windows.Threading;
using System;


using ManagerClient.ViewModels;
using ManagerClient.Networking;
using Shared.Protocol;

namespace ManagerClient.Views;

public partial class MainWindow : Window
{
    private readonly TcpLoginClient _tcpLoginClient = new TcpLoginClient();
    private int _tcpPort;
    private DispatcherTimer? _autoTimer;
    private string? _lastTasksSignature;

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

            StartAutoRefresh();

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
            //MessageBox.Show("Zadatak uspesno poslat!");

            vm.NewTaskName = "";
            vm.NewTaskEmployee = "";
            vm.NewTaskDueDate = DateTime.Today.AddDays(1);
            vm.NewTaskPriority = 1;
            
        }

        catch(Exception ex)
        {
            MessageBox.Show($"Greska prilikom slanja zadatka: {ex.Message}");
        }
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        _autoTimer?.Stop();
        _autoTimer = null;
        // zatvaranje tcp konekcije
        _tcpLoginClient.Dispose();
        // prikazivanje login panela i sakrivanje dashboarda
        LoginCard.Visibility = Visibility.Visible;
        DashboardPanel.Visibility = Visibility.Collapsed;
        // smanjenje prozora
        Width = 480;
        Height = 500;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        if(DataContext is LoginViewModel vm)
        {
            vm.NewTaskName = "";
            vm.NewTaskEmployee = "";
            vm.NewTaskDueDate = DateTime.Today.AddDays(1);
            vm.NewTaskPriority = 1;
        }
    }

    private void StartAutoRefresh()
    {
        _autoTimer?.Stop();
        _autoTimer = new DispatcherTimer();
        _autoTimer.Interval = TimeSpan.FromSeconds(1);
        _autoTimer.Tick += async (_, __) => RefreshAllTaskAsync();
        _autoTimer.Start();

        _ = RefreshAllTaskAsync();

    }

    private async Task RefreshAllTaskAsync()
    {
        if (DataContext is not LoginViewModel vm) return;
        if (string.IsNullOrWhiteSpace(vm.Username)) return;

        // zapamti selekciju
        string? selectedTaskName = vm.SelectedTask?.Naziv;

        try
        {
            var tasks = await Task.Run(() =>
                UdpTasksClient.GetAllForManager(vm.ServerIp, vm.UdpPort, vm.Username));

            // pamti signature
            string signature = string.Join("||",
                tasks
                    .OrderBy(t => t.Naziv)
                    .ThenBy(t => t.Zaposleni)
                    .ThenBy(t => t.Rok)
                    .Select(t => $"{t.Naziv}|{t.Zaposleni}|{t.Rok:yyyy-MM-dd}|{t.Prioritet}|{(int)t.Status}")
            );

            //ne menja kolekciju ako se nije nista promenilo
            if (signature == _lastTasksSignature)
                return;

            _lastTasksSignature = signature;

            // update kolekcije
            vm.ActiveTasks.Clear();
            foreach (var t in tasks)
                vm.ActiveTasks.Add(t);

            // Restore selekcije
            if (!string.IsNullOrWhiteSpace(selectedTaskName))
            {
                vm.SelectedTask = vm.ActiveTasks.FirstOrDefault(t => t.Naziv == selectedTaskName);
            }
        }
        catch (Exception ex) 
        {
            //iskljuciti nako debagovanja
         //MessageBox.Show(ex.Message, "Auto refresh UDP error"); 
        }
    }

    private void IncreasePriority_Click(object sender, RoutedEventArgs e) => ChangePriorityBy(+1);
    private void DecreasePriority_Click(object sender, RoutedEventArgs e) => ChangePriorityBy(-1);

    private void ChangePriorityBy(int delta)
    {
        if (DataContext is not LoginViewModel vm) return;
        if (!_tcpLoginClient.IsConnected) { MessageBox.Show("Nema konekcije sa serverom!"); return; }
        if (vm.SelectedTask is null) { MessageBox.Show("Izaberi zadatak u tabeli."); return; }

        int next = vm.SelectedTask.Prioritet + delta;
        if (next < 1) next = 1;
        if (next > 5) next = 5;

        if (next == vm.SelectedTask.Prioritet) return;

        _tcpLoginClient.SendLine($"{vm.SelectedTask.Naziv}:{next}");
        vm.SelectedTask.Prioritet = next; 
    }

    protected override void OnClosed(EventArgs e)
    {
        _tcpLoginClient.Dispose();
        _autoTimer?.Stop();
        base.OnClosed(e);
    }
}