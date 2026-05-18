using System.Windows;
using UsualIntakeAnalyzer.Models;

namespace UsualIntakeAnalyzer.Views
{
    public partial class ProjectEditDialog : Window
    {
        public SurveyProject? Result { get; private set; }
        private readonly SurveyProject? _src;

        public ProjectEditDialog(SurveyProject? src)
        {
            InitializeComponent();
            _src = src;
            if (src != null)
            {
                Title = "사업 편집";
                TxtCode.Text         = src.ProjectCode;
                TxtNameKo.Text       = src.NameKo;
                TxtNameEn.Text       = src.NameEn;
                TxtConducting.Text   = src.ConductingOrg;
                TxtCommission.Text   = src.CommissionOrg;
                TxtDomain.Text       = src.SurveyDomain;
                TxtNote.Text         = src.Description;
            }
            else
            {
                Title = "사업 추가";
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtCode.Text) ||
                string.IsNullOrWhiteSpace(TxtNameKo.Text))
            {
                MessageBox.Show("사업 약어코드와 사업명(한글)은 필수입니다.", "확인",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var p = _src ?? new SurveyProject();
            p.ProjectCode   = TxtCode.Text.Trim();
            p.NameKo        = TxtNameKo.Text.Trim();
            p.NameEn        = TxtNameEn.Text.Trim();
            p.ConductingOrg = TxtConducting.Text.Trim();
            p.CommissionOrg = TxtCommission.Text.Trim();
            p.SurveyDomain  = TxtDomain.Text.Trim();
            p.Description   = TxtNote.Text.Trim();
            Result = p;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
