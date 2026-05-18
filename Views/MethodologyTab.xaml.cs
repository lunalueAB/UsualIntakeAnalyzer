using System;
using System.Windows;
using System.Windows.Controls;

namespace UsualIntakeAnalyzer.Views
{
    /// <summary>산출 방법론 소개 탭. NCI/ISU/MSM 카드 + 자세히 보기 + 분석하러 가기.</summary>
    public partial class MethodologyTab : UserControl
    {
        /// <summary>"분석하러 가기" 버튼 클릭 시 발생. MainWindow에서 분석 탭으로 전환한다.</summary>
        public event Action? GoToAnalysisRequested;

        public MethodologyTab()
        {
            InitializeComponent();
        }

        private void BtnNciDetail_Click(object sender, RoutedEventArgs e)
            => OpenDetail(MethodKind.Nci);

        private void BtnIsuDetail_Click(object sender, RoutedEventArgs e)
            => OpenDetail(MethodKind.Isu);

        private void BtnMsmDetail_Click(object sender, RoutedEventArgs e)
            => OpenDetail(MethodKind.Msm);

        private void OpenDetail(MethodKind kind)
        {
            var dlg = new MethodDetailDialog(kind) { Owner = Window.GetWindow(this) };
            dlg.ShowDialog();
        }

        private void BtnGoAnalysis_Click(object sender, RoutedEventArgs e)
            => GoToAnalysisRequested?.Invoke();
    }

    public enum MethodKind { Nci, Isu, Msm }
}
