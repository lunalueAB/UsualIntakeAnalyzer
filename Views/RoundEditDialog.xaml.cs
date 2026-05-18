using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UsualIntakeAnalyzer.Models;
using UsualIntakeAnalyzer.Services;

namespace UsualIntakeAnalyzer.Views
{
    public partial class RoundEditDialog : Window
    {
        public SurveyRound? Result { get; private set; }
        private readonly SurveyRound? _src;
        private readonly string _phaseId;

        public RoundEditDialog(SurveyRound? src, string phaseId)
        {
            InitializeComponent();
            _src     = src;
            _phaseId = phaseId;

            // 부모 정보 표시
            var phase = SurveySourceService.LoadPhases()
                .FirstOrDefault(p => p.Id == phaseId);
            var project = phase == null ? null
                : SurveySourceService.LoadProjects().FirstOrDefault(p => p.Id == phase.ProjectId);
            TxtParent.Text = (project == null || phase == null)
                ? "(알 수 없음)"
                : $"{project.NameKo} · {phase.PhaseLabel}";

            if (src != null)
            {
                Title = "차수 편집";
                TxtNo.Text        = src.RoundNo.ToString();
                TxtYear.Text      = src.SurveyYear?.ToString() ?? "";
                TxtFieldEnd.Text  = src.FieldEnd;
                TxtNote.Text      = src.Notes;
                foreach (ComboBoxItem it in CboStatus.Items)
                    if ((string)it.Content == src.Status) { it.IsSelected = true; break; }
            }
            else
            {
                Title = "차수 추가";
                CboStatus.SelectedIndex = 1; // 진행중
                int next = SurveySourceService.LoadRounds()
                    .Where(r => r.PhaseId == phaseId)
                    .Select(r => r.RoundNo).DefaultIfEmpty(0).Max() + 1;
                TxtNo.Text = next.ToString();
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtNo.Text.Trim(), out int no) || no <= 0)
            {
                MessageBox.Show("차수 번호는 1 이상의 정수여야 합니다.", "확인",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            int? yr = int.TryParse(TxtYear.Text.Trim(), out int v1) ? v1 : (int?)null;
            string status = (CboStatus.SelectedItem is ComboBoxItem ci) ? (string)ci.Content : "";

            var r = _src ?? new SurveyRound { PhaseId = _phaseId };
            r.RoundNo    = no;
            r.SurveyYear = yr;
            r.FieldEnd   = TxtFieldEnd.Text.Trim();
            r.Status     = status;
            r.Notes      = TxtNote.Text.Trim();
            Result = r;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
