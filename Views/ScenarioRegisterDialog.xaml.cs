using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UsualIntakeAnalyzer.Models;
using UsualIntakeAnalyzer.Services;

namespace UsualIntakeAnalyzer.Views
{
    /// <summary>
    /// 시나리오 등록 팝업.
    /// 식품군명 + 식품(다중선택) + x1 + x0 + 시뮬횟수 + 등록자.
    /// </summary>
    public partial class ScenarioRegisterDialog : Window
    {
        // 결과 (호출자에게 반환)
        public Scenario? Result { get; private set; }

        // ── 식품 항목 ────────────────────────────────────────────────────
        public class FoodItem : INotifyPropertyChanged
        {
            private bool _isSelected;
            public string FoodName  { get; set; } = "";
            public string SubInfo   { get; set; } = "";
            public string CodeBadge { get; set; } = "";
            public HashSet<string> Codes { get; set; } = new();
            public bool IsSelected
            {
                get => _isSelected;
                set { _isSelected = value; PropertyChanged?.Invoke(this, new(nameof(IsSelected))); }
            }
            public event PropertyChangedEventHandler? PropertyChanged;
        }

        private List<FoodItem> _allFoods = new();
        private ObservableCollection<FoodItem> _shownFoods    = new();
        private ObservableCollection<FoodItem> _selectedFoods = new();

        // ── 데이터셋별 fcode 캐시 (x0/x1 필터링용) ─────────────────────
        private Dictionary<string, HashSet<string>> _x0Codes = new();
        private Dictionary<string, HashSet<string>> _x1Codes = new();

        public ScenarioRegisterDialog()
        {
            InitializeComponent();
            FoodList.ItemsSource         = _shownFoods;
            SelectedFoodList.ItemsSource = _selectedFoods;
            TxtRegBy.Text = Environment.UserName;
            Loaded += (_, _) => Init();
        }

        // 편집 모드용 — 기존 시나리오로 미리 채움
        public ScenarioRegisterDialog(Scenario existing) : this()
        {
            Loaded += (_, _) => PrefillFromExisting(existing);
        }

        // ── 초기화 ──────────────────────────────────────────────────────
        private void Init()
        {
            // 1) 식품 목록 빌드 (x0 union ∩ 코드집)
            BuildFoodList();
            ApplyFoodFilter();

            // 2) x0/x1 콤보 채우기
            BuildX0X1Combos();

            UpdateSaveEnabled();
        }

