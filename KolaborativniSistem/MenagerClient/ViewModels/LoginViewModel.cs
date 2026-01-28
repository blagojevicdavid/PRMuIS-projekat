using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;

using Shared.Models;

namespace ManagerClient.ViewModels;

public sealed class LoginViewModel : INotifyPropertyChanged
{
    private string _serverIp = "127.0.0.1";
    private int _udpPort = 50032;
    private string _username = ManagerClient.Properties.Settings.Default.LastManagerUsername ?? "";

    public ObservableCollection<ZadatakProjekta> ActiveTasks { get; } = new ObservableCollection<ZadatakProjekta>();

    public string ServerIp
    {
        get => _serverIp;
        set { _serverIp = value; OnPropertyChanged(); }
    }

    public int UdpPort
    {
        get => _udpPort;
        set { _udpPort = value; OnPropertyChanged(); }
    }

    public string Username
    {
        get => _username;
        set { _username = value; OnPropertyChanged(); }
    }

    private string _newTaskName = "";
    public string NewTaskName
    {
        get => _newTaskName;
        set { _newTaskName = value; OnPropertyChanged(); }
    }

    private string _newTaskEmployee = "";
    public string NewTaskEmployee
    {
        get => _newTaskEmployee;
        set { _newTaskEmployee = value; OnPropertyChanged(); }
    }

    private DateTime _newTaskDueDate = DateTime.Today.AddDays(1);
    public DateTime NewTaskDueDate
    {
        get => _newTaskDueDate;
        set { _newTaskDueDate = value; OnPropertyChanged(); }
    }

    private int _newTaskPriority = 1;
    public int NewTaskPriority
    {
        get => _newTaskPriority;
        set { _newTaskPriority = value; OnPropertyChanged(); }
    }

    private ZadatakProjekta? _selectedTask;
    public ZadatakProjekta? SelectedTask
    {
        get => _selectedTask;
        set
        {
            _selectedTask = value;

            SelectedTaskName = value?.Naziv;

            SelectedTaskComment = value?.Komentar ?? "";

            OnPropertyChanged();
        }
    }

    private string _selectedTaskComment = "";
    public string SelectedTaskComment
    {
        get => _selectedTaskComment;
        set { _selectedTaskComment = value; OnPropertyChanged(); }
    }

    private int _newPriorityValue = 1;
    public int NewPriorityValue
    {
        get => _newPriorityValue;
        set { _newPriorityValue = value; OnPropertyChanged(); }
    }

    private string? _selectedTaskName;
    public string? SelectedTaskName
    {
        get => _selectedTaskName;
        set { _selectedTaskName = value; OnPropertyChanged(); }
    }

    private string _connectionStatus = "";
    public string ConnectionStatus
    {
        get => _connectionStatus;
        set { _connectionStatus = value; OnPropertyChanged(); }
    }

    private string _formHint = "";
    public string FormHint
    {
        get => _formHint;
        set { _formHint = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
