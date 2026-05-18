using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UsualIntakeAnalyzer.Models;

namespace UsualIntakeAnalyzer.Services
{
    /// <summary>
    /// AppData 폴더 기반 영구 저장소.
    /// - x0/x1 데이터셋: 차수(SurveyRound) 단위
    /// - 코드집: 전역 1개로 운영 (새 업로드 시 기존 교체)
    /// </summary>
    public static class AppDataService
    {
        private static string _root         = "";
        private static string _dataDir      = "";
        private static string _metaFile     = "";
        private static string _codebookXlsx = "";
        private static string _codebookMeta = "";

        public static void Initialize()
        {
            _root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "UsualIntakeAnalyzer");
            _dataDir      = Path.Combine(_root, "data");
            _metaFile     = Path.Combine(_root, "datasets.json");
            _codebookXlsx = Path.Combine(_root, "codebook.xlsx");
            _codebookMeta = Path.Combine(_root, "codebook.json");

            Directory.CreateDirectory(_root);
            Directory.CreateDirectory(_dataDir);

            // 자료원(사업/기수/차수) 시드 + 활성 차수 초기화
            SurveySourceService.Initialize(_root);

            // 산출식품 프리셋 + 결과 캐시 저장소
            FoodPresetService.Initialize(_root);

            // 시나리오 저장소
            ScenarioService.Initialize(_root);

            // 식품군 DB (시드 포함)
            FoodGroupService.Initialize(_root);

            // 구버전 차수별 코드집 → 단일 전역 코드집으로 마이그레이션
            MigrateMultiCodebooksIfAny();

            // 구버전 데이터셋(차수 미지정) 자동 귀속
            MigrateOrphanDatasetsIfAny();
        }

        /// <summary>RoundId 가 비어있는 기존 데이터셋들을 활성 차수로 귀속시킨다.</summary>
        private static void MigrateOrphanDatasetsIfAny()
        {
            var meta = LoadDatasetMeta();
            var orphans = meta.Where(d => string.IsNullOrEmpty(d.RoundId)).ToList();
            if (orphans.Count == 0) return;

            var activeRoundId = SurveySourceService.GetActiveRoundId();
            if (string.IsNullOrEmpty(activeRoundId))
            {
                var first = SurveySourceService.LoadRounds().FirstOrDefault();
                if (first == null) return;
                activeRoundId = first.Id;
            }
            foreach (var d in orphans) d.RoundId = activeRoundId;
            SaveDatasetMeta(meta);
        }

        // ── 데이터셋 메타 ──────────────────────────────────
        public static List<DatasetInfo> LoadDatasetMeta()
        {
            if (!File.Exists(_metaFile)) return new List<DatasetInfo>();
            var json = File.ReadAllText(_metaFile);
            return JsonConvert.DeserializeObject<List<DatasetInfo>>(json) ?? new();
        }

        public static void SaveDatasetMeta(List<DatasetInfo> list)
        {
            File.WriteAllText(_metaFile, JsonConvert.SerializeObject(list, Formatting.Indented));
        }

        // ── 국건영 CSV 저장/로드/삭제 ──────────────────────
        public static string SaveDatasetCsv(string sourcePath, string id)
        {
            var dest = Path.Combine(_dataDir, id + ".csv");
            File.Copy(sourcePath, dest, overwrite: true);
            return dest;
        }

        /// <summary>확장자를 지정해 파일 저장 (정밀영양 xlsx 등 비-CSV 포맷용).</summary>
        public static string SaveDatasetFile(string sourcePath, string id, string ext)
        {
            var dest = Path.Combine(_dataDir, id + ext);
            File.Copy(sourcePath, dest, overwrite: true);
            return dest;
        }

        public static string GetDatasetCsvPath(string id)
            => Path.Combine(_dataDir, id + ".csv");

