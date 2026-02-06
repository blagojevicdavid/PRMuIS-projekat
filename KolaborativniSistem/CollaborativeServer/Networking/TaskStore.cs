using Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollaborativeServer.Networking
{
    public sealed class TaskStore
    {
        private readonly Dictionary<string, List<ZadatakProjekta>> _taskByManager = new();
        private readonly object _lock = new();

        private static int StatusRank(StatusZadatka s) => s switch
        {
            StatusZadatka.UToku => 0,
            StatusZadatka.NaCekanju => 1,
            StatusZadatka.Zavrsen => 2,
            _ => 9
        };

        // prijava menadzera
        public void EnsureManager(string managerUsername)
        {
            if (string.IsNullOrWhiteSpace(managerUsername))
                return;

            lock (_lock)
            {
                if (!_taskByManager.ContainsKey(managerUsername))
                    _taskByManager[managerUsername] = new List<ZadatakProjekta>();
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
            }
        }

        public List<ZadatakProjekta> GetInProgressTasks(string managerUsername)
        {
            if (string.IsNullOrWhiteSpace(managerUsername))
                return new List<ZadatakProjekta>();

            lock (_lock)
            {
                if (!_taskByManager.TryGetValue(managerUsername, out var list))
                    return new List<ZadatakProjekta>();

                return list
                    .Where(t => t.Status == StatusZadatka.UToku)
                    .ToList();
            }
        }

        public bool TryChangeTaskPriority(string managerUsername, string taskName, int newPriority)
        {
            if (!_taskByManager.TryGetValue(managerUsername, out var list))
                return false;

            var task = list.Find(t => string.Equals(t.Naziv, taskName, StringComparison.OrdinalIgnoreCase));
            if (task == null)
                return false;

            task.Prioritet = newPriority;
            return true;
        }


        public List<(string ManagerUsername, ZadatakProjekta Task)> GetTasksForEmployeeWithManager(string employeeUsername)
        {
            if (string.IsNullOrWhiteSpace(employeeUsername))
                return new List<(string, ZadatakProjekta)>();

            lock (_lock)
            {
                return _taskByManager
                    .SelectMany(kvp => kvp.Value
                        .Where(t => string.Equals(t.Zaposleni, employeeUsername, StringComparison.OrdinalIgnoreCase))
                        .Select(t => (ManagerUsername: kvp.Key, Task: t)))
                    .OrderBy(x => StatusRank(x.Task.Status))
                    .ThenBy(x => x.Task.Prioritet)
                    .ThenBy(x => x.Task.Rok)
                    .ThenBy(x => x.Task.Naziv, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        public bool TrySetStatus(string taskName, StatusZadatka newStatus, out StatusZadatka oldStatus)
        {
            oldStatus = default;

            if (string.IsNullOrWhiteSpace(taskName))
                return false;

            lock (_lock)
            {
                foreach (var list in _taskByManager.Values)
                {
                    var t = list.FirstOrDefault(x =>
                        string.Equals(x.Naziv, taskName, StringComparison.OrdinalIgnoreCase));

                    if (t == null) continue;

                    oldStatus = t.Status;
                    t.Status = newStatus;
                    return true;
                }

                return false;
            }
        }

        public bool TrySetStatus(string taskName, StatusZadatka newStatus)
        {
            return TrySetStatus(taskName, newStatus, out _);
        }

        public bool TryCompleteTask(string taskName, string? comment, out StatusZadatka oldStatus)
        {
            oldStatus = default;

            if (string.IsNullOrWhiteSpace(taskName))
                return false;

            lock (_lock)
            {
                foreach (var list in _taskByManager.Values)
                {
                    var t = list.FirstOrDefault(x =>
                        string.Equals(x.Naziv, taskName, StringComparison.OrdinalIgnoreCase));

                    if (t == null) continue;

                    oldStatus = t.Status;
                    t.Status = StatusZadatka.Zavrsen;
                    t.Komentar = (comment ?? "").Trim();
                    return true;
                }

                return false;
            }
        }

        public bool TryCompleteTask(string taskName, string? comment)
        {
            return TryCompleteTask(taskName, comment, out _);
        }

        public void DebugPrint()
        {
            lock (_lock)
            {
                Console.WriteLine("===== TASK STORE DICT STATE =====");

                if (_taskByManager.Count == 0)
                {
                    Console.WriteLine("(prazno)");
                    return;
                }

                foreach (var kvp in _taskByManager)
                {
                    Console.WriteLine($"MENADZER: {kvp.Key}");

                    if (kvp.Value.Count == 0)
                    {
                        Console.WriteLine("  (nema zadataka)");
                        continue;
                    }

                    foreach (var t in kvp.Value)
                    {
                        Console.WriteLine(
                            $"  - {t.Naziv} | Zaposleni: {t.Zaposleni} | Status: {t.Status} | Prioritet: {t.Prioritet} | Rok: {t.Rok:yyyy-MM-dd} | Komentar: {t.Komentar}"
                        );
                    }
                }

                Console.WriteLine("============================");
            }
        }

        public List<ZadatakProjekta> GetAllTasksForManager(string managerUsername)
        {
            if (string.IsNullOrWhiteSpace(managerUsername))
                return new List<ZadatakProjekta>();

            lock (_lock)
            {
                if (!_taskByManager.TryGetValue(managerUsername, out var list))
                    return new List<ZadatakProjekta>();

                var uToku = list
                    .Where(t => t.Status == StatusZadatka.UToku)
                    .OrderBy(t => t.Prioritet)
                    .ToList();

                var naCekanju = list
                    .Where(t => t.Status == StatusZadatka.NaCekanju)
                    .OrderBy(t => t.Prioritet)
                    .ToList();

                var zavrseni = list
                    .Where(t => t.Status == StatusZadatka.Zavrsen)
                    .ToList();

                uToku.AddRange(naCekanju);
                uToku.AddRange(zavrseni);
                return uToku;
            }
        }

        private static string SanitizeComment(string? comment)
        {
            if (string.IsNullOrWhiteSpace(comment)) return "";

            return comment
                .Replace("|", " ")
                .Replace(";", " ")
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
        }

        public string GetAllTasks(string managerUsername)
        {
            if (string.IsNullOrWhiteSpace(managerUsername))
                return string.Empty;

            lock (_lock)
            {
                if (!_taskByManager.TryGetValue(managerUsername, out var tasks))
                    return string.Empty;

                var uToku = tasks.Where(t => t.Status == StatusZadatka.UToku)
                                 .OrderBy(t => t.Prioritet);

                var naCekanju = tasks.Where(t => t.Status == StatusZadatka.NaCekanju)
                                     .OrderBy(t => t.Prioritet);

                var zavrseni = tasks.Where(t => t.Status == StatusZadatka.Zavrsen);

                var sb = new StringBuilder();

                foreach (var t in uToku.Concat(naCekanju).Concat(zavrseni))
                {
                    if (sb.Length > 0)
                        sb.Append(";");

                    string komentar = SanitizeComment(t.Komentar);

                    sb.Append($"{t.Naziv}|{t.Zaposleni}|{t.Rok:yyyy-MM-dd}|{t.Prioritet}|{(int)t.Status}|{komentar}");
                }

                return sb.ToString();
            }
        }
    }
}
