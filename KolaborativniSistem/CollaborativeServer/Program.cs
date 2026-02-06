using System;
using System.IO;
using System.Threading.Tasks;
using CollaborativeServer.Core;
using CollaborativeServer.Networking;

static ServerConfig ParseArgs(string[] args)
{
    var bind = "0.0.0.0";
    var udp = 50032;
    var tcp = 50005;
    var timeout = 250;

    for (int i = 0; i < args.Length; i++)
    {
        string a = args[i];

        if (a == "--bind" && i + 1 < args.Length) bind = args[++i];
        else if (a == "--udp" && i + 1 < args.Length && int.TryParse(args[++i], out var u)) udp = u;
        else if (a == "--tcp" && i + 1 < args.Length && int.TryParse(args[++i], out var t)) tcp = t;
        else if (a == "--timeout" && i + 1 < args.Length && int.TryParse(args[++i], out var ms)) timeout = ms;
    }

    return new ServerConfig
    {
        BindIP = bind,
        UdpPort = udp,
        TcpPort = tcp,
        SelectTimeoutMs = timeout
    };
}

var cfg = ParseArgs(args);

Console.WriteLine("=== Kolaborativni Servis ===");
Console.WriteLine($"Bind IP: {cfg.BindIP}");
Console.WriteLine($"UDP Port: {cfg.UdpPort}");
Console.WriteLine($"TCP Port: {cfg.TcpPort}");
Console.WriteLine($"Select timeout: {cfg.SelectTimeoutMs} ms");

// Stabilna lokacija za fajl (u folderu aplikacije)
var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
Directory.CreateDirectory(dataDir);
var tasksFile = Path.Combine(dataDir, "tasks.json");

// TaskStore će automatski učitati iz JSON-a
var store = new TaskStore(tasksFile);

var udpServer = new UdpServer(store);
var tcpServer = new TcpServer(store);

// Start servere u background taskovima i loguj exception ako se desi
_ = Task.Run(() =>
{
    try
    {
        udpServer.Start(cfg.BindIP, cfg.UdpPort, cfg.TcpPort);
    }
    catch (Exception ex)
    {
        Console.WriteLine("[FATAL][UDP] " + ex);
    }
});

_ = Task.Run(() =>
{
    try
    {
        tcpServer.Start(cfg.BindIP, cfg.TcpPort);
    }
    catch (Exception ex)
    {
        Console.WriteLine("[FATAL][TCP] " + ex);
    }
});

Console.WriteLine($"Tasks file: {tasksFile}");
Console.WriteLine("Press ENTER to stop.");
Console.ReadLine();
