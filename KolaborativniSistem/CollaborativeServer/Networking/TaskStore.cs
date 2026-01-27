using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Shared.Models;

namespace CollaborativeServer.Networking
{
    public sealed class TaskStore
    {
        private readonly Dictionary<string, List<ZadatakProjekta>> _taskByManager = new();
        private readonly object _lock = new();

        // prijava menadzera
        public void EnsureManager(string managerUsername)
        {
            if (string.IsNullOrWhiteSpace(managerUsername))
                return;

            lock (_lock)
            {
                if (!_taskByManager.ContainsKey(managerUsername))
                    _taskByManager[managerUsername] = new List<ZadatakProjekta>(); //dodavanje prazne liste ako ne postoji dict sa tim menadzerom
            }
        }

        //Dodavanje zadatka
        public void AddTask(string managerUsername, ZadatakProjekta task)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            if (string.IsNullOrWhiteSpace(managerUsername))
                throw new ArgumentException("Manager username is empty.", nameof(managerUsername));

            lock (_lock)
            {
                EnsureManager(managerUsername);

                // 
                task.Status = StatusZadatka.NaCekanju;

                _taskByManager[managerUsername].Add(task);
            }
        }

        // vraca sve zadatke kojima je status "U Toku"
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

        //povećanje prioriteta
        public bool TryIncreasePriority(string managerUsername, string taskName, int newPriority)
        {
            if (string.IsNullOrWhiteSpace(managerUsername) || string.IsNullOrWhiteSpace(taskName))
                return false;

            lock (_lock)
            {
                if (!_taskByManager.TryGetValue(managerUsername, out var list))
                    return false;

                var t = list.FirstOrDefault(x =>
                    string.Equals(x.Naziv, taskName, StringComparison.OrdinalIgnoreCase));

                if (t == null) return false;

                t.Prioritet = newPriority;
                return true;
            }
        }

        //vrati sve zadatke dodeljene korisniku po prioritetu rastuće
        public List<ZadatakProjekta> GetTasksForEmployee(string employeeUsername)
        {
            if (string.IsNullOrWhiteSpace(employeeUsername))
                return new List<ZadatakProjekta>();

            lock (_lock)
            {
                return _taskByManager.Values
                    .SelectMany(x => x)
                    .Where(t => string.Equals(t.Zaposleni, employeeUsername, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(t => t.Prioritet)
                    .ToList();
            }
        }

        //Promena statusa zadatka po nazivu
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
                    return true;
                }

                return false;
            }
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
                            $"  - {t.Naziv} | Zaposleni: {t.Zaposleni} | Status: {t.Status} | Prioritet: {t.Prioritet} | Rok: {t.Rok:yyyy-MM-dd}"
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

                //soritrano po prioritetu
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

                //spojeno u jednu listu
                uToku.AddRange(naCekanju);
                uToku.AddRange(zavrseni);
                return uToku;
            }
        }

        public string GetAllTasks(string managerUsername)
        {
            if (string.IsNullOrWhiteSpace(managerUsername))
                return string.Empty;
        
            lock(_lock)
            {
                if(!_taskByManager.TryGetValue(managerUsername, out var tasks))
                    return string.Empty;

                var uToku = tasks.Where(t => t.Status == StatusZadatka.UToku)
                                 .OrderBy(t => t.Prioritet);

                var naCekanju = tasks.Where(t => t.Status == StatusZadatka.NaCekanju)
                                    .OrderBy(t => t.Prioritet);

                var zavrseni = tasks.Where(t => t.Status == StatusZadatka.Zavrsen);

                var sb = new StringBuilder();

                foreach (var t in uToku.Concat(naCekanju).Concat(zavrseni))
                {
                    if(sb.Length > 0)
                        sb.Append(";");

                    sb.Append($"{t.Naziv}|{t.Zaposleni}|{t.Rok:yyyy-MM-dd}|{t.Prioritet}|{(int)t.Status}");
                }
                return sb.ToString();
            }
        }
    }
}
