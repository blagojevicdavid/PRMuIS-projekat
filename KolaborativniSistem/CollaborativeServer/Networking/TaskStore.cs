using Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CollaborativeServer.Networking
{
    public sealed class TaskStore
    {
        private readonly Dictionary<string, List<ZadatakProjekta>> _taskByManager = new();
        private readonly object _lock = new();

        private readonly string _filePath;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public TaskStore(string? filePath = null)
        {
            _filePath = string.IsNullOrWhiteSpace(filePath)
                ? Path.Combine(AppContext.BaseDirectory, "data", "tasks.json")
                : filePath;

            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            LoadFromDisk();
        }


        private void LoadFromDisk()
        {
            lock (_lock)
            {
                if (!File.Exists(_filePath))
                    return;

                try
                {
                    var json = File.ReadAllText(_filePath);
                    if (string.IsNullOrWhiteSpace(json))
                        return;

                    var data = JsonSerializer.Deserialize<Dictionary<string, List<ZadatakProjekta>>>(json, JsonOpts);
                    if (data == null)
                        return;

                    _taskByManager.Clear();
                    foreach (var kvp in data)
                        _taskByManager[kvp.Key] = kvp.Value ?? new List<ZadatakProjekta>();
                }
                catch
                {
                    
                }
            }
        }

        private void SaveToDisk()
        {
            lock (_lock)
            {
                var json = JsonSerializer.Serialize(_taskByManager, JsonOpts);

                var tmp = _filePath + ".tmp";
                File.WriteAllText(tmp, json);
                File.Copy(tmp, _filePath, true);
                File.Delete(tmp);
            }
        }

        private static int StatusRank(StatusZadatka s) => s switch
        {
            StatusZadatka.UToku => 0,
            StatusZadatka.NaCekanju => 1,
            StatusZadatka.Zavrsen => 2,
            _ => 9
        };

        public void EnsureManager(string managerUsername)
        {
            if (string.IsNullOrWhiteSpace(managerUsername))
                return;

            lock (_lock)
            {
                if (!_taskByManager.ContainsKey(managerUsername))
                {
                    _taskByManager[managerUsername] = new List<ZadatakProjekta>();
                    SaveToDisk();
                }
            }
        }

        public void AddTask(string managerUsername, ZadatakProjekta task)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            if (string.IsNullOrWhiteSpace(managerUsername))
                throw new ArgumentException("Manager username is empty.", nameof(managerUsername));

            lock (_lock)
            {
                EnsureManager(managerUsername);

                task.Status = StatusZadatka.NaCekanju;
                _taskByManager[managerUsername].Add(task);

                SaveToDisk();
            }
        }

        public bool TryChangeTaskPriority(string managerUsername, string taskName, int newPriority)
        {
            lock (_lock)
            {
                if (!_taskByManager.TryGetValue(managerUsername, out var list))
                    return false;

                var task = list.FirstOrDefault(t =>
                    string.Equals(t.Naziv, taskName, StringComparison.OrdinalIgnoreCase));

                if (task == null)
                    return false;

                task.Prioritet = newPriority;
                SaveToDisk();
                return true;
            }
        }

        public List<(string ManagerUsername, ZadatakProjekta Task)> GetTasksForEmployeeWithManager(string employeeUsername)
        {
            if (string.IsNullOrWhiteSpace(employeeUsername))
                return new();

            lock (_lock)
            {
                return _taskByManager
                    .SelectMany(kvp => kvp.Value
                        .Where(t => string.Equals(t.Zaposleni, employeeUsername, StringComparison.OrdinalIgnoreCase))
                        .Select(t => (kvp.Key, t)))
                    .OrderBy(x => StatusRank(x.t.Status))
                    .ThenBy(x => x.t.Prioritet)
                    .ThenBy(x => x.t.Rok)
                    .ThenBy(x => x.t.Naziv, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        public bool TrySetStatus(string taskName, StatusZadatka newStatus)
        {
            if (string.IsNullOrWhiteSpace(taskName))
                return false;

            lock (_lock)
            {
                foreach (var list in _taskByManager.Values)
                {
                    var t = list.FirstOrDefault(x =>
                        string.Equals(x.Naziv, taskName, StringComparison.OrdinalIgnoreCase));

                    if (t == null) continue;

                    t.Status = newStatus;
                    SaveToDisk();
                    return true;
                }
                return false;
            }
        }

        public bool TryCompleteTask(string taskName, string? comment)
        {
            if (string.IsNullOrWhiteSpace(taskName))
                return false;

            lock (_lock)
            {
                foreach (var list in _taskByManager.Values)
                {
                    var t = list.FirstOrDefault(x =>
                        string.Equals(x.Naziv, taskName, StringComparison.OrdinalIgnoreCase));

                    if (t == null) continue;

                    t.Status = StatusZadatka.Zavrsen;
                    t.Komentar = (comment ?? "").Trim();

                    SaveToDisk();
                    return true;
                }
                return false;
            }
        }

        public List<ZadatakProjekta> GetAllTasksForManager(string managerUsername)
        {
            if (string.IsNullOrWhiteSpace(managerUsername))
                return new();

            lock (_lock)
            {
                if (!_taskByManager.TryGetValue(managerUsername, out var list))
                    return new();

                return list
                    .OrderBy(t => StatusRank(t.Status))
                    .ThenBy(t => t.Prioritet)
                    .ThenBy(t => t.Rok)
                    .ToList();
            }
        }

        public void DebugPrint()
        {
            lock (_lock)
            {
                Console.WriteLine("===== TASK STORE =====");
                foreach (var kvp in _taskByManager)
                {
                    Console.WriteLine($"MENADZER: {kvp.Key}");
                    foreach (var t in kvp.Value)
                    {
                        Console.WriteLine(
                            $" - {t.Naziv} | {t.Zaposleni} | {t.Status} | P:{t.Prioritet} | Rok:{t.Rok:yyyy-MM-dd}");
                    }
                }
            }
        }
    }
}
