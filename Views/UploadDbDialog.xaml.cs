using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using UsualIntakeAnalyzer.Models;
using UsualIntakeAnalyzer.Services;

namespace UsualIntakeAnalyzer.Views
{
    /// <summary>
    /// 통합 업로드 다이얼로그 — 종류(1일/2일/코드집) + 대분류·기수·차수 + 파일 선택을 한 화면에서 처리.
    /// </summary>
    public partial class UploadDbDialog : Window
    {
        public string UploadedKind { get; private set; } = "";  // 결과 알림용

        private string _filePath = "";

        public UploadDbDialog(string preKind, string preProjectId,
                              string prePhaseId, string preRoundId)
        {
            InitializeComponent();

            BuildProjectCombo(preProjectId);
            // 기수/차수는 사업 선택 시 자동 갱신되며, 사전값을 적용
            SelectComboById(CboProject, preProjectId);
            BuildPhaseCombo(prePhaseId);
            SelectComboById(CboPhase, prePhaseId);
            BuildRoundCombo(preRoundId);
            SelectComboById(CboRound, preRoundId);

            // 종류 사전값
            foreach (ComboBoxItem it in CboKind.Items)
                if ((it.Tag as string) == preKind) { it.IsSelected = true; break; }
            if (CboKind.SelectedIndex < 0) CboKind.SelectedIndex = 0;

            TxtRegBy.Text = Environment.UserName;

            UpdateFieldsForKind();
        }

        private static void SelectComboById(ComboBox cbo, string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            foreach (ComboBoxItem it in cbo.Items)
                if ((it.Tag as string) == id) { it.IsSelected = true; return; }
        }

        // ── 사업/기수/차수 콤보 ────────────────────────────────────────
        private void BuildProjectCombo(string keepId = "")
        {
            CboProject.Items.Clear();
            CboProject.Items.Add(new ComboBoxItem { Content = "(선택)", Tag = "" });
            foreach (var p in SurveySourceService.LoadProjects().OrderBy(x => x.NameKo))
                CboProject.Items.Add(new ComboBoxItem
                    { Content = $"{p.NameKo}  ({p.ProjectCode})", Tag = p.Id });
            CboProject.SelectedIndex = 0;
        }

