using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace EmployeeClient.ViewModels;

 public sealed class LoginViewModel : INotifyPropertyChanged
{
    private string _serverIp = "127.0.0.1";
    private int _udpPort = 50032;
    private string _username =
    EmployeeClient.Properties.Settings.Default.LastEmployeeUsername ?? "";

    public string ServerIp
    {
        get => _serverIp;
        set
        {
            _serverIp = value;
            OnPropertyChanged();
        }
    }

    public int UdpPort
    {
        get => _udpPort;
        set
        {
            _udpPort = value;
            OnPropertyChanged();
        }
    }

    public string Username
    {
        get => _username;
        set
        {
            _username = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}


