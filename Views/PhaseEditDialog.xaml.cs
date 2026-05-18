using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UsualIntakeAnalyzer.Models;
using UsualIntakeAnalyzer.Services;

namespace UsualIntakeAnalyzer.Views
{
    public partial class PhaseEditDialog : Window
    {
        public SurveyPhase? Result { get; private set; }
        private readonly SurveyPhase? _src;
        private readonly string _projectId;

        public PhaseEditDialog(SurveyPhase? src, string projectId)
        {
            InitializeComponent();
            _src       = src;
            _projectId = projectId;

            var pj = SurveySourceService.LoadProjects()
                .FirstOrDefault(p => p.Id == projectId);
            TxtParent.Text = pj == null
                ? "(알 수 없음)"
                : $"{pj.NameKo}  ({pj.ProjectCode})";

            if (src != null)
            {
                Title = "기수 편집";
                TxtNo.Text        = src.PhaseNo.ToString();
                TxtLabel.Text     = src.PhaseLabel;
                TxtYearStart.Text = src.YearStart?.ToString() ?? "";
                TxtYearEnd.Text   = src.YearEnd?.ToString()   ?? "";
                TxtSample.Text    = src.SampleSize?.ToString() ?? "";
                TxtNote.Text      = src.Notes;
                foreach (ComboBoxItem it in CboStatus.Items)
                    if ((string)it.Content == src.Status) { it.IsSelected = true; break; }
            }
            else
            {
                Title = "기수 추가";
                CboStatus.SelectedIndex = 1; // 진행중
                // 기수번호 기본값: 다음 번호 자동 추천
                int next = SurveySourceService.LoadPhases()
                    .Where(p => p.ProjectId == projectId)
                    .Select(p => p.PhaseNo).DefaultIfEmpty(0).Max() + 1;
                TxtNo.Text    = next.ToString();
                TxtLabel.Text = $"제{next}기";
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtNo.Text.Trim(), out int no) || no <= 0)
            {
                MessageBox.Show("기수 번호는 1 이상의 정수여야 합니다.", "확인",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(TxtLabel.Text))
            {
                MessageBox.Show("표시명은 필수입니다.", "확인",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int? ys = int.TryParse(TxtYearStart.Text.Trim(), out int v1) ? v1 : (int?)null;
            int? ye = int.TryParse(TxtYearEnd.Text.Trim(),   out int v2) ? v2 : (int?)null;
            int? smp= int.TryParse(TxtSample.Text.Trim(),     out int v3) ? v3 : (int?)null;

            string status = (CboStatus.SelectedItem is ComboBoxItem ci)
                ? (string)ci.Content : "";

            var p = _src ?? new SurveyPhase { ProjectId = _projectId };
            p.PhaseNo    = no;
            p.PhaseLabel = TxtLabel.Text.Trim();
            p.YearStart  = ys;
            p.YearEnd    = ye;
            p.SampleSize = smp;
            p.Status     = status;
            p.Notes      = TxtNote.Text.Trim();
            Result = p;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
