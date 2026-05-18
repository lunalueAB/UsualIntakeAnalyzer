using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace UsualIntakeAnalyzer.Views
{
    public partial class RegisterDatasetDialog : Window
    {
        public string Description  => TxtDesc.Text.Trim();
        public string RegisteredBy => TxtRegBy.Text.Trim();
        public string SelectedFilePath { get; private set; } = "";

        private string _csvFilter = "CSV 파일 (*.csv)|*.csv|모든 파일 (*.*)|*.*";

        public RegisterDatasetDialog(string title = "자료 등록")
        {
            InitializeComponent();
            Title         = title;
            TxtDate.Text  = DateTime.Now.ToString("yyyy-MM-dd");
            TxtRegBy.Text = Environment.UserName;
        }

        // 파일 선택
        private void BtnPickFile_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Title  = "CSV 파일 선택",
                Filter = _csvFilter
            };
            if (ofd.ShowDialog() != true) return;

            SelectedFilePath  = ofd.FileName;
            TxtFilePath.Text  = ofd.FileName;
            TxtFilePath.Foreground = (System.Windows.Media.Brush)
                FindResource("TextBrush");

            // 설명 자동 입력 (비어 있을 때만)
            if (string.IsNullOrWhiteSpace(TxtDesc.Text))
                TxtDesc.Text = Path.GetFileName(ofd.FileName);

            // 파일 선택되면 등록 버튼 활성화
            BtnOk.IsEnabled = true;
        }

        private void BtnOk_Click    (object s, RoutedEventArgs e) { DialogResult = true;  }
        private void BtnCancel_Click(object s, RoutedEventArgs e) { DialogResult = false; }
    }
}
