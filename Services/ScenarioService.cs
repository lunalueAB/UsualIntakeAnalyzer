using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UsualIntakeAnalyzer.Models;

namespace UsualIntakeAnalyzer.Services
{
    /// <summary>분석 시나리오 영구 저장 + CRUD.</summary>
    public static class ScenarioService
    {
        private static string _file = "";

        public static void Initialize(string root)
            => _file = Path.Combine(root, "scenarios.json");

        public static List<Scenario> LoadAll()
        {
            if (!File.Exists(_file)) return new List<Scenario>();
            try
            {
                return JsonConvert.DeserializeObject<List<Scenario>>(
                    File.ReadAllText(_file)) ?? new List<Scenario>();
            }
            catch { return new List<Scenario>(); }
        }

        public static void SaveAll(List<Scenario> list)
            => File.WriteAllText(_file,
                                 JsonConvert.SerializeObject(list, Formatting.Indented));

        public static Scenario Add(Scenario s)
        {
            var list = LoadAll();
            s.RegisteredAt = DateTime.Now;
            list.Add(s);
            SaveAll(list);
            return s;
        }

        public static void Update(Scenario s)
        {
            var list = LoadAll();
            var idx = list.FindIndex(x => x.Id == s.Id);
            if (idx < 0) return;
            list[idx] = s;
            SaveAll(list);
        }

        public static void Delete(string id)
        {
            var list = LoadAll();
            list.RemoveAll(x => x.Id == id);
            SaveAll(list);
        }

        public static Scenario? Get(string id)
            => LoadAll().FirstOrDefault(x => x.Id == id);

        public static void TouchAnalyzedAt(string id, DateTime at)
        {
            var list = LoadAll();
            var s = list.FirstOrDefault(x => x.Id == id);
            if (s == null) return;
            s.LastAnalyzedAt = at;
            SaveAll(list);
        }
    }
}
