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
    /// <summary>
    /// 분석 이력 모달 — 등록된 모든 시나리오를 표시.
    /// 결과:
    ///  - <see cref="OpenScenario"/>: "열기" 버튼/더블클릭으로 선택한 시나리오 (호출자가 분석 실행)
    ///  - <see cref="DetailRequested"/>: 호출자가 상세 다이얼로그를 띄워야 하는 경우
    /// </summary>
    public partial class ScenarioHistoryDialog : Window
    {
        public Scenario? OpenScenario     { get; private set; }
        public Scenario? DetailRequested  { get; private set; }

        public class HistoryRow
        {
            public Scenario Source { get; set; } = new();
            public string   Name              => Source.Name;
            public int      FoodCount         => Source.FoodNames?.Count ?? 0;
            public string   RegisteredBy      => Source.RegisteredBy;
            public DateTime RegisteredAt      => Source.RegisteredAt;
            public string   LastAnalyzedDisplay
                => Source.LastAnalyzedAt.HasValue
                    ? Source.LastAnalyzedAt.Value.ToString("yyyy-MM-dd HH:mm")
                    : "—";
            public string CacheBadge { get; set; } = "";
        }

        private List<HistoryRow> _all = new();
        private ObservableCollection<HistoryRow> _shown = new();

        public ScenarioHistoryDialog()
        {
            InitializeComponent();
            GridScenarios.ItemsSource = _shown;
            Loaded += (_, _) => Reload();
        }

        private void Reload()
        {
            _all = ScenarioService.LoadAll()
                .OrderByDescending(s => s.RegisteredAt)
                .Select(s =>
                {
                    // 다중 선택 시나리오 지원: X0Ids/X1Ids 우선, 없으면 구 단일 Id로 폴백
                    var x0Ids = (s.X0Ids != null && s.X0Ids.Count > 0) ? s.X0Ids
                                : new System.Collections.Generic.List<string> { s.X0Id };
                    var x1Ids = (s.X1Ids != null && s.X1Ids.Count > 0) ? s.X1Ids
                                : new System.Collections.Generic.List<string> { s.X1Id };
                    var (hit, _) = FoodPresetService.ProbeCache(
                        x0Ids, x1Ids, s.SimTime, s.FoodCodes);
                    return new HistoryRow
                    {
                        Source     = s,
                        CacheBadge = hit ? "✅" : "—"
                    };
                })
                .ToList();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string kw = TxtSearch.Text.Trim();
            _shown.Clear();
            foreach (var r in _all)
            {
                if (string.IsNullOrEmpty(kw)
                    || r.Source.Name        .Contains(kw, StringComparison.OrdinalIgnoreCase)
                    || r.Source.RegisteredBy.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    _shown.Add(r);
            }
            TxtCount.Text = $"{_shown.Count:N0} / {_all.Count:N0} 건";
            UpdateActionEnabled();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
            => ApplyFilter();

        private HistoryRow? Selected => GridScenarios.SelectedItem as HistoryRow;

        private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => UpdateActionEnabled();

        private void Grid_MouseDoubleClick(object sender,
            System.Windows.Input.MouseButtonEventArgs e)
            => BtnOpen_Click(sender, new RoutedEventArgs());

        private void UpdateActionEnabled()
        {
            bool ok = Selected != null;
            BtnDetail.IsEnabled = ok;
            BtnDelete.IsEnabled = ok;
            BtnOpen  .IsEnabled = ok;
        }

        private void BtnDetail_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            DetailRequested = Selected.Source;
            OpenScenario    = null;
            DialogResult    = true;
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            if (MessageBox.Show($"'{Selected.Source.Name}' 시나리오를 삭제하시겠습니까?",
                "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning)
                != MessageBoxResult.Yes) return;
            ScenarioService.Delete(Selected.Source.Id);
            Reload();
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            OpenScenario    = Selected.Source;
            DetailRequested = null;
            DialogResult    = true;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
