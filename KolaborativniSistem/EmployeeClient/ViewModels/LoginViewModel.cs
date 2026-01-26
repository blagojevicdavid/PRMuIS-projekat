using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EmployeeClient.ViewModels;

public sealed class LoginViewModel : INotifyPropertyChanged
{
    private string _serverIp = "127.0.0.1";
    private int _udpPort = 50032;
    private string _username = EmployeeClient.Properties.Settings.Default.LastEmployeeUsername ?? "";
    private string _loginHint = "";

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

    public string LoginHint
    {
        get => _loginHint;
        set { _loginHint = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
