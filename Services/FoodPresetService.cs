using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UsualIntakeAnalyzer.Models;

namespace UsualIntakeAnalyzer.Services
{
    /// <summary>
    /// 산출식품(FoodPreset) 영구 저장 + 분석 결과 캐시 관리.
    /// 캐시 키는 (차수Id + 시뮬횟수 + 정렬된 식품코드 집합) 의 SHA1.
    /// </summary>
    public static class FoodPresetService
    {
        private static string _root      = "";
        private static string _metaFile  = "";
        private static string _cacheDir  = "";

        public static void Initialize(string root)
        {
            _root     = root;
            _metaFile = Path.Combine(root, "food_presets.json");
            _cacheDir = Path.Combine(root, "preset_cache");
            Directory.CreateDirectory(_cacheDir);
        }

        // ── 프리셋 CRUD ──────────────────────────────────────────────────
        public static List<FoodPreset> LoadAll()
        {
            if (!File.Exists(_metaFile)) return new List<FoodPreset>();
            try
            {
                return JsonConvert.DeserializeObject<List<FoodPreset>>(
                    File.ReadAllText(_metaFile)) ?? new List<FoodPreset>();
            }
            catch
            {
                return new List<FoodPreset>();
            }
        }

        public static void SaveAll(List<FoodPreset> list)
            => File.WriteAllText(_metaFile,
                                 JsonConvert.SerializeObject(list, Formatting.Indented));

        public static FoodPreset Add(FoodPreset p)
        {
            var list = LoadAll();
            p.CreatedAt = p.UpdatedAt = DateTime.Now;
            list.Add(p);
            SaveAll(list);
            return p;
        }

        public static void Update(FoodPreset p)
        {
            var list = LoadAll();
            var idx = list.FindIndex(x => x.Id == p.Id);
            if (idx < 0) return;
            p.UpdatedAt = DateTime.Now;
            list[idx]   = p;
            SaveAll(list);
        }

        public static void Delete(string id)
        {
            var list = LoadAll();
            list.RemoveAll(x => x.Id == id);
            SaveAll(list);
        }

        public static FoodPreset? Get(string id)
            => LoadAll().FirstOrDefault(x => x.Id == id);

        // ── 결과 캐시 ────────────────────────────────────────────────────
        /// <summary>
        /// 캐시 키 생성: (x0Id + x1Id + 시뮬횟수 + 정렬된 식품코드)의 SHA1.
        /// 단일 Id 버전 — 하위 호환용.
        /// </summary>
        public static string ComputeCacheKey(
            string x0Id, string x1Id, int simTime, IEnumerable<string> foodCodes)
            => ComputeCacheKey(
                new[] { x0Id }, new[] { x1Id }, simTime, foodCodes);

        /// <summary>
        /// 캐시 키 생성: (정렬된 x0Ids + 정렬된 x1Ids + 시뮬횟수 + 정렬된 식품코드)의 SHA1.
        /// 다중 데이터셋 선택을 지원한다.
        /// </summary>
        public static string ComputeCacheKey(
            IEnumerable<string> x0Ids, IEnumerable<string> x1Ids,
            int simTime, IEnumerable<string> foodCodes)
        {
            // 파서/분석 로직이 변경될 때 이 버전을 올리면 기존 캐시가 자동 무효화된다.
            const string ParserVersion = "v4";

            var s0 = (x0Ids ?? Array.Empty<string>())
                     .Where(s => !string.IsNullOrEmpty(s))
                     .OrderBy(s => s, StringComparer.Ordinal);
            var s1 = (x1Ids ?? Array.Empty<string>())
                     .Where(s => !string.IsNullOrEmpty(s))
                     .OrderBy(s => s, StringComparer.Ordinal);
            var sortedCodes = (foodCodes ?? Array.Empty<string>())
                              .Where(c => !string.IsNullOrEmpty(c))
                              .OrderBy(c => c, StringComparer.Ordinal);
            var raw = $"{ParserVersion}|{string.Join("+", s0)}|{string.Join("+", s1)}|{simTime}|{string.Join(",", sortedCodes)}";
            using var sha = SHA1.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            var sb = new StringBuilder();
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static string GetCachePath(string key)
            => Path.Combine(_cacheDir, key + ".json");

        public static bool HasCache(string key) => File.Exists(GetCachePath(key));

        public static AnalysisResult? LoadCache(string key)
        {
            var path = GetCachePath(key);
            if (!File.Exists(path)) return null;
            try
            {
                return JsonConvert.DeserializeObject<AnalysisResult>(
                    File.ReadAllText(path));
            }
            catch
            {
                return null;
            }
        }

        public static void SaveCache(string key, AnalysisResult result)
        {
            try
            {
                File.WriteAllText(GetCachePath(key),
                    JsonConvert.SerializeObject(result, Formatting.None));
            }
            catch { /* 디스크 오류 시 무시 — 캐시일 뿐 */ }
        }

        /// <summary>(x0Id, x1Id, simTime, foodCodes) 단위로 캐시 존재 여부 조회 — 단일 Id 하위 호환</summary>
        public static (bool hit, DateTime? at) ProbeCache(
            string x0Id, string x1Id, int simTime, IEnumerable<string> foodCodes)
            => ProbeCache(new[] { x0Id }, new[] { x1Id }, simTime, foodCodes);

        /// <summary>(x0Ids, x1Ids, simTime, foodCodes) 단위로 캐시 존재 여부 조회</summary>
        public static (bool hit, DateTime? at) ProbeCache(
            IEnumerable<string> x0Ids, IEnumerable<string> x1Ids,
            int simTime, IEnumerable<string> foodCodes)
        {
            var key  = ComputeCacheKey(x0Ids, x1Ids, simTime, foodCodes);
            var path = GetCachePath(key);
            return File.Exists(path)
                ? (true, File.GetLastWriteTime(path))
                : (false, null);
        }

        /// <summary>
        /// 프리셋의 LastAnalyzedAt 갱신(가장 최근 캐시 시각).
        /// 분석 직후 호출하면 목록에 즉시 반영된다.
        /// </summary>
        public static void TouchAnalyzedAt(string presetId, DateTime at)
        {
            var list = LoadAll();
            var p = list.FirstOrDefault(x => x.Id == presetId);
            if (p == null) return;
            p.LastAnalyzedAt = at;
            SaveAll(list);
        }

        public static void ClearAllCache()
        {
            if (!Directory.Exists(_cacheDir)) return;
            foreach (var f in Directory.GetFiles(_cacheDir, "*.json"))
            {
                try { File.Delete(f); } catch { }
            }
        }
    }
}
