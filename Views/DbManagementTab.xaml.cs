using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UsualIntakeAnalyzer.Models;
using UsualIntakeAnalyzer.Services;

namespace UsualIntakeAnalyzer.Views
{
    /// <summary>
    /// DB 조회/관리 탭. 좌측 필터(대분류·기수·차수·종류·검색) + 우측 통합 테이블.
    /// 1일 조사(x1) / 2일 조사(x0) / 코드집을 한 그리드에서 관리한다.
    /// </summary>
    public partial class DbManagementTab : UserControl
    {
        public event Action? CodebookChanged;

        // 통합 행 모델 (x0/x1/코드집을 한 줄로 표시)
        public class DbItemRow
        {
            public string   Kind         { get; set; } = "";  // "X0" | "X1" | "CB"
            public string   TypeLabel    { get; set; } = "";
            public string   OriginalId   { get; set; } = "";  // DatasetInfo.Id 또는 CodebookInfo.Id
            public string   RoundId      { get; set; } = "";
            public bool     IsOrphan     { get; set; } = false; // 자료원 구조에서 차수가 삭제된 경우
            public string   ProjectName  { get; set; } = "";
            public string   SourceLabel  { get; set; } = "";
            public string   FileName     { get; set; } = "";
            public string   Description  { get; set; } = "";
            public int      RowCount     { get; set; }
            public DateTime RegisteredAt { get; set; }
            public string   RegisteredBy { get; set; } = "";
        }

        private List<DbItemRow> _all = new();
        private ObservableCollection<DbItemRow> _shown = new();

        // 식품군 행 모델
        public class GroupRow
        {
            public FoodGroup Source { get; set; } = new();
            public string   Name        => Source.Name;
            public string   Description => Source.Description;
            public int      FoodCount   => Source.FoodNames?.Count ?? 0;
            public int      CodeCount   => Source.FoodCodes?.Count ?? 0;
            public string   KindLabel   => Source.IsBuiltIn ? "기본" : "사용자";
            public DateTime CreatedAt   => Source.CreatedAt;
        }

        private List<GroupRow> _allGroups = new();
        private ObservableCollection<GroupRow> _shownGroups = new();

        public DbManagementTab()
        {
            InitializeComponent();
            GridGroups.ItemsSource = _shownGroups;
            Loaded += (_, _) => Reload();
        }

        // ── 자료/식품군 토글 ─────────────────────────────────────────────
        private void BtnViewData_Click(object sender, RoutedEventArgs e)
            => SwitchView(true);

        private void BtnViewGroup_Click(object sender, RoutedEventArgs e)
            => SwitchView(false);

        private void SwitchView(bool data)
        {
            PnlDataView.Visibility  = data ? Visibility.Visible : Visibility.Collapsed;
            PnlGroupView.Visibility = data ? Visibility.Collapsed : Visibility.Visible;
            // 토글 버튼 외형 (선택 = primary, 비선택 = secondary)
            BtnViewData .Style = data
                ? (Style)FindResource("WindowChromeButton") // primary 시각 효과 흉내
                : (Style)FindResource("SecondaryButton");
            BtnViewGroup.Style = data
                ? (Style)FindResource("SecondaryButton")
                : (Style)FindResource("WindowChromeButton");
            // 위 트릭이 어색하므로 단순 스타일로 — primary는 기본 Button, secondary는 secondary
            BtnViewData .Style = data
                ? null // 기본 (primary)
                : (Style)FindResource("SecondaryButton");
            BtnViewGroup.Style = data
                ? (Style)FindResource("SecondaryButton")
                : null; // 기본 (primary)

            if (!data) ReloadGroups();
        }

        // ════════════════════════════════════════════════════════════════
        // 전체 갱신
        // ════════════════════════════════════════════════════════════════
        public void Reload()
        {
            BuildAllRows();
            BuildProjectCombo();   // 캐스케이딩 시작점만 새로 그림
            ApplyFilter();
        }