        private void BuildPhaseCombo(string keepId = "")
        {
            CboPhase.Items.Clear();
            CboPhase.Items.Add(new ComboBoxItem { Content = "(선택)", Tag = "" });
            string pid = (CboProject.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            if (string.IsNullOrEmpty(pid))
            {
                CboPhase.SelectedIndex = 0;
                BuildRoundCombo();
                return;
            }
            var phases = SurveySourceService.LoadPhases()
                .Where(p => p.ProjectId == pid)
                .OrderBy(p => p.PhaseNo);
            foreach (var ph in phases)
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
            BuildRoundCombo();
        }

        private void BuildRoundCombo(string keepId = "")
        {
            CboRound.Items.Clear();
            CboRound.Items.Add(new ComboBoxItem { Content = "(선택)", Tag = "" });
            string phid = (CboPhase.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            if (string.IsNullOrEmpty(phid))
            {
                CboRound.SelectedIndex = 0;
                UpdateSaveEnabled();
                return;
            }
            var rounds = SurveySourceService.LoadRounds()
                .Where(r => r.PhaseId == phid)
                .OrderBy(r => r.RoundNo);
            foreach (var r in rounds)
                CboRound.Items.Add(new ComboBoxItem
                    { Content = r.DisplayLabel, Tag = r.Id });
            CboRound.SelectedIndex = 0;
            UpdateSaveEnabled();
        }

        // ── 종류에 따라 라벨/필드 가시성 조정 ─────────────────────────
        private void UpdateFieldsForKind()
        {
            string kind = (CboKind.SelectedItem as ComboBoxItem)?.Tag as string ?? "X1";
            bool isCsv = kind == "X0" || kind == "X1";
            bool isCb  = kind == "CB";
            bool isPr  = kind == "PR";

            // 자료원 캐스케이드 (코드집은 전역이라 숨김, 정밀영양은 차수 필요)
            LblProject.Visibility = (isCsv || isPr) ? Visibility.Visible : Visibility.Collapsed;
            CboProject.Visibility = (isCsv || isPr) ? Visibility.Visible : Visibility.Collapsed;
            LblPhase  .Visibility = (isCsv || isPr) ? Visibility.Visible : Visibility.Collapsed;
            CboPhase  .Visibility = (isCsv || isPr) ? Visibility.Visible : Visibility.Collapsed;
            LblRound  .Visibility = (isCsv || isPr) ? Visibility.Visible : Visibility.Collapsed;
            CboRound  .Visibility = (isCsv || isPr) ? Visibility.Visible : Visibility.Collapsed;
            PnlCbInfo .Visibility = isCb  ? Visibility.Visible : Visibility.Collapsed;

            // 설명/등록자 (CSV + 정밀영양)
            LblDesc .Visibility = (isCsv || isPr) ? Visibility.Visible : Visibility.Collapsed;
            TxtDesc .Visibility = (isCsv || isPr) ? Visibility.Visible : Visibility.Collapsed;
            LblBy   .Visibility = (isCsv || isPr) ? Visibility.Visible : Visibility.Collapsed;
            TxtRegBy.Visibility = (isCsv || isPr) ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── 이벤트 핸들러 ────────────────────────────────────────────
        private void CboKind_SelectionChanged(object s, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            UpdateFieldsForKind();
            // 파일 선택은 종류가 바뀌면 무효화
            _filePath = "";
            TxtFilePath.Text = "선택된 파일이 없습니다.";
            UpdateSaveEnabled();
        }

        private void CboProject_SelectionChanged(object s, SelectionChangedEventArgs e)
        { if (IsLoaded) BuildPhaseCombo(); }

        private void CboPhase_SelectionChanged(object s, SelectionChangedEventArgs e)
        { if (IsLoaded) BuildRoundCombo(); }

        private void BtnPickFile_Click(object sender, RoutedEventArgs e)
        {
            string kind = (CboKind.SelectedItem as ComboBoxItem)?.Tag as string ?? "X1";
            string filter = kind == "CB"
                ? "Excel 파일 (*.xlsx)|*.xlsx"
                : "Excel/CSV 파일 (*.xlsx;*.csv)|*.xlsx;*.csv|모든 파일 (*.*)|*.*";

            var ofd = new OpenFileDialog
            {
                Title  = kind == "CB" ? "코드집 xlsx 파일 선택"
                       : kind == "PR" ? "정밀영양 데이터 xlsx 파일 선택"
                       : "CSV 파일 선택",
                Filter = filter
            };
            if (ofd.ShowDialog() != true) return;

            _filePath = ofd.FileName;
            TxtFilePath.Text = _filePath;
            TxtFilePath.Foreground =
                (System.Windows.Media.Brush)FindResource("TextBrush");

            // CSV/PR이고 설명 비어있으면 파일명으로 채움
            if (kind != "CB" && string.IsNullOrWhiteSpace(TxtDesc.Text))
                TxtDesc.Text = Path.GetFileName(_filePath);

            UpdateSaveEnabled();
        }

        private void UpdateSaveEnabled()
        {
            string kind = (CboKind.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            bool fileOk = !string.IsNullOrEmpty(_filePath) && File.Exists(_filePath);

            if (kind == "CB")
            {
                // 코드집은 전역 — 차수 불필요
                BtnSave.IsEnabled = fileOk;
            }
            else
            {
                string rid = (CboRound.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
                BtnSave.IsEnabled = fileOk && !string.IsNullOrEmpty(rid);
            }
        }

        // ── 등록 ────────────────────────────────────────────────────
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string kind = (CboKind .SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            string rid  = (CboRound.SelectedItem as ComboBoxItem)?.Tag as string ?? "";

            if (string.IsNullOrEmpty(kind) ||
                string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
            {
                MessageBox.Show("종류·파일을 선택하세요.", "확인",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (kind != "CB" && string.IsNullOrEmpty(rid))
            {
                MessageBox.Show("자료원(차수)을 선택하세요.", "확인",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (kind == "PR")
                {
                    // 정밀영양 데이터 — xlsx 파일 그대로 보관, 분석엔 사용 불가
                    var info = new DatasetInfo
                    {
                        Type         = DatasetType.PrecisionNutrition,
                        RoundId      = rid,
                        RegisteredAt = DateTime.Now,
                        Description  = string.IsNullOrWhiteSpace(TxtDesc.Text)
                                       ? Path.GetFileName(_filePath)
                                       : TxtDesc.Text.Trim(),
                        RegisteredBy = string.IsNullOrWhiteSpace(TxtRegBy.Text)
                                       ? Environment.UserName
                                       : TxtRegBy.Text.Trim(),
                        FileName     = Path.GetFileName(_filePath),
                        RowCount     = -1   // xlsx 행 수 미집계 (파서 미구현)
                    };
                    // xlsx를 data 디렉터리에 id.xlsx 로 복사
                    AppDataService.SaveDatasetFile(_filePath, info.Id,
                                                   Path.GetExtension(_filePath));
                    var meta = AppDataService.LoadDatasetMeta();
                    meta.Add(info);
                    AppDataService.SaveDatasetMeta(meta);
                    MessageBox.Show($"정밀영양 데이터 등록 완료\n{info.FileName}\n\n※ 현재 분석에 사용할 수 없습니다.",
                        "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                    UploadedKind = kind;
                    DialogResult = true;
                    return;
                }
                else if (kind == "CB")
                {
                    // 코드집 — 전역 단일. 기존 있으면 자동 교체.
                    if (AppDataService.CodebookExists())
                    {
                        if (MessageBox.Show(
                                "기존 코드집이 등록되어 있습니다. 교체하시겠습니까?",
                                "확인", MessageBoxButton.YesNo, MessageBoxImage.Question)
                            != MessageBoxResult.Yes) return;
                    }
                    var entries = ExcelParserService.ParseCodebook(_filePath);
                    AppDataService.SaveCodebook(_filePath, entries.Count);
                    MessageBox.Show($"코드집 등록 완료: {entries.Count:N0}건",
                        "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // CSV (x0/x1)
                    int rows = File.ReadAllLines(_filePath).Length - 1;
                    var info = new DatasetInfo
                    {
                        Type         = kind == "X0" ? DatasetType.X0 : DatasetType.X1,
                        RoundId      = rid,
                        RegisteredAt = DateTime.Now,
                        Description  = string.IsNullOrWhiteSpace(TxtDesc.Text)
                                       ? Path.GetFileName(_filePath)
                                       : TxtDesc.Text.Trim(),
                        RegisteredBy = string.IsNullOrWhiteSpace(TxtRegBy.Text)
                                       ? Environment.UserName
                                       : TxtRegBy.Text.Trim(),
                        FileName     = Path.GetFileName(_filePath),
                        RowCount     = rows
                    };
                    AppDataService.SaveDatasetCsv(_filePath, info.Id);
                    var meta = AppDataService.LoadDatasetMeta();
                    meta.Add(info);
                    AppDataService.SaveDatasetMeta(meta);
                    MessageBox.Show($"등록 완료: {info.FileName}\n행 수: {rows:N0}",
                        "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                UploadedKind = kind;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("등록 실패: " + ex.Message, "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
