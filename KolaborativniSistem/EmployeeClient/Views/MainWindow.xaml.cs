using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using EmployeeClient.Networking;
using EmployeeClient.ViewModels;
using Shared.Models;

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

            await _tcpLoginClient.ConnectAndIdentifyAsync(vm.ServerIp, tcpPort, vm.Username);

            LoginCard.Visibility = Visibility.Collapsed;
            DashboardPanel.Visibility = Visibility.Visible;

            Width = 980;
            Height = 560;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var dash = new EmployeeDashboardViewModel
            {
                Username = vm.Username,
                ConnectionStatus = $"TCP:{vm.ServerIp}:{tcpPort}",
                FormHint = "",
                CompletionComment = ""
            };

            DataContext = dash;

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
        if (!_tcpLoginClient.IsConnected)
            return;

        RefreshTasks();
    }

    private static int StatusRank(StatusZadatka s) => s switch
    {
        StatusZadatka.UToku => 0,
        StatusZadatka.NaCekanju => 1,
        StatusZadatka.Zavrsen => 2,
        _ => 9
    };

    private void RefreshTasks()
    {
        if (_isRefreshing) return;
        if (!_tcpLoginClient.IsConnected) return;

        if (DataContext is not EmployeeDashboardViewModel dash)
            return;

        _isRefreshing = true;

        string? selectedNaziv = dash.SelectedTask?.Naziv;

        try
        {
            // ✅ JSON LIST request
            var req = new
            {
                type = "employee_list"
            };

            var reply = _tcpLoginClient.SendJsonAndReceive(req);

            // 1) probaj JSON reply
            var parsed = TryParseTasksFromJsonReply(reply);

            // 2) fallback na legacy
            parsed ??= ParseTasksFromLegacyReply(reply);

            if (parsed == null || parsed.Count == 0)
            {
                dash.AssignedTasks.Clear();
                dash.SelectedTask = null;
                dash.FormHint = "Nema dodeljenih zadataka.";
                dash.NotifySelectionDerived();
                return;
            }

            parsed = parsed
                .OrderBy(t => StatusRank(t.Status))
                .ThenBy(t => int.TryParse(t.Prioritet, out var pr) ? pr : int.MaxValue)
                .ThenBy(t => t.Rok)
                .ThenBy(t => t.Naziv, StringComparer.OrdinalIgnoreCase)
                .ToList();

            dash.AssignedTasks.Clear();
            foreach (var t in parsed)
                dash.AssignedTasks.Add(t);

            if (!string.IsNullOrWhiteSpace(selectedNaziv))
                dash.SelectedTask = dash.AssignedTasks.FirstOrDefault(x => x.Naziv == selectedNaziv);

            dash.FormHint = $"Učitano: {dash.AssignedTasks.Count}";
            dash.NotifySelectionDerived();
        }
        catch (Exception ex)
        {
            dash.FormHint = ex.Message;
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private static List<TaskRow>? TryParseTasksFromJsonReply(string reply)
    {
        var trimmed = (reply ?? "").Trim();
        if (trimmed.Length == 0) return null;
        if (!trimmed.StartsWith("{") && !trimmed.StartsWith("[")) return null;

        try
        {
            using var doc = JsonDocument.Parse(trimmed);

            // ako server vrati direktno niz taskova
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                return doc.RootElement.EnumerateArray()
                    .Select(ReadTaskRowFromJson)
                    .Where(x => x != null)
                    .Cast<TaskRow>()
                    .ToList();
            }

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            if (!doc.RootElement.TryGetProperty("tasks", out var tasksEl))
                return null;

            if (tasksEl.ValueKind != JsonValueKind.Array)
                return null;

            var list = new List<TaskRow>();
            foreach (var el in tasksEl.EnumerateArray())
            {
                var row = ReadTaskRowFromJson(el);
                if (row != null) list.Add(row);
            }

            return list;
        }
        catch
        {
            return null;
        }
    }

    private static TaskRow? ReadTaskRowFromJson(JsonElement el)
    {
        try
        {
            string naziv = el.TryGetProperty("naziv", out var n) ? (n.GetString() ?? "") : "";
            string menadzer = el.TryGetProperty("menadzer", out var m) ? (m.GetString() ?? "") : "";
            string rok = el.TryGetProperty("rok", out var r) ? (r.GetString() ?? "") : "";
            string prioritet = el.TryGetProperty("prioritet", out var p) ? (p.GetString() ?? "") : "";

            string statusStr = el.TryGetProperty("status", out var s) ? (s.GetString() ?? "") : "";
            if (!Enum.TryParse<StatusZadatka>(statusStr, true, out var status))
                status = StatusZadatka.NaCekanju;

            return new TaskRow
            {
                Naziv = naziv.Trim(),
                Menadzer = menadzer.Trim(),
                Rok = rok.Trim(),
                Prioritet = prioritet.Trim(),
                Status = status
            };
        }
        catch
        {
            return null;
        }
    }

    // stari format: "naziv|menadzer|rok|prioritet|status^..."
    private static List<TaskRow>? ParseTasksFromLegacyReply(string reply)
    {
        if (string.IsNullOrWhiteSpace(reply) || reply == "NO_TASKS")
            return new List<TaskRow>();

        var parsed = new List<TaskRow>();

        var items = reply.Split('^').Select(x => x.Trim()).Where(x => x.Length > 0);
        foreach (var item in items)
        {
            var p = item.Split('|');
            if (p.Length < 5) continue;

            var statusStr = p[4].Trim();
            if (!Enum.TryParse<StatusZadatka>(statusStr, true, out var status))
                status = StatusZadatka.NaCekanju;

            parsed.Add(new TaskRow
            {
                Naziv = p[0].Trim(),
                Menadzer = p[1].Trim(),
                Rok = p[2].Trim(),
                Prioritet = p[3].Trim(),
                Status = status
            });
        }

        return parsed;
    }

    private void StartTask_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not EmployeeDashboardViewModel dash)
            return;

        if (dash.SelectedTask == null)
        {
            dash.FormHint = "Izaberi zadatak iz tabele.";
            return;
        }

        if (dash.SelectedTask.Status != StatusZadatka.NaCekanju)
        {
            dash.FormHint = "Možeš preuzeti samo zadatak koji je Na čekanju.";
            return;
        }

        try
        {
            // ✅ JSON TAKE
            var req = new
            {
                type = "employee_action",
                action = "take",
                taskName = dash.SelectedTask.Naziv
            };

            var reply = _tcpLoginClient.SendJsonAndReceive(req);
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
            dash.FormHint = "Izaberi zadatak iz tabele.";
            return;
        }

        if (dash.SelectedTask.Status != StatusZadatka.UToku)
        {
            dash.FormHint = "Možeš završiti samo zadatak koji je U toku.";
            return;
        }

        try
        {
            string comment = (dash.CompletionComment ?? "").Trim();

            // ✅ JSON FINISH (komentar optional)
            var req = new
            {
                type = "employee_action",
                action = "finish",
                taskName = dash.SelectedTask.Naziv,
                comment = string.IsNullOrWhiteSpace(comment) ? null : comment
            };

            var reply = _tcpLoginClient.SendJsonAndReceive(req);
            dash.FormHint = reply;

            if (reply.Trim().Equals("OK", StringComparison.OrdinalIgnoreCase))
                dash.CompletionComment = "";

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
