using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UsualIntakeAnalyzer.Services;

namespace UsualIntakeAnalyzer.Views
{
    /// <summary>
    /// 고아(orphan) 데이터셋의 자료원(차수)을 다시 지정하는 다이얼로그.
    /// </summary>
    public partial class ReassignRoundDialog : Window
    {
        public string? SelectedRoundId { get; private set; }

        public ReassignRoundDialog(string fileName, string currentLabel)
        {
            InitializeComponent();
            TxtFileName    .Text = $"파일: {fileName}";
            TxtCurrentLabel.Text = $"현재 자료원: {currentLabel}";
            Loaded += (_, _) => LoadRounds();
        }

        private void LoadRounds()
        {
            CboRound.Items.Clear();

            var projects = SurveySourceService.LoadProjects();
            var phases   = SurveySourceService.LoadPhases();
            var rounds   = SurveySourceService.LoadRounds()
                               .OrderBy(r => r.SurveyYear)
                               .ThenBy(r => r.RoundNo);

            foreach (var r in rounds)
            {
                var ph = phases.FirstOrDefault(p => p.Id == r.PhaseId);
                var pj = ph != null ? projects.FirstOrDefault(p => p.Id == ph.ProjectId) : null;

                string label = string.Format("{0} {1} · {2}",
                    pj?.ProjectCode  ?? "?",
                    ph?.PhaseLabel   ?? "?",
                    r.DisplayLabel);

                CboRound.Items.Add(new ComboBoxItem { Content = label, Tag = r.Id });
            }

            if (CboRound.Items.Count == 0)
            {
                CboRound.Items.Add(new ComboBoxItem
                    { Content = "(등록된 차수 없음)", IsEnabled = false });
            }
        }

        private void CboRound_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BtnOk.IsEnabled = CboRound.SelectedItem is ComboBoxItem item && item.IsEnabled;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            SelectedRoundId = (CboRound.SelectedItem as ComboBoxItem)?.Tag as string;
            if (!string.IsNullOrEmpty(SelectedRoundId))
                DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
