using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UsualIntakeAnalyzer.Models;

namespace UsualIntakeAnalyzer.Services
{
    /// <summary>
    /// 자료원(사업/기수/차수) 영구 저장 + CRUD 서비스.
    /// 첫 실행 시 KNHANES 1~9기 / KPNC 1기를 시드한다.
    /// </summary>
    public static class SurveySourceService
    {
        private static string _projectsFile = "";
        private static string _phasesFile   = "";
        private static string _roundsFile   = "";
        private static string _activeFile   = "";

        public static void Initialize(string root)
        {
            _projectsFile = Path.Combine(root, "survey_projects.json");
            _phasesFile   = Path.Combine(root, "survey_phases.json");
            _roundsFile   = Path.Combine(root, "survey_rounds.json");
            _activeFile   = Path.Combine(root, "active_source.json");

            // 최초 실행 시 시드
            if (!File.Exists(_projectsFile)) Seed();
        }

        // ── 사업 (Project) ───────────────────────────────────────────────
        public static List<SurveyProject> LoadProjects()
            => File.Exists(_projectsFile)
                ? JsonConvert.DeserializeObject<List<SurveyProject>>(File.ReadAllText(_projectsFile))
                  ?? new List<SurveyProject>()
                : new List<SurveyProject>();

        public static void SaveProjects(List<SurveyProject> list)
            => File.WriteAllText(_projectsFile, JsonConvert.SerializeObject(list, Formatting.Indented));

        public static SurveyProject AddProject(SurveyProject p)
        {
            var list = LoadProjects();
            list.Add(p);
            SaveProjects(list);
            return p;
        }

        public static void UpdateProject(SurveyProject p)
        {
            var list = LoadProjects();
            var idx = list.FindIndex(x => x.Id == p.Id);
            if (idx >= 0) list[idx] = p;
            SaveProjects(list);
        }

        public static void DeleteProject(string projectId)
        {
            var projects = LoadProjects();
            projects.RemoveAll(x => x.Id == projectId);
            SaveProjects(projects);

            // 하위 기수/차수도 같이 정리
            var phases = LoadPhases();
            var phaseIds = phases.Where(p => p.ProjectId == projectId)
                                 .Select(p => p.Id).ToList();
            phases.RemoveAll(p => p.ProjectId == projectId);
            SavePhases(phases);

            var rounds = LoadRounds();
            rounds.RemoveAll(r => phaseIds.Contains(r.PhaseId));
            SaveRounds(rounds);
        }

        // ── 기수 (Phase) ─────────────────────────────────────────────────
        public static List<SurveyPhase> LoadPhases()
            => File.Exists(_phasesFile)
                ? JsonConvert.DeserializeObject<List<SurveyPhase>>(File.ReadAllText(_phasesFile))
                  ?? new List<SurveyPhase>()
                : new List<SurveyPhase>();

        public static void SavePhases(List<SurveyPhase> list)
            => File.WriteAllText(_phasesFile, JsonConvert.SerializeObject(list, Formatting.Indented));

        public static SurveyPhase AddPhase(SurveyPhase p)
        {
            var list = LoadPhases();
            list.Add(p);
            SavePhases(list);
            return p;
        }

        public static void UpdatePhase(SurveyPhase p)
        {
            var list = LoadPhases();
            var idx = list.FindIndex(x => x.Id == p.Id);
            if (idx >= 0) list[idx] = p;
            SavePhases(list);
        }

        public static void DeletePhase(string phaseId)
        {
            var phases = LoadPhases();
            phases.RemoveAll(p => p.Id == phaseId);
            SavePhases(phases);

            var rounds = LoadRounds();
            rounds.RemoveAll(r => r.PhaseId == phaseId);
            SaveRounds(rounds);
        }

        // ── 차수 (Round) ─────────────────────────────────────────────────
        public static List<SurveyRound> LoadRounds()
            => File.Exists(_roundsFile)
                ? JsonConvert.DeserializeObject<List<SurveyRound>>(File.ReadAllText(_roundsFile))
                  ?? new List<SurveyRound>()
                : new List<SurveyRound>();

        public static void SaveRounds(List<SurveyRound> list)
            => File.WriteAllText(_roundsFile, JsonConvert.SerializeObject(list, Formatting.Indented));

        public static SurveyRound AddRound(SurveyRound r)
        {
            var list = LoadRounds();
            list.Add(r);
            SaveRounds(list);
            return r;
        }

        public static void UpdateRound(SurveyRound r)
        {
            var list = LoadRounds();
            var idx = list.FindIndex(x => x.Id == r.Id);
            if (idx >= 0) list[idx] = r;
            SaveRounds(list);
        }

        public static void DeleteRound(string roundId)
        {
            var rounds = LoadRounds();
            rounds.RemoveAll(r => r.Id == roundId);
            SaveRounds(rounds);
        }

        // ── 활성 차수 ────────────────────────────────────────────────────
        public static string GetActiveRoundId()
        {
            if (!File.Exists(_activeFile)) return "";
            var st = JsonConvert.DeserializeObject<ActiveSourceState>(File.ReadAllText(_activeFile));
            return st?.ActiveRoundId ?? "";
        }

        public static void SetActiveRoundId(string roundId)
        {
            var st = new ActiveSourceState { ActiveRoundId = roundId };
            File.WriteAllText(_activeFile, JsonConvert.SerializeObject(st, Formatting.Indented));
        }

