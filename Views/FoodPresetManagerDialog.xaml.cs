using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UsualIntakeAnalyzer.Models;
using UsualIntakeAnalyzer.Services;

namespace UsualIntakeAnalyzer.Views
{
    /// <summary>산출식품 관리 모달. 목록 출력·등록·수정·삭제·보기·사용 기능 제공.</summary>
    public partial class FoodPresetManagerDialog : Window
    {
        // ── 호출자에게 돌려줄 결과 ──────────────────────────────────────
        /// <summary>"사용" 버튼으로 선택한 프리셋 (없으면 null)</summary>
        public FoodPreset? UsePreset { get; private set; }

        // ── 호출자가 넘겨주는 컨텍스트 ──────────────────────────────────
        private readonly HashSet<string> _currentSelectedCodes;
        private readonly List<string>    _currentSelectedNames;
        private readonly string          _x0Id;
        private readonly string          _x1Id;
        private readonly int             _currentSimTime;

        // ── 표시용 행 모델 ───────────────────────────────────────────────
        public class PresetRow
        {
            public FoodPreset Source { get; set; } = new();
            public string Name        => Source.Name;
            public int    FoodCount   => Source.FoodNames?.Count ?? 0;
            public int    CodeCount   => Source.FoodCodes?.Count ?? 0;
            public DateTime CreatedAt => Source.CreatedAt;
            public DateTime UpdatedAt => Source.UpdatedAt;
            public string CacheStatus { get; set; } = "";
        }

        private ObservableCollection<PresetRow> _shown = new();
        private List<PresetRow> _all = new();

        public FoodPresetManagerDialog(
            HashSet<string> currentSelectedCodes,
            List<string>    currentSelectedNames,
            string          x0Id,
            string          x1Id,
            int             currentSimTime)
        {
            InitializeComponent();
            _currentSelectedCodes = currentSelectedCodes ?? new HashSet<string>();
            _currentSelectedNames = currentSelectedNames ?? new List<string>();
            _x0Id                 = x0Id ?? "";
            _x1Id                 = x1Id ?? "";
            _currentSimTime       = currentSimTime;

            // 컨텍스트 안내 — 분석 탭의 현재 x0/x1 선택을 표시
            var x0 = string.IsNullOrEmpty(_x0Id) ? null
                     : AppDataService.LoadDatasetMeta().FirstOrDefault(d => d.Id == _x0Id);
            var x1 = string.IsNullOrEmpty(_x1Id) ? null
                     : AppDataService.LoadDatasetMeta().FirstOrDefault(d => d.Id == _x1Id);
            string x0Label = x0?.FileName ?? "(미선택)";
            string x1Label = x1?.FileName ?? "(미선택)";
            TxtContext.Text =
                $"x0: {x0Label}  ·  x1: {x1Label}  ·  시뮬: {_currentSimTime}회";

            GridPresets.ItemsSource = _shown;
            Loaded += (_, _) => Reload();
        }

        // ── 목록 빌드 ────────────────────────────────────────────────────
        private void Reload()
        {
            var presets = FoodPresetService.LoadAll()
                          .OrderByDescending(p => p.UpdatedAt)
                          .ToList();
            _all = presets.Select(BuildRow).ToList();
            ApplyFilter();
        }

        private PresetRow BuildRow(FoodPreset p)
        {
            string status;
            if (string.IsNullOrEmpty(_x0Id) || string.IsNullOrEmpty(_x1Id))
                status = "x0/x1 미선택";
            else
            {
                var (hit, at) = FoodPresetService.ProbeCache(
                    _x0Id, _x1Id, _currentSimTime, p.FoodCodes);
                status = hit
                    ? $"✅ 캐시 ({at:yyyy-MM-dd HH:mm})"
                    : "—";
            }
            return new PresetRow { Source = p, CacheStatus = status };
        }

