using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Shared.Models;

namespace EmployeeClient.ViewModels;

public sealed class EmployeeDashboardViewModel : INotifyPropertyChanged
{
    private string _username = "";
    private string _connectionStatus = "";
    private TaskRow? _selectedTask;
    private string _formHint = "";
    private string _completionComment = "";

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
        set
        {
            _selectedTask = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanTakeSelected));
            OnPropertyChanged(nameof(CanFinishSelected));
            OnPropertyChanged(nameof(SelectedStatusText));
            OnPropertyChanged(nameof(SelectedPriorityText));
        }
    }

    public string FormHint
    {
        get => _formHint;
        set { _formHint = value; OnPropertyChanged(); }
    }

    public string CompletionComment
    {
        get => _completionComment;
        set { _completionComment = value; OnPropertyChanged(); }
    }

    public bool CanTakeSelected => SelectedTask != null && SelectedTask.Status == StatusZadatka.NaCekanju;
    public bool CanFinishSelected => SelectedTask != null && SelectedTask.Status == StatusZadatka.UToku;

    public string SelectedStatusText => SelectedTask?.Status.ToString() ?? "";
    public string SelectedPriorityText => SelectedTask?.Prioritet ?? "";

    public void NotifySelectionDerived()
    {
        OnPropertyChanged(nameof(CanTakeSelected));
        OnPropertyChanged(nameof(CanFinishSelected));
        OnPropertyChanged(nameof(SelectedStatusText));
        OnPropertyChanged(nameof(SelectedPriorityText));
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

    public StatusZadatka Status { get; set; } = StatusZadatka.NaCekanju;
    public string StatusText => Status.ToString();
}