        public static void DeleteDataset(string id)
        {
            var meta = LoadDatasetMeta();
            var entry = meta.FirstOrDefault(d => d.Id == id);
            meta.RemoveAll(d => d.Id == id);
            SaveDatasetMeta(meta);

            // CSV 또는 xlsx 둘 다 시도해서 삭제
            var ext = (entry?.FileName is string fn)
                      ? Path.GetExtension(fn)
                      : ".csv";
            var path = Path.Combine(_dataDir, id + ext);
            if (!File.Exists(path))
                path = Path.Combine(_dataDir, id + ".csv");   // 하위 호환
            if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>특정 차수의 데이터셋 조회</summary>
        public static List<DatasetInfo> GetDatasetsByRound(string roundId)
            => LoadDatasetMeta().Where(d => d.RoundId == roundId).ToList();

        /// <summary>등록된 모든 x0 또는 x1 데이터셋 조회</summary>
        public static List<DatasetInfo> GetDatasetsByType(DatasetType type)
            => LoadDatasetMeta().Where(d => d.Type == type)
                                 .OrderByDescending(d => d.RegisteredAt)
                                 .ToList();

        // ── 코드집 (전역 1개) ────────────────────────────────────
        public static CodebookInfo? LoadCodebookInfo()
        {
            if (!File.Exists(_codebookMeta)) return null;
            try
            {
                return JsonConvert.DeserializeObject<CodebookInfo>(
                    File.ReadAllText(_codebookMeta));
            }
            catch { return null; }
        }

        public static bool CodebookExists()
            => File.Exists(_codebookXlsx) && File.Exists(_codebookMeta);

        public static string GetCodebookPath()
            => _codebookXlsx;

        /// <summary>전역 코드집 등록 — 기존 코드집은 교체 삭제. 시드 식품군 코드도 즉시 재확장.</summary>
        public static CodebookInfo SaveCodebook(string sourcePath, int rowCount)
        {
            File.Copy(sourcePath, _codebookXlsx, overwrite: true);
            var info = new CodebookInfo
            {
                RoundId    = "",   // 전역
                UploadedAt = DateTime.Now,
                FileName   = Path.GetFileName(sourcePath),
                RowCount   = rowCount
            };
            File.WriteAllText(_codebookMeta,
                JsonConvert.SerializeObject(info, Formatting.Indented));
            // 새 코드집에 맞춰 시드 식품군의 1차코드 자동 재확장
            try { FoodGroupService.ExpandCodesFromCodebook(); } catch { }
            return info;
        }

        public static void DeleteCodebook()
        {
            if (File.Exists(_codebookXlsx)) File.Delete(_codebookXlsx);
            if (File.Exists(_codebookMeta)) File.Delete(_codebookMeta);
        }

        // ── (호환) 활성 차수 기반 데이터셋 — 분석 탭이 직접 골랐으므로 더 이상 사용 안 함 ──
        public static DatasetInfo? GetActiveDataset(DatasetType type)
        {
            // 분석 탭이 x0/x1을 직접 선택하므로 이 헬퍼는 deprecated.
            // 외부 호환을 위해 가장 최근 등록 데이터셋 반환.
            return GetDatasetsByType(type).FirstOrDefault();
        }

        // ── 구버전 차수별 코드집 → 전역 코드집 마이그레이션 ─────
        private static void MigrateMultiCodebooksIfAny()
        {
            var legacyDir  = Path.Combine(_root, "codebooks");
            var legacyMeta = Path.Combine(_root, "codebooks.json");

            // 신규 구조에서 이미 전역 코드집이 있으면 스킵
            if (File.Exists(_codebookMeta) && File.Exists(_codebookXlsx))
            {
                // 구버전 잔재 정리
                CleanupLegacyCodebookStorage(legacyDir, legacyMeta);
                return;
            }

            // 구버전 다중 코드집 자료 발견 → 가장 최근 것을 전역으로 복사
            if (File.Exists(legacyMeta) && Directory.Exists(legacyDir))
            {
                try
                {
                    var list = JsonConvert.DeserializeObject<List<CodebookInfo>>(
                        File.ReadAllText(legacyMeta)) ?? new();
                    var latest = list.OrderByDescending(c => c.UploadedAt).FirstOrDefault();
                    if (latest != null)
                    {
                        var src = Path.Combine(legacyDir, latest.Id + ".xlsx");
                        if (File.Exists(src))
                        {
                            File.Copy(src, _codebookXlsx, overwrite: true);
                            var info = new CodebookInfo
                            {
                                RoundId    = "",
                                UploadedAt = latest.UploadedAt,
                                FileName   = latest.FileName,
                                RowCount   = latest.RowCount
                            };
                            File.WriteAllText(_codebookMeta,
                                JsonConvert.SerializeObject(info, Formatting.Indented));
                        }
                    }
                }
                catch { /* 이전 데이터 손상 — 무시 */ }
                CleanupLegacyCodebookStorage(legacyDir, legacyMeta);
                return;
            }

            // 더 오래된 구버전(단일 codebook.xlsx 가 없고 별도 codebook.json만)도 함께 처리
            // (현재 _codebookXlsx 와 동일 경로라 위 체크에서 걸러짐)
        }

        private static void CleanupLegacyCodebookStorage(string legacyDir, string legacyMeta)
        {
            try
            {
                if (Directory.Exists(legacyDir))
                {
                    foreach (var f in Directory.GetFiles(legacyDir))
                    {
                        try { File.Delete(f); } catch { }
                    }
                    try { Directory.Delete(legacyDir); } catch { }
                }
                if (File.Exists(legacyMeta)) File.Delete(legacyMeta);
            }
            catch { /* 정리 실패 — 무시 */ }
        }
    }
}