        // ── 시드 데이터 (첨부 스키마 기반) ──────────────────────────────
        private static void Seed()
        {
            var knhanes = new SurveyProject
            {
                ProjectCode   = "KNHANES",
                NameKo        = "국민건강영양조사",
                NameEn        = "Korea National Health and Nutrition Examination Survey",
                ConductingOrg = "질병관리청",
                CommissionOrg = "보건복지부",
                SurveyDomain  = "건강·영양·검진",
                Description   = "매년 시행, 복합표본설계",
                IsBuiltIn     = true
            };
            var kpnc = new SurveyProject
            {
                ProjectCode   = "KPNC",
                NameKo        = "정밀영양조사사업",
                NameEn        = "Korea Nutrition Precision Survey",
                ConductingOrg = "가천대학교",
                CommissionOrg = "식품의약품안전처",
                SurveyDomain  = "취약계층 정밀영양 조사",
                Description   = "매년 시행, 국가바이오통합빅데이터 연계",
                IsBuiltIn     = true
            };
            SaveProjects(new List<SurveyProject> { knhanes, kpnc });

            // 기수
            var phases = new List<SurveyPhase>
            {
                new() { ProjectId = knhanes.Id, PhaseNo = 1, PhaseLabel = "제1기",
                        YearStart = 1998, YearEnd = 1998, Status = "완료",
                        SampleSize = 8110, Notes = "국민건강영양조사 시작", IsBuiltIn = true },
                new() { ProjectId = knhanes.Id, PhaseNo = 2, PhaseLabel = "제2기",
                        YearStart = 2001, YearEnd = 2002, Status = "완료",
                        SampleSize = 7500, IsBuiltIn = true },
                new() { ProjectId = knhanes.Id, PhaseNo = 3, PhaseLabel = "제3기",
                        YearStart = 2005, YearEnd = 2007, Status = "완료",
                        SampleSize = 7090, IsBuiltIn = true },
                new() { ProjectId = knhanes.Id, PhaseNo = 4, PhaseLabel = "제4기",
                        YearStart = 2007, YearEnd = 2009, Status = "완료",
                        SampleSize = 7480, Notes = "연중 순환 표본 도입", IsBuiltIn = true },
                new() { ProjectId = knhanes.Id, PhaseNo = 5, PhaseLabel = "제5기",
                        YearStart = 2010, YearEnd = 2012, Status = "완료",
                        SampleSize = 7560, IsBuiltIn = true },
                new() { ProjectId = knhanes.Id, PhaseNo = 6, PhaseLabel = "제6기",
                        YearStart = 2013, YearEnd = 2015, Status = "완료",
                        SampleSize = 7500, IsBuiltIn = true },
                new() { ProjectId = knhanes.Id, PhaseNo = 7, PhaseLabel = "제7기",
                        YearStart = 2016, YearEnd = 2018, Status = "완료", IsBuiltIn = true },
                new() { ProjectId = knhanes.Id, PhaseNo = 8, PhaseLabel = "제8기",
                        YearStart = 2019, YearEnd = 2021, Status = "완료",
                        Notes = "코로나19로 2020년 중단", IsBuiltIn = true },
                new() { ProjectId = knhanes.Id, PhaseNo = 9, PhaseLabel = "제9기",
                        YearStart = 2022, YearEnd = 2024, Status = "진행중", IsBuiltIn = true },
                new() { ProjectId = kpnc.Id,    PhaseNo = 1, PhaseLabel = "제1기",
                        YearStart = 2025, YearEnd = 2029, Status = "진행중",
                        SampleSize = 7500, IsBuiltIn = true }
            };
            SavePhases(phases);

            // 차수 (제8기, KPNC 제1기만 시드)
            var phs8  = phases.First(p => p.ProjectId == knhanes.Id && p.PhaseNo == 8);
            var phsK1 = phases.First(p => p.ProjectId == kpnc.Id    && p.PhaseNo == 1);

            var rounds = new List<SurveyRound>
            {
                new() { PhaseId = phs8.Id,  RoundNo = 1, SurveyYear = 2019,
                        FieldEnd = "2019-12-31", Status = "완료", IsBuiltIn = true },
                new() { PhaseId = phs8.Id,  RoundNo = 2, SurveyYear = 2020,
                        FieldEnd = "2020-12-31", Status = "중단(코로나19)", IsBuiltIn = true },
                new() { PhaseId = phs8.Id,  RoundNo = 3, SurveyYear = 2021,
                        FieldEnd = "2021-12-31", Status = "완료", IsBuiltIn = true },
                new() { PhaseId = phsK1.Id, RoundNo = 1, SurveyYear = 2025,
                        FieldEnd = "", Status = "진행중", IsBuiltIn = true }
            };
            SaveRounds(rounds);

            // 활성 차수: 가장 최근 KNHANES 차수 (8기 3차)
            var defaultActive = rounds.FirstOrDefault(r =>
                r.PhaseId == phs8.Id && r.RoundNo == 3);
            if (defaultActive != null)
                SetActiveRoundId(defaultActive.Id);
        }

        // ── 편의 조회 ────────────────────────────────────────────────────
        /// <summary>차수 → 기수 → 사업 풀 정보를 묶어 반환</summary>
        public static (SurveyProject? project, SurveyPhase? phase, SurveyRound? round)
            GetRoundContext(string roundId)
        {
            if (string.IsNullOrEmpty(roundId)) return (null, null, null);
            var round = LoadRounds().FirstOrDefault(r => r.Id == roundId);
            if (round == null) return (null, null, null);
            var phase = LoadPhases().FirstOrDefault(p => p.Id == round.PhaseId);
            if (phase == null) return (null, null, round);
            var project = LoadProjects().FirstOrDefault(pj => pj.Id == phase.ProjectId);
            return (project, phase, round);
        }
    }
}