        // ── 필터 콤보 빌드 ───────────────────────────────────────────────
        private void BuildProjectCombo()
        {
            string keepPid = (CboProject.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            CboProject.Items.Clear();
            CboProject.Items.Add(new ComboBoxItem { Content = "(전체)", Tag = "" });
            foreach (var p in SurveySourceService.LoadProjects().OrderBy(x => x.NameKo))
                CboProject.Items.Add(new ComboBoxItem
                    { Content = $"{p.NameKo}  ({p.ProjectCode})", Tag = p.Id });

            // 이전 선택 복원 (없으면 전체)
            CboProject.SelectedIndex = 0;
            if (!string.IsNullOrEmpty(keepPid))
            {
                foreach (ComboBoxItem it in CboProject.Items)
                    if ((it.Tag as string) == keepPid) { it.IsSelected = true; break; }
            }

            BuildPhaseCombo();
        }

        private void BuildPhaseCombo()
        {
            string keepPhid = (CboPhase.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            string pid = (CboProject.SelectedItem as ComboBoxItem)?.Tag as string ?? "";

            CboPhase.Items.Clear();
            CboPhase.Items.Add(new ComboBoxItem { Content = "(전체)", Tag = "" });
            var phases = SurveySourceService.LoadPhases();
            if (!string.IsNullOrEmpty(pid))
                phases = phases.Where(p => p.ProjectId == pid).ToList();
            foreach (var ph in phases.OrderBy(x => x.PhaseNo))
            {
                string yr = (ph.YearStart != null && ph.YearEnd != null)
                    ? (ph.YearStart == ph.YearEnd
                        ? $"  ({ph.YearStart})"
                        : $"  ({ph.YearStart}–{ph.YearEnd})")
                    : "";
                CboPhase.Items.Add(new ComboBoxItem
                    { Content = ph.PhaseLabel + yr, Tag = ph.Id });
            }

            CboPhase.SelectedIndex = 0;
            if (!string.IsNullOrEmpty(keepPhid))
            {
                foreach (ComboBoxItem it in CboPhase.Items)
                    if ((it.Tag as string) == keepPhid) { it.IsSelected = true; break; }
            }

            BuildRoundCombo();
        }

        private void BuildRoundCombo()
        {
            string keepRid = (CboRound.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            string phid = (CboPhase.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            string pid  = (CboProject.SelectedItem as ComboBoxItem)?.Tag as string ?? "";

            CboRound.Items.Clear();
            CboRound.Items.Add(new ComboBoxItem { Content = "(전체)", Tag = "" });

            var rounds = SurveySourceService.LoadRounds();
            if (!string.IsNullOrEmpty(phid))
                rounds = rounds.Where(r => r.PhaseId == phid).ToList();
            else if (!string.IsNullOrEmpty(pid))
            {
                var phaseIds = SurveySourceService.LoadPhases()
                    .Where(p => p.ProjectId == pid).Select(p => p.Id).ToHashSet();
                rounds = rounds.Where(r => phaseIds.Contains(r.PhaseId)).ToList();
            }

            foreach (var r in rounds.OrderBy(x => x.RoundNo))
                CboRound.Items.Add(new ComboBoxItem
                    { Content = r.DisplayLabel, Tag = r.Id });

            CboRound.SelectedIndex = 0;
            if (!string.IsNullOrEmpty(keepRid))
            {
                foreach (ComboBoxItem it in CboRound.Items)
                    if ((it.Tag as string) == keepRid) { it.IsSelected = true; break; }
            }
        }

        // ── 통합 행 빌드 (x0/x1 + 코드집) ────────────────────────────────
        private void BuildAllRows()
        {
            var rows = new List<DbItemRow>();

            foreach (var d in AppDataService.LoadDatasetMeta())
            {
                var (project, phase, round) = SurveySourceService.GetRoundContext(d.RoundId);
                // 차수 ID가 있는데 조회가 안 되면 고아(orphan) 데이터
                bool isOrphan = !string.IsNullOrEmpty(d.RoundId) && round == null;
                rows.Add(new DbItemRow
                {
                    Kind         = d.Type == DatasetType.X0 ? "X0"
                               : d.Type == DatasetType.PrecisionNutrition ? "PR" : "X1",
                    TypeLabel    = d.Type == DatasetType.X0
                                   ? "🍴 2일 조사"
                                 : d.Type == DatasetType.PrecisionNutrition
                                   ? "🔬 정밀영양 (미지원)"
                                 : "🍱 1일 조사",
                    OriginalId   = d.Id,
                    RoundId      = d.RoundId,
                    IsOrphan     = isOrphan,
                    ProjectName  = isOrphan ? "⚠ 삭제된 자료원" : (project?.NameKo ?? "(미지정)"),
                    SourceLabel  = isOrphan ? "⚠ 삭제된 차수" :
                                   ((round != null && phase != null)
                                   ? $"{phase.PhaseLabel} · {round.DisplayLabel}"
                                   : "(미지정)"),
                    FileName     = d.FileName,
                    Description  = d.Description,
                    RowCount     = d.RowCount,
                    RegisteredAt = d.RegisteredAt,
                    RegisteredBy = d.RegisteredBy
                });
            }

            // 코드집은 전역 1개
            var cb = AppDataService.LoadCodebookInfo();
            if (cb != null)
            {
                rows.Add(new DbItemRow
                {
                    Kind         = "CB",
                    TypeLabel    = "📖 코드집",
                    OriginalId   = cb.Id,
                    RoundId      = "",
                    ProjectName  = "—",
                    SourceLabel  = "(전역)",
                    FileName     = cb.FileName,
                    Description  = "전역 1개로 운영",
                    RowCount     = cb.RowCount,
                    RegisteredAt = cb.UploadedAt,
                    RegisteredBy = ""
                });
            }
            _all = rows;
        }

        // ── 필터 적용 ────────────────────────────────────────────────────
        private void ApplyFilter()
        {
            string pid  = (CboProject.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            string phid = (CboPhase  .SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            string rid  = (CboRound  .SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            string kw   = TxtSearch.Text?.Trim() ?? "";

            bool wantX0 = ChkX0.IsChecked == true;
            bool wantX1 = ChkX1.IsChecked == true;
            bool wantCb = ChkCb.IsChecked == true;

            var phasesByProject = SurveySourceService.LoadPhases()
                .Where(p => string.IsNullOrEmpty(pid) || p.ProjectId == pid)
                .Select(p => p.Id).ToHashSet();

            _shown.Clear();
            foreach (var r in _all.OrderByDescending(x => x.RegisteredAt))
            {
                if (r.Kind == "X0" && !wantX0) continue;
                if (r.Kind == "X1" && !wantX1) continue;
                if (r.Kind == "CB" && !wantCb) continue;

                // 차수 직접 지정
                if (!string.IsNullOrEmpty(rid))
                {
                    if (r.RoundId != rid) continue;
                }
                else
                {
                    // 차수가 미지정인 데이터는 사업/기수 필터가 있을 때 제외
                    if (string.IsNullOrEmpty(r.RoundId))
                    {
                        if (!string.IsNullOrEmpty(pid) || !string.IsNullOrEmpty(phid))
                            continue;
                    }
                    else
                    {
                        var round = SurveySourceService.LoadRounds()
                            .FirstOrDefault(x => x.Id == r.RoundId);

                        if (round == null)
                        {
                            // 고아 데이터(자료원 구조에서 해당 차수가 삭제된 경우)
                            // 필터가 없을 때만 표시, 특정 사업/기수 필터 시에는 숨김
                            if (!string.IsNullOrEmpty(pid) || !string.IsNullOrEmpty(phid))
                                continue;
                            // 필터 없으면 그대로 표시 (pass-through)
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(phid) && round.PhaseId != phid) continue;
                            if (!string.IsNullOrEmpty(pid) && !phasesByProject.Contains(round.PhaseId)) continue;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(kw))
                {
                    bool hit = r.FileName    .Contains(kw, StringComparison.OrdinalIgnoreCase)
                            || r.Description .Contains(kw, StringComparison.OrdinalIgnoreCase)
                            || r.SourceLabel .Contains(kw, StringComparison.OrdinalIgnoreCase)
                            || r.ProjectName .Contains(kw, StringComparison.OrdinalIgnoreCase)
                            || r.TypeLabel   .Contains(kw, StringComparison.OrdinalIgnoreCase);
                    if (!hit) continue;
                }

                _shown.Add(r);
            }
            GridDb.ItemsSource = _shown;

            int x0c       = _all.Count(x => x.Kind == "X0");
            int x1c       = _all.Count(x => x.Kind == "X1");
            int cbc       = _all.Count(x => x.Kind == "CB");
            int orphanCnt = _all.Count(x => x.IsOrphan);
            string orphanSuffix = orphanCnt > 0
                ? $"  |  ⚠ 삭제된 자료원 {orphanCnt:N0}건 (필터 초기화 시 표시)"
                : "";
            TxtSummary.Text =
                $"전체 {_all.Count:N0}건 · 표시 {_shown.Count:N0}건  " +
                $"|  1일(x1) {x1c:N0} · 2일(x0) {x0c:N0} · 코드집 {cbc:N0}" +
                orphanSuffix;
        }

        // ════════════════════════════════════════════════════════════════
        // 필터 이벤트 핸들러
        // ════════════════════════════════════════════════════════════════
        private void CboProject_SelectionChanged(object s, SelectionChangedEventArgs e)
        { if (IsLoaded) { BuildPhaseCombo(); ApplyFilter(); } }

        private void CboPhase_SelectionChanged(object s, SelectionChangedEventArgs e)
        { if (IsLoaded) { BuildRoundCombo(); ApplyFilter(); } }

        private void CboRound_SelectionChanged(object s, SelectionChangedEventArgs e)
        { if (IsLoaded) ApplyFilter(); }

        private void TypeChk_Changed(object s, RoutedEventArgs e)
        { if (IsLoaded) ApplyFilter(); }

        private void TxtSearch_TextChanged(object s, TextChangedEventArgs e)
        { if (IsLoaded) ApplyFilter(); }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            CboProject.SelectedIndex = 0;
            CboPhase  .SelectedIndex = 0;
            CboRound  .SelectedIndex = 0;
            ChkX0.IsChecked = ChkX1.IsChecked = ChkCb.IsChecked = true;
            TxtSearch.Text = "";
            ApplyFilter();
        }

        // ════════════════════════════════════════════════════════════════
        // 우측: 업로드 / 조회 / 삭제
        // ════════════════════════════════════════════════════════════════
        private void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            // 현재 필터를 기본값으로 사용
            string preProjectId = (CboProject.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            string prePhaseId   = (CboPhase  .SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            string preRoundId   = (CboRound  .SelectedItem as ComboBoxItem)?.Tag as string ?? "";

            // 종류 기본값: 체크된 것 중 첫 번째
            string preKind =
                ChkX1.IsChecked == true ? "X1" :
                ChkX0.IsChecked == true ? "X0" :
                ChkCb.IsChecked == true ? "CB" : "X1";

            var dlg = new UploadDbDialog(preKind, preProjectId, prePhaseId, preRoundId)
            {
                Owner = Window.GetWindow(this)
            };
            if (dlg.ShowDialog() == true)
            {
                Reload();
                if (dlg.UploadedKind == "CB") CodebookChanged?.Invoke();
            }
        }

        private void BtnView_Click(object sender, RoutedEventArgs e)
            => ViewSelected();

        private void GridDb_MouseDoubleClick(object sender,
            System.Windows.Input.MouseButtonEventArgs e)
            => ViewSelected();

        private void ViewSelected()
        {
            if (GridDb.SelectedItem is not DbItemRow row)
            { MessageBox.Show("조회할 행을 선택하세요."); return; }

            switch (row.Kind)
            {
                case "X0":
                case "X1":
                {
                    var d = AppDataService.LoadDatasetMeta()
                        .FirstOrDefault(x => x.Id == row.OriginalId);
                    if (d == null) { MessageBox.Show("데이터를 찾을 수 없습니다."); return; }
                    var path = AppDataService.GetDatasetCsvPath(d.Id);
                    if (!File.Exists(path)) { MessageBox.Show("CSV 파일을 찾을 수 없습니다."); return; }
                    new DataPreviewWindow(d, path)
                        { Owner = Window.GetWindow(this) }.ShowDialog();
                    break;
                }
                case "CB":
                {
                    var c = AppDataService.LoadCodebookInfo();
                    if (c == null) { MessageBox.Show("코드집을 찾을 수 없습니다."); return; }
                    new CodebookPreviewWindow(c, AppDataService.GetCodebookPath())
                        { Owner = Window.GetWindow(this) }.ShowDialog();
                    break;
                }
            }
        }

        // ── 자료원 재지정 ─────────────────────────────────────────────────
        private void BtnReassign_Click(object sender, RoutedEventArgs e)
        {
            if (GridDb.SelectedItem is not DbItemRow row || row.Kind == "CB")
            {
                MessageBox.Show("재지정할 데이터(x0 또는 x1) 행을 선택하세요.", "안내",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!row.IsOrphan)
            {
                if (MessageBox.Show(
                    $"이 데이터는 이미 자료원이 지정되어 있습니다.\n현재: {row.SourceLabel}\n\n그래도 자료원을 변경하시겠습니까?",
                    "자료원 재지정", MessageBoxButton.YesNo, MessageBoxImage.Question)
                    != MessageBoxResult.Yes) return;
            }

            var dlg = new ReassignRoundDialog(row.FileName, row.SourceLabel)
            { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true && !string.IsNullOrEmpty(dlg.SelectedRoundId))
            {
                var meta = AppDataService.LoadDatasetMeta();
                var d = meta.FirstOrDefault(x => x.Id == row.OriginalId);
                if (d != null)
                {
                    d.RoundId = dlg.SelectedRoundId;
                    AppDataService.SaveDatasetMeta(meta);
                    Reload();
                    MessageBox.Show("자료원이 재지정되었습니다.", "완료",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (GridDb.SelectedItem is not DbItemRow row)
            { MessageBox.Show("삭제할 행을 선택하세요."); return; }

            string label = $"[{row.TypeLabel}] {row.FileName}";
            if (MessageBox.Show($"{label}\n\n위 항목을 삭제하시겠습니까?",
                "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning)
                != MessageBoxResult.Yes) return;

            switch (row.Kind)
            {
                case "X0":
                case "X1":
                case "PR":
                    AppDataService.DeleteDataset(row.OriginalId);
                    break;
                case "CB":
                    AppDataService.DeleteCodebook();
                    CodebookChanged?.Invoke();
                    break;
            }
            Reload();
        }

        // ── 자료원 관리 (사업/기수/차수 CRUD) ───────────────────────────
        private void BtnSourceManage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SourceManageDialog { Owner = Window.GetWindow(this) };
            dlg.ShowDialog();
            Reload();
        }

        // ════════════════════════════════════════════════════════════════
        // 식품군 DB 관리
        // ════════════════════════════════════════════════════════════════
        private void ReloadGroups()
        {
            _allGroups = FoodGroupService.LoadAll()
                .OrderByDescending(g => g.IsBuiltIn)
                .ThenBy(g => g.Name)
                .Select(g => new GroupRow { Source = g })
                .ToList();
            ApplyGroupFilter();
        }

        private void ApplyGroupFilter()
        {
            string kw = TxtGroupSearch.Text?.Trim() ?? "";
            _shownGroups.Clear();
            foreach (var r in _allGroups)
            {
                if (string.IsNullOrEmpty(kw)
                    || r.Source.Name       .Contains(kw, StringComparison.OrdinalIgnoreCase)
                    || r.Source.Description.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    _shownGroups.Add(r);
            }
            int built = _allGroups.Count(g => g.Source.IsBuiltIn);
            int user  = _allGroups.Count - built;
            TxtGroupSummary.Text =
                $"전체 {_allGroups.Count:N0}건  ·  표시 {_shownGroups.Count:N0}건  " +
                $"|  기본 {built}  ·  사용자 {user}";
        }

        private void TxtGroupSearch_TextChanged(object s, TextChangedEventArgs e)
            => ApplyGroupFilter();

        private void BtnGroupAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!CodebookAndDataReady()) return;
            var dlg = new FoodGroupEditDialog { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                FoodGroupService.Add(dlg.Result);
                ReloadGroups();
            }
        }

        private void BtnGroupEdit_Click(object sender, RoutedEventArgs e)
            => OpenGroupEditor(GridGroups.SelectedItem as GroupRow);

        private void GridGroups_MouseDoubleClick(object sender,
            System.Windows.Input.MouseButtonEventArgs e)
            => OpenGroupEditor(GridGroups.SelectedItem as GroupRow);

        private void OpenGroupEditor(GroupRow? row)
        {
            if (row == null) { MessageBox.Show("편집할 식품군을 선택하세요."); return; }
            if (!CodebookAndDataReady()) return;
            var dlg = new FoodGroupEditDialog(row.Source) { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                FoodGroupService.Update(dlg.Result);
                ReloadGroups();
            }
        }

        private void BtnGroupDelete_Click(object sender, RoutedEventArgs e)
        {
            if (GridGroups.SelectedItem is not GroupRow row)
            { MessageBox.Show("삭제할 식품군을 선택하세요."); return; }
            if (MessageBox.Show($"'{row.Source.Name}' 식품군을 삭제하시겠습니까?",
                "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning)
                != MessageBoxResult.Yes) return;
            FoodGroupService.Delete(row.Source.Id);
            ReloadGroups();
        }

        private bool CodebookAndDataReady()
        {
            if (!AppDataService.CodebookExists())
            {
                MessageBox.Show("먼저 코드집을 등록하세요.", "안내",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            if (AppDataService.GetDatasetsByType(DatasetType.X0).Count == 0)
            {
                MessageBox.Show("2일 조사(x0) 데이터가 등록되어 있지 않습니다.", "안내",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            return true;
        }
    }
}