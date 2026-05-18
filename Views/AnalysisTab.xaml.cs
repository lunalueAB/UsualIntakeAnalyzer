using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using UsualIntakeAnalyzer.Models;
using UsualIntakeAnalyzer.Services;

namespace UsualIntakeAnalyzer.Views
{
    // ── 방법 비교 테이블 행 ──────────────────────────────────────────────────
    public class ComparisonRow
    {
        public string Method  { get; set; } = "";
        public int    N       { get; set; }
        public double Average { get; set; }
        public double Sd      { get; set; }
        public double P25th   { get; set; }
        public double Median  { get; set; }
        public double P75th   { get; set; }
        public double P90th   { get; set; }
        public double P95th   { get; set; }
        public double P99th   { get; set; }

        public ComparisonRow(string method, QuantileRow r)
        {
            Method = method; N = r.N;
            Average = r.Average; Sd = r.Sd;
            P25th = r.P25th; Median = r.Median;
            P75th = r.P75th; P90th = r.P90th;
            P95th = r.P95th; P99th = r.P99th;
        }
    }

    public partial class AnalysisTab : UserControl
    {
        private AnalysisResult? _lastResult;

        // 현재 선택된 시나리오 — 툴바 "분석 실행" / "결과 내보내기"의 대상
        private Scenario? _currentScenario;

        // x0/x1 파일별 SurveyRecord 캐시 — 같은 파일 재분석 시 IO 절감
        private readonly Dictionary<string, List<SurveyRecord>> _x0Cache = new();
        private readonly Dictionary<string, List<SurveyRecord>> _x1Cache = new();

        public AnalysisTab()
        {
            InitializeComponent();
            Loaded += (_, _) => RefreshCodebook();
        }

        // ── 외부 진입점 (DB 변경 시 갱신) ─────────────────────────────────
        public void RefreshCodebook()
        {
            // x0/x1 데이터셋이 바뀌면 SurveyRecord 캐시 무효화
            _x0Cache.Clear();
            _x1Cache.Clear();
            UpdateToolbarState();
        }

        private void UpdateToolbarState()
        {
            bool hasCurrent = _currentScenario != null;
            BtnExportExcel.IsEnabled = _lastResult != null;

            if (hasCurrent)
            {
                TxtCurrentScenario.Text = _currentScenario!.Name;
                BdgCurrentScenario.Visibility = Visibility.Visible;
            }
            else
            {
                BdgCurrentScenario.Visibility = Visibility.Collapsed;
            }
        }