        private void BuildFoodList()
        {
            _allFoods.Clear();

            if (!AppDataService.CodebookExists()) return;

            // 모든 x0의 fcode union (캐시도 함께 채움)
            var fcodeUnion = new HashSet<string>(StringComparer.Ordinal);
            foreach (var d in AppDataService.GetDatasetsByType(DatasetType.X0))
            {
                var path = AppDataService.GetDatasetCsvPath(d.Id);
                var set  = File.Exists(path)
                           ? CsvParserService.ScanFCodes(path)
                           : new HashSet<string>();
                _x0Codes[d.Id] = set;
                fcodeUnion.UnionWith(set);
            }
            // x1 fcode도 미리 캐싱 (필터링용)
            foreach (var d in AppDataService.GetDatasetsByType(DatasetType.X1))
            {
                var path = AppDataService.GetDatasetCsvPath(d.Id);
                _x1Codes[d.Id] = File.Exists(path)
                                 ? CsvParserService.ScanFCodes(path)
                                 : new HashSet<string>();
            }

            try
            {
                var entries = ExcelParserService.ParseCodebook(AppDataService.GetCodebookPath());
                var filtered = entries
                    .Where(e => !string.IsNullOrWhiteSpace(e.FoodName))
                    .Where(e => fcodeUnion.Count == 0 || fcodeUnion.Contains(e.Code));
                var groups = filtered.GroupBy(e => e.FoodName).OrderBy(g => g.Key);
                foreach (var g in groups)
                {
                    var codes = g.Select(e => e.Code)
                                 .Where(c => !string.IsNullOrWhiteSpace(c))
                                 .ToHashSet();
                    var groups2 = g.Select(e => e.FoodGroup)
                                   .Where(fg => !string.IsNullOrWhiteSpace(fg))
                                   .Distinct().OrderBy(fg => fg).ToList();
                    var item = new FoodItem
                    {
                        FoodName  = g.Key,
                        SubInfo   = string.Join(" · ", groups2),
                        CodeBadge = $"{codes.Count}코드",
                        Codes     = codes
                    };
                    item.PropertyChanged += (_, _) => UpdateSelectedSummaryAndCombos();
                    _allFoods.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("식품 목록 빌드 오류: " + ex.Message, "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BuildX0X1Combos()
        {
            CboX0.Items.Clear();
            foreach (var d in AppDataService.GetDatasetsByType(DatasetType.X0))
                CboX0.Items.Add(BuildDatasetItem(d, _x0Codes));

            CboX1.Items.Clear();
            foreach (var d in AppDataService.GetDatasetsByType(DatasetType.X1))
                CboX1.Items.Add(BuildDatasetItem(d, _x1Codes));

            FilterCombosBySelectedCodes();
        }

        private static ComboBoxItem BuildDatasetItem(
            DatasetInfo d, Dictionary<string, HashSet<string>> codeMap)
        {
            var (project, phase, round) = SurveySourceService.GetRoundContext(d.RoundId);
            string ctx = (project != null && phase != null && round != null)
                ? $"{project.NameKo} {phase.PhaseLabel} {round.DisplayLabel}"
                : "(자료원 미지정)";
            int codeCount = codeMap.TryGetValue(d.Id, out var s) ? s.Count : 0;
            string codeNote = codeCount > 0 ? $"  · {codeCount:N0}개 코드" : "  · 집계 자료";
            return new ComboBoxItem
            {
                Content = $"{ctx} — {d.FileName}{codeNote}",
                Tag     = d.Id,
                ToolTip = $"{ctx}\n파일: {d.FileName}\n행 수: {d.RowCount:N0}\n등록: {d.RegisteredAt:yyyy-MM-dd HH:mm}"
            };
        }

        // 선택된 식품 코드를 포함하지 않는 데이터셋은 비활성화
        private void FilterCombosBySelectedCodes()
        {
            var selectedCodes = CurrentSelectedCodes();
            FilterCombo(CboX0, _x0Codes, selectedCodes);
            FilterCombo(CboX1, _x1Codes, selectedCodes);
        }

        private static void FilterCombo(ComboBox cbo,
            Dictionary<string, HashSet<string>> codeMap, HashSet<string> selectedCodes)
        {
            string? keep = (cbo.SelectedItem as ComboBoxItem)?.Tag as string;
            int firstEnabled = -1;
            for (int i = 0; i < cbo.Items.Count; i++)
            {
                if (cbo.Items[i] is not ComboBoxItem ci) continue;
                string id = (ci.Tag as string) ?? "";
                bool hasCodes = codeMap.TryGetValue(id, out var s) && s.Count > 0;
                bool match;
                if (selectedCodes.Count == 0)
                    match = true;                                    // 선택 전엔 전부 활성
                else if (!hasCodes)
                    match = true;                                    // 집계 자료 → 항상 허용
                else
                    match = s!.Overlaps(selectedCodes);
                ci.IsEnabled = match;
                if (match && firstEnabled < 0) firstEnabled = i;
            }
            // 현재 선택이 비활성으로 바뀌면 첫 번째 활성으로 이동
            if (cbo.SelectedItem is ComboBoxItem cur && cur.IsEnabled == false)
                cbo.SelectedIndex = firstEnabled;
        }

        private HashSet<string> CurrentSelectedCodes()
            => new HashSet<string>(_allFoods.Where(f => f.IsSelected).SelectMany(f => f.Codes));

        // ── 검색 / 전체 / 해제 ─────────────────────────────────────────
        private void TxtFoodSearch_TextChanged(object s, TextChangedEventArgs e)
            => ApplyFoodFilter();

        private void ApplyFoodFilter()
        {
            string kw = TxtFoodSearch.Text.Trim();
            _shownFoods.Clear();
            foreach (var f in _allFoods)
            {
                if (string.IsNullOrEmpty(kw) ||
                    f.FoodName.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    f.SubInfo .Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    f.Codes.Any(c => c.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                    _shownFoods.Add(f);
            }
        }

        private void BtnSelectAll_Click(object s, RoutedEventArgs e)
        {
            foreach (var f in _shownFoods) f.IsSelected = true;
            UpdateSelectedSummaryAndCombos();
        }

        private void BtnDeselectAll_Click(object s, RoutedEventArgs e)
        {
            foreach (var f in _shownFoods) f.IsSelected = false;
            UpdateSelectedSummaryAndCombos();
        }

        private void FoodItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is FoodItem f)
            {
                f.IsSelected = !f.IsSelected;
                UpdateSelectedSummaryAndCombos();
            }
        }

        private void UpdateSelectedSummaryAndCombos()
        {
            int sel  = _allFoods.Count(f => f.IsSelected);
            int code = _allFoods.Where(f => f.IsSelected).Sum(f => f.Codes.Count);
            TxtSelectedSummary.Text = sel == 0
                ? "선택 0개"
                : $"선택 {sel}개 식품  ·  {code:N0}개 코드";
            TxtSelectedHeader.Text  = $"선택된 식품 ({sel})";

            // 우측 패널: 선택된 식품 컬렉션 동기화
            _selectedFoods.Clear();
            foreach (var f in _allFoods.Where(x => x.IsSelected)
                                       .OrderBy(x => x.FoodName))
                _selectedFoods.Add(f);
            TxtSelectedEmpty.Visibility = sel == 0
                ? Visibility.Visible : Visibility.Collapsed;

            FilterCombosBySelectedCodes();
            UpdateSaveEnabled();
        }

        // 우측 패널의 ✕ 버튼 — 해당 식품 IsSelected = false
        private void BtnRemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn &&
                btn.Tag is FoodItem f)
            {
                f.IsSelected = false;
                UpdateSelectedSummaryAndCombos();
            }
        }

        // ── 공통 변경 핸들러 ────────────────────────────────────────────
        private void OnAnyChange(object sender, RoutedEventArgs e)
            => UpdateSaveEnabled();

        private void OnAnyChange(object sender, SelectionChangedEventArgs e)
            => UpdateSaveEnabled();

        private void OnAnyChange(object sender, TextChangedEventArgs e)
            => UpdateSaveEnabled();

        private void UpdateSaveEnabled()
        {
            bool ok = !string.IsNullOrWhiteSpace(TxtName.Text) &&
                      _allFoods.Any(f => f.IsSelected) &&
                      CboX1.SelectedItem is ComboBoxItem x1 && x1.IsEnabled &&
                      CboX0.SelectedItem is ComboBoxItem x0 && x0.IsEnabled &&
                      !string.IsNullOrWhiteSpace(TxtRegBy.Text);
            BtnSave.IsEnabled = ok;
        }

        // ── 편집 모드 채우기 ────────────────────────────────────────────
        private void PrefillFromExisting(Scenario s)
        {
            Title         = "시나리오 편집";
            TxtName.Text  = s.Name;
            TxtRegBy.Text = s.RegisteredBy;
            // 식품 선택 복원 — 코드 일치하면 IsSelected
            var codeSet = new HashSet<string>(s.FoodCodes);
            foreach (var f in _allFoods)
                f.IsSelected = f.Codes.Any(c => codeSet.Contains(c));
            UpdateSelectedSummaryAndCombos();
            // x0/x1 선택
            foreach (ComboBoxItem it in CboX0.Items)
                if ((it.Tag as string) == s.X0Id) { it.IsSelected = true; break; }
            foreach (ComboBoxItem it in CboX1.Items)
                if ((it.Tag as string) == s.X1Id) { it.IsSelected = true; break; }
            // 시뮬
            foreach (ComboBoxItem it in CboSimTime.Items)
                if ((string)it.Content == s.SimTime.ToString()) { it.IsSelected = true; break; }
            // Id 보존
            _editingId = s.Id;
            UpdateSaveEnabled();
        }

        private string? _editingId;

        // ── 저장 ────────────────────────────────────────────────────────
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var selectedFoods = _allFoods.Where(f => f.IsSelected).ToList();
            var codes = selectedFoods.SelectMany(f => f.Codes).Distinct().ToList();
            var names = selectedFoods.Select(f => f.FoodName).ToList();

            string x0Id = (CboX0.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            string x1Id = (CboX1.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            int simTime = int.Parse(((ComboBoxItem)CboSimTime.SelectedItem).Content.ToString()!);

            var s = new Scenario
            {
                Id           = _editingId ?? Guid.NewGuid().ToString(),
                Name         = TxtName.Text.Trim(),
                FoodNames    = names,
                FoodCodes    = codes,
                X0Id         = x0Id,
                X1Id         = x1Id,
                SimTime      = simTime,
                RegisteredBy = TxtRegBy.Text.Trim()
            };
            Result = s;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