        private void ApplyFilter()
        {
            string kw = TxtSearch.Text.Trim();
            _shown.Clear();
            foreach (var r in _all)
            {
                if (string.IsNullOrEmpty(kw) ||
                    r.Source.Name       .Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    r.Source.Description.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    _shown.Add(r);
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
            => ApplyFilter();

        // ── 선택 시 상세 패널 ────────────────────────────────────────────
        private void GridPresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridPresets.SelectedItem is not PresetRow row)
            {
                TxtDetailName.Text  = "목록에서 산출식품을 선택하세요.";
                TxtDetailFoods.Text = "";
                return;
            }
            var p = row.Source;
            TxtDetailName.Text  = string.IsNullOrEmpty(p.Description)
                ? p.Name
                : $"{p.Name}  —  {p.Description}";
            TxtDetailFoods.Text = p.FoodNames.Count == 0
                ? $"({p.FoodCodes.Count}개 코드)"
                : string.Join(", ", p.FoodNames.Take(40))
                  + (p.FoodNames.Count > 40 ? $"  외 {p.FoodNames.Count - 40}개" : "");
        }

        private void GridPresets_MouseDoubleClick(object sender,
            System.Windows.Input.MouseButtonEventArgs e)
            => BtnUse_Click(sender, new RoutedEventArgs());

        // ── 새로 만들기 (현재 선택 기반) ────────────────────────────────
        private void BtnNewFromSelection_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSelectedCodes.Count == 0)
            {
                MessageBox.Show("먼저 분석 탭에서 식품을 선택하세요.", "안내",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var preset = new FoodPreset
            {
                FoodCodes = _currentSelectedCodes.ToList(),
                FoodNames = _currentSelectedNames.ToList()
            };
            var dlg = new FoodPresetEditDialog(preset, isNew: true) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                FoodPresetService.Add(dlg.Result);
                Reload();
            }
        }

        private void BtnNewBlank_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new FoodPresetEditDialog(new FoodPreset(), isNew: true) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                FoodPresetService.Add(dlg.Result);
                Reload();
            }
        }

        // ── 보기 (요약 다이얼로그) ───────────────────────────────────────
        private void BtnView_Click(object sender, RoutedEventArgs e)
        {
            if (GridPresets.SelectedItem is not PresetRow row)
            { MessageBox.Show("목록에서 항목을 선택하세요."); return; }

            var p = row.Source;
            string foods = p.FoodNames.Count == 0
                ? "(이름 정보 없음)"
                : string.Join("\n  • ", p.FoodNames);
            string codes = p.FoodCodes.Count == 0
                ? "(없음)"
                : string.Join(", ", p.FoodCodes.Take(40))
                  + (p.FoodCodes.Count > 40 ? $"  외 {p.FoodCodes.Count - 40}개" : "");

            string msg =
                $"명칭: {p.Name}\n" +
                $"설명: {(string.IsNullOrEmpty(p.Description) ? "(없음)" : p.Description)}\n" +
                $"등록: {p.CreatedAt:yyyy-MM-dd HH:mm}    수정: {p.UpdatedAt:yyyy-MM-dd HH:mm}\n" +
                (p.LastAnalyzedAt.HasValue ? $"마지막 분석: {p.LastAnalyzedAt:yyyy-MM-dd HH:mm}\n" : "") +
                $"\n식품 ({p.FoodNames.Count}개):\n  • {foods}\n" +
                $"\n1차코드 ({p.FoodCodes.Count}개):\n  {codes}";
            MessageBox.Show(msg, "산출식품 보기",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── 편집 ─────────────────────────────────────────────────────────
        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (GridPresets.SelectedItem is not PresetRow row)
            { MessageBox.Show("목록에서 항목을 선택하세요."); return; }

            var dlg = new FoodPresetEditDialog(row.Source, isNew: false) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                FoodPresetService.Update(dlg.Result);
                Reload();
            }
        }

        // ── 삭제 ─────────────────────────────────────────────────────────
        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (GridPresets.SelectedItem is not PresetRow row)
            { MessageBox.Show("목록에서 항목을 선택하세요."); return; }
            if (MessageBox.Show($"'{row.Source.Name}'을(를) 삭제하시겠습니까?",
                "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning)
                != MessageBoxResult.Yes) return;

            FoodPresetService.Delete(row.Source.Id);
            Reload();
        }

        // ── 사용 (분석 탭에 적용 + 자동 분석) ────────────────────────────
        private void BtnUse_Click(object sender, RoutedEventArgs e)
        {
            if (GridPresets.SelectedItem is not PresetRow row)
            { MessageBox.Show("사용할 산출식품을 선택하세요."); return; }
            UsePreset = row.Source;
            DialogResult = true;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