        // ── 등록 (사이드 패널 열기) ─────────────────────────────────────
        private void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            if (!AppDataService.CodebookExists())
            {
                MessageBox.Show("먼저 코드집을 등록하세요. (DB 조회/관리 탭)",
                    "안내", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (AppDataService.GetDatasetsByType(DatasetType.X0).Count == 0 ||
                AppDataService.GetDatasetsByType(DatasetType.X1).Count == 0)
            {
                MessageBox.Show("x0 또는 x1 데이터가 등록되어 있지 않습니다.",
                    "안내", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            OpenSidePanel();
        }

        // ════════════════════════════════════════════════════════════════
        // 사이드 패널 — 시나리오 등록 흐름
        // ════════════════════════════════════════════════════════════════
        public class SideGroupRow
        {
            public FoodGroup Source { get; set; } = new();
            public string Name      => Source.Name;
            public int    FoodCount => Source.FoodNames?.Count ?? 0;
            public int    CodeCount => Source.FoodCodes?.Count ?? 0;
        }

        /// <summary>x0/x1 데이터셋 체크박스 리스트의 개별 항목 ViewModel.</summary>
        public class DatasetCheckItem : INotifyPropertyChanged
        {
            private bool _isChecked;
            private bool _isEnabled;

            public bool IsChecked
            {
                get => _isChecked;
                set { _isChecked = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked))); }
            }
            public bool IsEnabled
            {
                get => _isEnabled;
                set { _isEnabled = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled))); }
            }
            /// <summary>DatasetInfo.Id</summary>
            public string DatasetId { get; set; } = "";
            /// <summary>파일명 + 코드 수 배지 (예: "2016_x1.csv · 1,234코드")</summary>
            public string Label     { get; set; } = "";
            /// <summary>자료원 컨텍스트 (예: "국민건강영양조사 제7기 1차 (2016)")</summary>
            public string SubLabel  { get; set; } = "";

            public event PropertyChangedEventHandler? PropertyChanged;
        }

        private List<SideGroupRow> _sideAllGroups = new();
        private ObservableCollection<SideGroupRow>       _sideShownGroups = new();
        private ObservableCollection<DatasetCheckItem>   _x0Items         = new();
        private ObservableCollection<DatasetCheckItem>   _x1Items         = new();
        private HashSet<string> _x0FcodeUnion = new();
        private Dictionary<string, HashSet<string>> _x0CodeMap = new();
        private Dictionary<string, HashSet<string>> _x1CodeMap = new();

        private void OpenSidePanel()
        {
            BuildX0X1CodeMaps();
            LoadSideGroups();
            BuildSideDatasetCombos();
            TxtSideRegBy.Text = Environment.UserName;
            CboSideSim.SelectedIndex = 2; // 5
            UpdateSideRunEnabled();
            SidePanel.Visibility = Visibility.Visible;
        }

        private void BtnSideClose_Click(object sender, RoutedEventArgs e)
        {
            SidePanel.Visibility = Visibility.Collapsed;
        }

        // x0/x1 fcode 매핑 사전 캐싱
        private void BuildX0X1CodeMaps()
        {
            _x0CodeMap.Clear();
            _x1CodeMap.Clear();
            _x0FcodeUnion = new HashSet<string>(StringComparer.Ordinal);
            foreach (var d in AppDataService.GetDatasetsByType(DatasetType.X0))
            {
                var path = AppDataService.GetDatasetCsvPath(d.Id);
                var set  = File.Exists(path) ? CsvParserService.ScanFCodes(path)
                                              : new HashSet<string>();
                _x0CodeMap[d.Id] = set;
                _x0FcodeUnion.UnionWith(set);
            }
            foreach (var d in AppDataService.GetDatasetsByType(DatasetType.X1))
            {
                var path = AppDataService.GetDatasetCsvPath(d.Id);
                _x1CodeMap[d.Id] = File.Exists(path)
                                    ? CsvParserService.ScanFCodes(path)
                                    : new HashSet<string>();
            }
        }

        // 식품군 목록 — x0 fcode union과 교집합이 있는 식품군만
        private void LoadSideGroups()
        {
            int totalAll = FoodGroupService.LoadAll().Count;
            _sideAllGroups = FoodGroupService.LoadAll()
                .Where(g => g.FoodCodes.Count > 0
                            && g.FoodCodes.Any(c => _x0FcodeUnion.Contains(c)))
                .OrderBy(g => g.Name)
                .Select(g => new SideGroupRow { Source = g })
                .ToList();
            ApplySideGroupFilter();
            GridSideGroups.ItemsSource = _sideShownGroups;

            if (_sideAllGroups.Count == 0)
            {
                if (!AppDataService.CodebookExists())
                    TxtSideGroupHint.Text =
                        "코드집이 없어 식품군 매칭이 불가합니다. DB 조회/관리에서 코드집을 먼저 등록하세요.";
                else if (_x0FcodeUnion.Count == 0)
                    TxtSideGroupHint.Text =
                        "2일 조사 데이터가 없거나 식품 코드가 비어 있습니다.";
                else
                    TxtSideGroupHint.Text =
                        $"등록된 {totalAll}개 식품군 중 현재 2일 조사 데이터의 코드와 일치하는 것이 없습니다. ＋ 시나리오 추가로 직접 만드세요.";
            }
            else
            {
                TxtSideGroupHint.Text =
                    $"전체 {totalAll}개 식품군 중 분석 가능한 {_sideAllGroups.Count}개 표시. 목록에서 선택하세요.";
            }
        }

        private void ApplySideGroupFilter()
        {
            string kw = TxtSideGroupSearch.Text?.Trim() ?? "";
            _sideShownGroups.Clear();
            foreach (var g in _sideAllGroups)
            {
                if (string.IsNullOrEmpty(kw)
                    || g.Source.Name       .Contains(kw, StringComparison.OrdinalIgnoreCase)
                    || g.Source.Description.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    _sideShownGroups.Add(g);
            }
        }

        private void TxtSideGroupSearch_TextChanged(object s, TextChangedEventArgs e)
            => ApplySideGroupFilter();

        // x0/x1 체크박스 목록 — 선택된 식품군의 코드를 포함한 데이터셋만 활성
        private void BuildSideDatasetCombos()
        {
            _x0Items.Clear();
            foreach (var d in AppDataService.GetDatasetsByType(DatasetType.X0))
                _x0Items.Add(BuildDatasetCheckItem(d, _x0CodeMap));

            _x1Items.Clear();
            foreach (var d in AppDataService.GetDatasetsByType(DatasetType.X1))
                _x1Items.Add(BuildDatasetCheckItem(d, _x1CodeMap));

            IcSideX0.ItemsSource = _x0Items;
            IcSideX1.ItemsSource = _x1Items;
            FilterSideCombosBySelectedGroup();
        }

        private static DatasetCheckItem BuildDatasetCheckItem(
            DatasetInfo d, Dictionary<string, HashSet<string>> codeMap)
        {
            var (project, phase, round) = SurveySourceService.GetRoundContext(d.RoundId);
            string ctx = (project != null && phase != null && round != null)
                ? $"{project.NameKo} {phase.PhaseLabel} {round.DisplayLabel}"
                : "(자료원 미지정)";
            int n = codeMap.TryGetValue(d.Id, out var s) ? s.Count : 0;
            string codeNote = n > 0 ? $" · {n:N0}코드" : " · 집계 자료";
            return new DatasetCheckItem
            {
                DatasetId = d.Id,
                Label     = d.FileName + codeNote,
                SubLabel  = ctx,
                IsEnabled = false,
                IsChecked = false
            };
        }

        private void FilterSideCombosBySelectedGroup()
        {
            HashSet<string> codes = GridSideGroups.SelectedItem is SideGroupRow row
                ? new HashSet<string>(row.Source.FoodCodes)
                : new HashSet<string>();

            FilterDatasetItems(_x0Items, _x0CodeMap, codes);
            FilterDatasetItems(_x1Items, _x1CodeMap, codes);
        }

        private static void FilterDatasetItems(
            ObservableCollection<DatasetCheckItem> items,
            Dictionary<string, HashSet<string>> codeMap,
            HashSet<string> selectedCodes)
        {
            foreach (var item in items)
            {
                bool hasCodes = codeMap.TryGetValue(item.DatasetId, out var s) && s.Count > 0;
                bool match;
                if (selectedCodes.Count == 0)  match = false;
                else if (!hasCodes)            match = true;   // 집계 자료 — 코드 무관하게 허용
                else                           match = s!.Overlaps(selectedCodes);

                item.IsEnabled = match;
                if (!match) item.IsChecked = false; // 비활성 항목은 자동 해제
            }
        }

        private void GridSideGroups_SelectionChanged(object sender,
            SelectionChangedEventArgs e)
        {
            if (GridSideGroups.SelectedItem is SideGroupRow row)
            {
                TxtSideGroupHint.Text =
                    $"선택됨: {row.Name}  ·  식품 {row.FoodCount} · 코드 {row.CodeCount}";
                UpdateSideFoodsPreview(row.Source);
            }
            else
            {
                TxtSideGroupHint.Text = "목록에서 식품군을 선택하세요";
                UpdateSideFoodsPreview(null);
            }

            FilterSideCombosBySelectedGroup();
            UpdateSideRunEnabled();
        }

        private void UpdateSideFoodsPreview(FoodGroup? g)
        {
            if (g == null || g.FoodNames.Count == 0)
            {
                TxtSideFoodsHeader.Text   = "포함된 식품명";
                TxtSideFoodsEmpty.Visibility = Visibility.Visible;
                ScvSideFoods.Visibility    = Visibility.Collapsed;
                SideFoodChips.ItemsSource = null;
                return;
            }
            var names = g.FoodNames.OrderBy(n => n).ToList();
            TxtSideFoodsHeader.Text     = $"포함된 식품명  ({names.Count}개)";
            TxtSideFoodsEmpty.Visibility = Visibility.Collapsed;
            ScvSideFoods.Visibility      = Visibility.Visible;
            SideFoodChips.ItemsSource    = names;
        }

        private void OnSideAnyChange(object sender, TextChangedEventArgs e)
            => UpdateSideRunEnabled();

        private void OnDatasetChecked(object sender, RoutedEventArgs e)
            => UpdateSideRunEnabled();

        private void BtnSelectAllX1_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _x1Items.Where(i => i.IsEnabled))
                item.IsChecked = true;
            UpdateSideRunEnabled();
        }

        private void BtnDeselectAllX1_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _x1Items)
                item.IsChecked = false;
            UpdateSideRunEnabled();
        }

        private void BtnSelectAllX0_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _x0Items.Where(i => i.IsEnabled))
                item.IsChecked = true;
            UpdateSideRunEnabled();
        }

        private void BtnDeselectAllX0_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _x0Items)
                item.IsChecked = false;
            UpdateSideRunEnabled();
        }

        private void UpdateSideRunEnabled()
        {
            bool hasGroup = GridSideGroups.SelectedItem is SideGroupRow;
            bool hasX0    = _x0Items.Any(i => i.IsChecked && i.IsEnabled);
            bool hasX1    = _x1Items.Any(i => i.IsChecked && i.IsEnabled);
            BtnSideRun.IsEnabled =
                hasGroup && hasX0 && hasX1 &&
                !string.IsNullOrWhiteSpace(TxtSideRegBy.Text);
        }

        // 사이드의 "+ 시나리오 추가" — 식품군 추가 다이얼로그
        private void BtnSideAddGroup_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new FoodGroupEditDialog { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                FoodGroupService.Add(dlg.Result);
                LoadSideGroups();
                // 새로 만든 식품군을 자동 선택
                var newRow = _sideShownGroups.FirstOrDefault(r => r.Source.Id == dlg.Result.Id);
                if (newRow != null) GridSideGroups.SelectedItem = newRow;
            }
        }

        // "다음 분석하기" — 시나리오 저장 + 분석 실행
        private void BtnSideRun_Click(object sender, RoutedEventArgs e)
        {
            if (GridSideGroups.SelectedItem is not SideGroupRow row) return;

            var x0Ids = _x0Items.Where(i => i.IsChecked && i.IsEnabled)
                                 .Select(i => i.DatasetId).ToList();
            var x1Ids = _x1Items.Where(i => i.IsChecked && i.IsEnabled)
                                 .Select(i => i.DatasetId).ToList();
            int simTime = int.Parse(((ComboBoxItem)CboSideSim.SelectedItem).Content.ToString()!);
            string by   = TxtSideRegBy.Text.Trim();

            var scenario = new Scenario
            {
                Name         = row.Source.Name,
                FoodGroupId  = row.Source.Id,
                FoodCodes    = row.Source.FoodCodes.ToList(),
                FoodNames    = row.Source.FoodNames.ToList(),
                X0Ids        = x0Ids,
                X1Ids        = x1Ids,
                X0Id         = x0Ids.FirstOrDefault() ?? "",  // 하위 호환
                X1Id         = x1Ids.FirstOrDefault() ?? "",  // 하위 호환
                SimTime      = simTime,
                RegisteredBy = by
            };
            var saved = ScenarioService.Add(scenario);
            _currentScenario = saved;
            UpdateToolbarState();

            SidePanel.Visibility = Visibility.Collapsed;
            _ = RunAnalysisAsync(saved);
        }

        // ── 분석 이력 모달 ────────────────────────────────────────────────
        private void BtnHistory_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ScenarioHistoryDialog { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() != true) return;

            if (dlg.OpenScenario != null)
            {
                _currentScenario = dlg.OpenScenario;
                UpdateToolbarState();
                _ = RunAnalysisAsync(_currentScenario);
            }
            else if (dlg.DetailRequested != null)
            {
                OpenDetail(dlg.DetailRequested);
            }
        }

        private void OpenDetail(Scenario? s)
        {
            if (s == null) return;
            var dlg = new ScenarioDetailDialog(s) { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() != true) return;

            switch (dlg.Action)
            {
                case ScenarioDetailDialog.DetailAction.Analyze:
                    _currentScenario = s;
                    UpdateToolbarState();
                    _ = RunAnalysisAsync(s);
                    break;
                case ScenarioDetailDialog.DetailAction.Edit:
                    if (dlg.EditedResult != null)
                    {
                        ScenarioService.Update(dlg.EditedResult);
                        if (_currentScenario?.Id == dlg.EditedResult.Id)
                            _currentScenario = dlg.EditedResult;
                        UpdateToolbarState();
                    }
                    break;
                case ScenarioDetailDialog.DetailAction.Delete:
                    ScenarioService.Delete(s.Id);
                    if (_currentScenario?.Id == s.Id) _currentScenario = null;
                    UpdateToolbarState();
                    break;
            }
        }

        // ── 분석 실행 ──────────────────────────────────────────────────────
        private async Task RunAnalysisAsync(Scenario s)
        {
            // 하위 호환: 구 Scenario는 X0Ids/X1Ids가 비어 있고 X0Id/X1Id만 있음
            var x0Ids = (s.X0Ids != null && s.X0Ids.Count > 0) ? s.X0Ids
                        : (!string.IsNullOrEmpty(s.X0Id) ? new List<string> { s.X0Id }
                                                         : new List<string>());
            var x1Ids = (s.X1Ids != null && s.X1Ids.Count > 0) ? s.X1Ids
                        : (!string.IsNullOrEmpty(s.X1Id) ? new List<string> { s.X1Id }
                                                         : new List<string>());

            if (x0Ids.Count == 0 || x1Ids.Count == 0)
            { MessageBox.Show("시나리오에 x0/x1이 지정되어 있지 않습니다."); return; }

            // 현재 시나리오 갱신 + 툴바 상태
            _currentScenario = s;
            UpdateToolbarState();

            var selectedCodes = new HashSet<string>(s.FoodCodes);
            string cacheKey = FoodPresetService.ComputeCacheKey(
                x0Ids, x1Ids, s.SimTime, selectedCodes);

            PgBar.Visibility            = Visibility.Visible;
            PnlPlaceholder.Visibility   = Visibility.Collapsed;
            ResultDashboard.Visibility  = Visibility.Collapsed;
            PnlResultToolbar.Visibility = Visibility.Collapsed;
            TxtProgress.Text            = $"[{s.Name}] 데이터 로드 중...";

            try
            {
                // 캐시 적중
                if (FoodPresetService.HasCache(cacheKey))
                {
                    var cached = FoodPresetService.LoadCache(cacheKey);
                    if (cached != null)
                    {
                        TxtProgress.Text = $"[{s.Name}] 캐시된 결과 표시";
                        _lastResult = cached;
                        ShowResults(_lastResult, s);
                        ScenarioService.TouchAnalyzedAt(s.Id, DateTime.Now);
                        return;
                    }
                }

                // x0 다중 로드 + 병합 (파일별 메모리 캐시 활용)
                var x0All = new List<SurveyRecord>();
                foreach (var id in x0Ids)
                {
                    if (!_x0Cache.TryGetValue(id, out var data))
                    {
                        var path = AppDataService.GetDatasetCsvPath(id);
                        data = await Task.Run(() => CsvParserService.ParseX0(path));
                        _x0Cache[id] = data;
                    }
                    x0All.AddRange(data);
                }

                // x1 다중 로드 + 병합
                var x1All = new List<SurveyRecord>();
                foreach (var id in x1Ids)
                {
                    if (!_x1Cache.TryGetValue(id, out var data))
                    {
                        var path = AppDataService.GetDatasetCsvPath(id);
                        data = await Task.Run(() => CsvParserService.ParseX1(path));
                        _x1Cache[id] = data;
                    }
                    x1All.AddRange(data);
                }

                var progress = new Progress<string>(msg => TxtProgress.Text = $"[{s.Name}] {msg}");
                _lastResult = await Task.Run(() =>
                    UsualIntakeCalculator.Compute(x0All, x1All, selectedCodes, s.SimTime, progress));

                FoodPresetService.SaveCache(cacheKey, _lastResult);
                ScenarioService.TouchAnalyzedAt(s.Id, DateTime.Now);

                ShowResults(_lastResult, s);
            }
            catch (Exception ex)
            {
                MessageBox.Show("분석 오류: " + ex.Message, "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                TxtProgress.Text = "오류 발생: " + ex.Message;
                PnlPlaceholder.Visibility = Visibility.Visible;
            }
            finally
            {
                PgBar.Visibility = Visibility.Collapsed;
                UpdateToolbarState();
            }
        }

        // ── 결과 표시 (대시보드) ───────────────────────────────────────
        private void ShowResults(AnalysisResult result, Scenario? scenario = null)
        {
            var total = result.ResultTable.FirstOrDefault(r => r.Sex == "ALL" && r.AgeGDesc == "ALL")
                        ?? result.ResultTable.FirstOrDefault();

            // 메타 행
            TxtMetaName.Text = scenario?.Name ?? "(시나리오 미지정)";
            TxtMetaSub .Text = scenario != null
                ? $"식품 {scenario.FoodNames.Count}개 · 시뮬 {scenario.SimTime}회 · 등록자 {scenario.RegisteredBy}"
                : "";
            TxtN   .Text = total?.N.ToString("N0") ?? "-";
            TxtRhoP.Text = result.RhoP.ToString("F4");
            TxtRhoA.Text = result.RhoA.ToString("F4");
            TxtPapa.Text = result.Papa.ToString("F1") + "%";

            // 요약 통계 행
            TxtMean  .Text = total?.Average.ToString("F2") ?? "-";
            TxtMedian.Text = total?.Median.ToString("F2")  ?? "-";
            TxtSd    .Text = total?.Sd.ToString("F2")      ?? "-";
            TxtP95   .Text = total?.P95th.ToString("F2")   ?? "-";
            TxtP99   .Text = total?.P99th.ToString("F2")   ?? "-";

            // 테이블
            GridResult.ItemsSource = result.ResultTable;

            // 차트 (감마·식품 구성 차트는 제거됨)
            BuildDensityAllPlot(result);
            BuildQuantilePlot(result);
            ApplyNoInteractController();

            // 결과 메타 툴바
            var add = result.AdditionalResult;
            string addLabel = add != null ? $"  +{add.MethodUsed} 보완 적용" : "";
            string scenarioPrefix = scenario != null ? $"[{scenario.Name}]  " : "";
            TxtResultSummary.Text =
                scenarioPrefix +
                $"[NCI 기본]  N={result.PersonIntakes.Count:N0}" +
                $"  rhoP={result.RhoP:F4}  rhoA={result.RhoA:F4}" +
                $"  papa={result.Papa:F1}%  0섭취율={result.ZeroPrevalence:F1}%" +
                addLabel;

            // 방법 비교 카드 + 차트 (보완 분석이 있을 때만)
            if (add != null)
            {
                BuildComparisonTab(result, add);
                PnlComparison.Visibility      = Visibility.Visible;
                PnlComparisonChart.Visibility = Visibility.Visible;
                TxtCompChartTitle.Text        = $"NCI vs {add.MethodUsed} — 분포 비교";
            }
            else
            {
                PnlComparison.Visibility      = Visibility.Collapsed;
                PnlComparisonChart.Visibility = Visibility.Collapsed;
            }

            ResultDashboard .Visibility = Visibility.Visible;
            PnlResultToolbar.Visibility = Visibility.Visible;
            PnlPlaceholder  .Visibility = Visibility.Collapsed;

            string completeMsg = add != null
                ? $"분석 완료 (NCI + {add.MethodUsed}) — {result.PersonIntakes.Count:N0}명"
                : $"분석 완료 (NCI) — {result.PersonIntakes.Count:N0}명";
            TxtProgress.Text = scenarioPrefix + completeMsg;
        }

        // ── 방법 비교 ──────────────────────────────────────────────────────
        private void BuildComparisonTab(AnalysisResult nci, AnalysisResult add)
        {
            var nciTotal = nci.ResultTable.FirstOrDefault(r => r.Sex == "ALL" && r.AgeGDesc == "ALL");
            var addTotal = add.ResultTable.FirstOrDefault(r => r.Sex == "ALL" && r.AgeGDesc == "ALL");

            string addNote = add.MethodNote.Replace("\n", "  ");
            TxtComparisonSummary.Text =
                $"▶ NCI (기본)   평균 {nciTotal?.Average:F2}  SD {nciTotal?.Sd:F2}  " +
                $"중앙값 {nciTotal?.Median:F2}  P95 {nciTotal?.P95th:F2}  P99 {nciTotal?.P99th:F2}\n" +
                $"▶ {add.MethodUsed} (보완)   평균 {addTotal?.Average:F2}  SD {addTotal?.Sd:F2}  " +
                $"중앙값 {addTotal?.Median:F2}  P95 {addTotal?.P95th:F2}  P99 {addTotal?.P99th:F2}\n" +
                $"  {addNote}";

            var rows = new List<ComparisonRow>();
            if (nciTotal != null) rows.Add(new ComparisonRow("NCI", nciTotal));
            if (addTotal != null) rows.Add(new ComparisonRow(add.MethodUsed, addTotal));
            GridComparison.ItemsSource = rows;

            var model = CreateModel($"NCI vs {add.MethodUsed} — 일상섭취 분포 비교");
            var nciArr = nci.PersonIntakes.Select(p => p.Intake).ToArray();
            var addArr = add.PersonIntakes.Select(p => p.Intake).ToArray();
            var rawArr = nci.PersonIntakes.Select(p => p.RawIntk).ToArray();
            double xMax = ComputeTrimmedXMax(nciArr, addArr, rawArr);
            model.Series.Add(KdeSeries(
                nciArr,
                OxyColor.FromRgb(0x25, 0x63, 0xEB), "NCI", xMax));
            model.Series.Add(KdeSeries(
                addArr,
                OxyColor.FromRgb(0xB4, 0x53, 0x09), add.MethodUsed, xMax));
            model.Series.Add(KdeSeries(
                rawArr,
                OxyColor.FromRgb(0x8A, 0x8A, 0x85), "1일 실측치", xMax, dashed: true));

            AddAxes(model, "섭취량 (g/day)", "확률밀도");
            model.Legends.Add(new OxyPlot.Legends.Legend
            {
                LegendPosition   = OxyPlot.Legends.LegendPosition.TopRight,
                LegendBackground = OxyColor.FromArgb(230, 255, 255, 255),
                LegendTextColor  = OxyColor.FromRgb(0x1A, 0x1A, 0x1A)
            });
            PlotComparison.Model = model;
        }

        // ── 차트 빌더 ──────────────────────────────────────────────────────
        private void BuildDensityAllPlot(AnalysisResult result)
        {
            var model = CreateModel("일상섭취량 분포");

            // X 범위: 데이터의 99th percentile 기반으로 꼬리 자르기
            var allIntakes = result.PersonIntakes.Select(p => p.Intake)
                                                  .Where(v => v > 0).ToArray();
            double xMax = ComputeTrimmedXMax(allIntakes);
            if (xMax <= 0) xMax = 1;

            // 4개 시리즈: 전체·남자·여자 일상섭취 + 1일 실측치
            var allArr     = result.PersonIntakes.Select(p => p.Intake).ToArray();
            var maleArr    = result.PersonIntakes.Where(p => p.Sex == 1).Select(p => p.Intake).ToArray();
            var femaleArr  = result.PersonIntakes.Where(p => p.Sex == 2).Select(p => p.Intake).ToArray();
            var rawArr     = result.PersonIntakes.Select(p => p.RawIntk).ToArray();

            model.Series.Add(KdeSeries(allArr,
                OxyColor.FromRgb(0x25, 0x63, 0xEB), "전체 일상섭취", xMax));
            if (maleArr.Length > 0)
                model.Series.Add(KdeSeries(maleArr,
                    OxyColor.FromRgb(0x0F, 0x6E, 0x56), "남자 일상섭취", xMax));
            if (femaleArr.Length > 0)
                model.Series.Add(KdeSeries(femaleArr,
                    OxyColor.FromRgb(0xB9, 0x1C, 0x1C), "여자 일상섭취", xMax));
            model.Series.Add(KdeSeries(rawArr,
                OxyColor.FromRgb(0x8A, 0x8A, 0x85), "1일 실측치", xMax, dashed: true));

            AddAxes(model, "섭취량 (g/day)", "확률밀도");
            model.Legends.Add(new OxyPlot.Legends.Legend
            {
                LegendPosition   = OxyPlot.Legends.LegendPosition.TopRight,
                LegendBackground = OxyColor.FromArgb(230, 255, 255, 255),
                LegendTextColor  = OxyColor.FromRgb(0x1A, 0x1A, 0x1A)
            });
            PlotDensityAll.Model = model;
        }

        /// <summary>분포의 꼬리(이상치)를 잘라 차트 X축이 데이터 본체에 집중되도록 한다.</summary>
        private static double ComputeTrimmedXMax(double[] data)
        {
            return ComputeTrimmedXMax(new[] { data });
        }

        private static double ComputeTrimmedXMax(params double[][] series)
        {
            var sorted = series
                .Where(s => s != null)
                .SelectMany(s => s)
                .Where(v => double.IsFinite(v) && v > 0)
                .OrderBy(v => v)
                .ToArray();
            if (sorted.Length == 0) return 1;

            int idx = Math.Min(sorted.Length - 1, (int)Math.Ceiling(sorted.Length * 0.99) - 1);
            double p99 = sorted[Math.Max(0, idx)];
            double max = sorted[^1];

            // P99의 ~1.15배를 기본으로 하되, 꼬리가 짧은 자료는 실제 최대값까지 보여준다.
            // 극단값이 긴 자료는 본체가 눌리지 않도록 꼬리를 자동으로 잘라낸다.
            return Math.Max(1, Math.Min(p99 * 1.15, max * 1.2));
        }

        private void BuildQuantilePlot(AnalysisResult result)
        {
            var model = CreateModel("성별·연령군 분위수");
            var grouped = result.ResultTable
                .Where(r => r.AgeGDesc != "ALL" && r.Sex != "ALL")
                .ToList();

            // 성별×분위수별 8색 팔레트 — 같은 분위수라도 성별이 다르면 색이 다름
            var palette = new Dictionary<(string sex, string label), OxyColor>
            {
                {("남자", "평균"),  OxyColor.FromRgb(0x0C, 0x44, 0x7C) },
                {("남자", "P95"),   OxyColor.FromRgb(0x37, 0x8A, 0xDD) },
                {("남자", "P97.5"), OxyColor.FromRgb(0x05, 0xA1, 0xC9) },
                {("남자", "P99"),   OxyColor.FromRgb(0x53, 0x4A, 0xB7) },
                {("여자", "평균"),  OxyColor.FromRgb(0x99, 0x35, 0x56) },
                {("여자", "P95"),   OxyColor.FromRgb(0xD8, 0x5A, 0x30) },
                {("여자", "P97.5"), OxyColor.FromRgb(0xD4, 0x53, 0x7E) },
                {("여자", "P99"),   OxyColor.FromRgb(0xBF, 0x5A, 0xF2) },
            };
            OxyColor C(string sex, string label) =>
                palette.TryGetValue((sex, label), out var c) ? c
                    : OxyColor.FromRgb(0x5A, 0x5A, 0x55);

            var ageLabels = grouped
                .Select(r => r.AgeGDesc)
                .Distinct()
                .OrderBy(GetAgeSortKey)
                .ThenBy(x => x)
                .ToList();

            foreach (var sexGroup in grouped.GroupBy(r => r.Sex).OrderBy(g => g.Key))
            {
                var rows = sexGroup
                    .OrderBy(r => GetAgeSortKey(r.AgeGDesc))
                    .ThenBy(r => r.AgeGDesc)
                    .ToList();
                var avgLine = new LineSeries
                {
                    Title           = $"{sexGroup.Key} 평균",
                    Color           = C(sexGroup.Key, "평균"),
                    StrokeThickness = 1.8,
                    MarkerType      = MarkerType.Circle,
                    MarkerSize      = 5,
                    MarkerFill      = C(sexGroup.Key, "평균")
                };
                for (int i = 0; i < rows.Count; i++)
                    avgLine.Points.Add(new DataPoint(ageLabels.IndexOf(rows[i].AgeGDesc), rows[i].Average));
                model.Series.Add(avgLine);

                string[] pctLabels = { "P95", "P97.5", "P99" };
                Func<QuantileRow, double>[] getters = { r => r.P95th, r => r.P975th, r => r.P99th };
                MarkerType[] markers = { MarkerType.Diamond, MarkerType.Triangle, MarkerType.Square };
                for (int pi = 0; pi < 3; pi++)
                {
                    var color = C(sexGroup.Key, pctLabels[pi]);
                    var scatter = new ScatterSeries
                    {
                        Title        = $"{sexGroup.Key} {pctLabels[pi]}",
                        MarkerType   = markers[pi],
                        MarkerSize   = 5,
                        MarkerFill   = color,
                        MarkerStroke = color
                    };
                    for (int i = 0; i < rows.Count; i++)
                        scatter.Points.Add(new ScatterPoint(
                            ageLabels.IndexOf(rows[i].AgeGDesc), getters[pi](rows[i])));
                    model.Series.Add(scatter);
                }
            }

            var catAx = new CategoryAxis
            {
                Position       = AxisPosition.Bottom,
                Title          = "연령군",
                TextColor      = OxyColor.FromRgb(0x5A, 0x5A, 0x55),
                TitleColor     = OxyColor.FromRgb(0x5A, 0x5A, 0x55),
                TicklineColor  = OxyColor.FromRgb(0xD4, 0xD4, 0xD0),
                Angle          = -28,
                IsZoomEnabled  = false,
                IsPanEnabled   = false
            };
            foreach (var lbl in ageLabels) catAx.Labels.Add(lbl);
            model.Axes.Clear();
            model.Axes.Add(catAx);
            var valAx = new LinearAxis
            {
                Position           = AxisPosition.Left,
                Title              = "섭취량 (g/day)",
                TextColor          = OxyColor.FromRgb(0x5A, 0x5A, 0x55),
                TitleColor         = OxyColor.FromRgb(0x5A, 0x5A, 0x55),
                TicklineColor      = OxyColor.FromRgb(0xD4, 0xD4, 0xD0),
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColor.FromRgb(0xEC, 0xEC, 0xEA),
                IsZoomEnabled      = false,
                IsPanEnabled       = false
            };
            model.Axes.Add(valAx);
            // 범례는 차트 밖 x축 라벨 아래에 한 줄로 배치한다.
            model.Legends.Add(new OxyPlot.Legends.Legend
            {
                LegendPosition   = OxyPlot.Legends.LegendPosition.BottomCenter,
                LegendPlacement  = OxyPlot.Legends.LegendPlacement.Outside,
                LegendOrientation = OxyPlot.Legends.LegendOrientation.Horizontal,
                LegendBackground = OxyColor.FromArgb(230, 255, 255, 255),
                LegendTextColor  = OxyColor.FromRgb(0x1A, 0x1A, 0x1A),
                LegendMargin     = 6
            });
            model.PlotMargins = new OxyThickness(60, 12, 18, 60);
            PlotQuantile.Model = model;
        }

        private static int GetAgeSortKey(string ageLabel)
        {
            var digits = new string(ageLabel.TakeWhile(ch => char.IsDigit(ch)).ToArray());
            return int.TryParse(digits, out int age) ? age : int.MaxValue;
        }

        // ── 차트 헬퍼 ──────────────────────────────────────────────────────
        private static PlotModel CreateModel(string title) => new PlotModel
        {
            Title               = title,
            TitleColor          = OxyColor.FromRgb(0x1A, 0x1A, 0x1A),
            TitleFontSize       = 13,
            Background          = OxyColors.Transparent,
            PlotAreaBorderColor = OxyColor.FromRgb(0xD4, 0xD4, 0xD0),
            TextColor           = OxyColor.FromRgb(0x5A, 0x5A, 0x55)
        };

        // 모든 차트의 마우스 휠 줌·팬·드래그를 비활성화하는 빈 컨트롤러
        private static readonly PlotController _noInteractController = CreateNoInteractController();

        private static PlotController CreateNoInteractController()
        {
            var c = new PlotController();
            c.UnbindAll();
            return c;
        }

        private void ApplyNoInteractController()
        {
            PlotDensityAll .Controller = _noInteractController;
            PlotQuantile   .Controller = _noInteractController;
            PlotComparison .Controller = _noInteractController;
        }

        private static void AddAxes(PlotModel model, string xTitle, string yTitle)
        {
            model.Axes.Add(new LinearAxis
            {
                Position           = AxisPosition.Bottom, Title = xTitle,
                TextColor          = OxyColor.FromRgb(0x5A, 0x5A, 0x55),
                TitleColor         = OxyColor.FromRgb(0x5A, 0x5A, 0x55),
                TicklineColor      = OxyColor.FromRgb(0xD4, 0xD4, 0xD0),
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColor.FromRgb(0xEC, 0xEC, 0xEA),
                IsZoomEnabled      = false,
                IsPanEnabled       = false
            });
            model.Axes.Add(new LinearAxis
            {
                Position           = AxisPosition.Left, Title = yTitle,
                TextColor          = OxyColor.FromRgb(0x5A, 0x5A, 0x55),
                TitleColor         = OxyColor.FromRgb(0x5A, 0x5A, 0x55),
                TicklineColor      = OxyColor.FromRgb(0xD4, 0xD4, 0xD0),
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColor.FromRgb(0xEC, 0xEC, 0xEA),
                IsZoomEnabled      = false,
                IsPanEnabled       = false
            });
        }

        private static LineSeries KdeSeries(double[] data, OxyColor color,
            string title, double xMax, bool dashed = false)
        {
            var series = new LineSeries
            {
                Title           = title,
                Color           = color,
                StrokeThickness = dashed ? 1.5 : 2.0,
                LineStyle       = dashed ? LineStyle.Dash : LineStyle.Solid
            };
            if (data.Length == 0) return series;
            double h = BandwidthSilverman(data);
            if (h < 1e-9) h = 1;
            int pts = 200;
            double xMin = 0;
            double step = (xMax - xMin) / pts;
            for (int i = 0; i <= pts; i++)
            {
                double x = xMin + i * step;
                double y = KernelDensity(data, x, h);
                series.Points.Add(new DataPoint(x, y));
            }
            return series;
        }

        private static double BandwidthSilverman(double[] data)
        {
            double n  = data.Length;
            double sd = StdDev(data);
            if (sd < 1e-15) return 1.0;
            return 1.06 * sd * Math.Pow(n, -0.2);
        }

        private static double KernelDensity(double[] data, double x, double h)
        {
            double sum = 0;
            double inv = 1.0 / (h * Math.Sqrt(2 * Math.PI));
            foreach (var xi in data)
            {
                double u = (x - xi) / h;
                sum += Math.Exp(-0.5 * u * u);
            }
            return sum * inv / data.Length;
        }

        private static double StdDev(double[] v)
        {
            if (v.Length < 2) return 0;
            double mean = v.Average();
            return Math.Sqrt(v.Sum(x => (x - mean) * (x - mean)) / (v.Length - 1));
        }

        // ── 결과 내보내기 ────────────────────────────────────────────────
        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResult == null) { MessageBox.Show("먼저 분석을 실행하세요."); return; }

            var sfd = new SaveFileDialog
            {
                Title    = "결과 저장",
                Filter   = "CSV (*.csv)|*.csv",
                FileName = $"일상섭취량_결과_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };
            if (sfd.ShowDialog() != true) return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("성별,연령군,N,평균,SD,P1st,P5th,P25th,중앙값,P75th,P90th,P95th,P97.5th,P99th,최솟값,최댓값");
                foreach (var r in _lastResult.ResultTable)
                    sb.AppendLine($"{r.Sex},{r.AgeGDesc},{r.N}," +
                        $"{r.Average:F4},{r.Sd:F4},{r.P1st:F4},{r.P5th:F4},{r.P25th:F4}," +
                        $"{r.Median:F4},{r.P75th:F4},{r.P90th:F4},{r.P95th:F4}," +
                        $"{r.P975th:F4},{r.P99th:F4},{r.Min:F4},{r.Max:F4}");
                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("저장 완료: " + sfd.FileName, "완료",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("저장 실패: " + ex.Message, "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
