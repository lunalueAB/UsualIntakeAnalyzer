using System.Linq;
using System.Windows;
using UsualIntakeAnalyzer.Models;
using UsualIntakeAnalyzer.Services;

namespace UsualIntakeAnalyzer.Views
{
    /// <summary>
    /// 시나리오 상세 보기 + 액션. 결과 DialogResult로 호출자에게 의도를 알린다.
    /// - <c>Action == Analyze</c>: 분석 실행 요청
    /// - <c>Action == Edit</c>:    편집된 시나리오를 <see cref="EditedResult"/>로 반환
    /// - <c>Action == Delete</c>:  삭제 요청
    /// - <c>Action == None</c>:    그냥 닫음
    /// </summary>
    public partial class ScenarioDetailDialog : Window
    {
        public enum DetailAction { None, Analyze, Edit, Delete }

        public DetailAction Action      { get; private set; } = DetailAction.None;
        public Scenario?    EditedResult { get; private set; }

        private readonly Scenario _src;

        public ScenarioDetailDialog(Scenario s)
        {
            InitializeComponent();
            _src = s;
            Render();
        }

        private void Render()
        {
            TxtName.Text = _src.Name;
            string lastAnalyzed = _src.LastAnalyzedAt.HasValue
                ? $"마지막 분석 {_src.LastAnalyzedAt:yyyy-MM-dd HH:mm}"
                : "(아직 분석 이력 없음)";
            TxtMeta.Text =
                $"등록 {_src.RegisteredAt:yyyy-MM-dd HH:mm}  ·  " +
                $"식품 {_src.FoodNames.Count}개  ·  코드 {_src.FoodCodes.Count}개  ·  {lastAnalyzed}";

            TxtX1 .Text = LookupDatasetLabels(_src.X1Ids, _src.X1Id);
            TxtX0 .Text = LookupDatasetLabels(_src.X0Ids, _src.X0Id);
            TxtSim.Text = $"{_src.SimTime}회";
            TxtBy .Text = string.IsNullOrWhiteSpace(_src.RegisteredBy) ? "(없음)" : _src.RegisteredBy;

            TxtFoods.Text = _src.FoodNames.Count == 0
                ? "(없음)"
                : "• " + string.Join("\n• ", _src.FoodNames);
            TxtCodes.Text = _src.FoodCodes.Count == 0
                ? "(없음)"
                : string.Join(", ", _src.FoodCodes);
        }

        private static string? LookupDatasetLabel(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var d = AppDataService.LoadDatasetMeta().FirstOrDefault(x => x.Id == id);
            if (d == null) return "(찾을 수 없음)";
            var (project, phase, round) = SurveySourceService.GetRoundContext(d.RoundId);
            string ctx = (project != null && phase != null && round != null)
                ? $"{project.NameKo} {phase.PhaseLabel} {round.DisplayLabel}"
                : "(자료원 미지정)";
            return $"{ctx} — {d.FileName}";
        }

        /// <summary>다중 Id 목록을 표시용 문자열로 변환. 구 단일 Id는 fallbackId로 처리.</summary>
        private static string LookupDatasetLabels(List<string>? ids, string fallbackId)
        {
            var effective = (ids != null && ids.Count > 0) ? ids
                            : (!string.IsNullOrEmpty(fallbackId)
                               ? new System.Collections.Generic.List<string> { fallbackId }
                               : new System.Collections.Generic.List<string>());
            if (effective.Count == 0) return "(미지정)";
            return string.Join("\n",
                effective.Select(id => LookupDatasetLabel(id) ?? "(찾을 수 없음)"));
        }

        private void BtnAnalyze_Click(object sender, RoutedEventArgs e)
        {
            Action = DetailAction.Analyze;
            DialogResult = true;
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ScenarioRegisterDialog(_src) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                EditedResult = dlg.Result;
                Action = DetailAction.Edit;
                DialogResult = true;
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show($"'{_src.Name}' 시나리오를 삭제하시겠습니까?",
                "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning)
                != MessageBoxResult.Yes) return;
            Action = DetailAction.Delete;
            DialogResult = true;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Action = DetailAction.None;
            DialogResult = false;
        }
    }
}
