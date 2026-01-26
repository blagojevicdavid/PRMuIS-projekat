using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EmployeeClient.ViewModels;

public sealed class EmployeeDashboardViewModel : INotifyPropertyChanged
{
    private string _username = "";
    private string _connectionStatus = "";
    private TaskRow? _selectedTask;
    private string _formHint = "";

    public string Username
    {
        get => _username;
        set { _username = value; OnPropertyChanged(); }
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        set { _connectionStatus = value; OnPropertyChanged(); }
    }

    public ObservableCollection<TaskRow> AssignedTasks { get; } = new();

    public TaskRow? SelectedTask
    {
        get => _selectedTask;
        set { _selectedTask = value; OnPropertyChanged(); }
    }

    public string FormHint
    {
        get => _formHint;
        set { _formHint = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class TaskRow
{
    public string Naziv { get; set; } = "";
    public string Menadzer { get; set; } = "";
    public string Rok { get; set; } = "";
    public string Prioritet { get; set; } = "";
    public string Status { get; set; } = "";
}
