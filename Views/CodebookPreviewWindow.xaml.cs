using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UsualIntakeAnalyzer.Models;
using UsualIntakeAnalyzer.Services;

namespace UsualIntakeAnalyzer.Views
{
    /// <summary>코드집 상세 조회 창 — 검색 + 행 표시.</summary>
    public partial class CodebookPreviewWindow : Window
    {
        private List<FoodCodeEntry> _all = new();

        public CodebookPreviewWindow(CodebookInfo info, string filePath)
        {
            InitializeComponent();
            TxtTitle.Text = $"코드집 — {info.FileName}";
            TxtMeta.Text  =
                $"전역 코드집  ·  업로드 {info.UploadedAt:yyyy-MM-dd HH:mm}  ·  {info.RowCount:N0}건";

            try
            {
                _all = ExcelParserService.ParseCodebook(filePath);
                Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show("코드집 로드 실패: " + ex.Message, "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Refresh()
        {
            string kw = TxtSearch.Text.Trim();
            var filtered = string.IsNullOrEmpty(kw) ? _all
                : _all.Where(c =>
                    c.FoodName .Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    c.FoodGroup.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    c.Code     .Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    c.CodeName .Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    c.SubCat1  .Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    c.SubCat2  .Contains(kw, StringComparison.OrdinalIgnoreCase)).ToList();
            GridEntries.ItemsSource = filtered;
            TxtCount.Text = $"{filtered.Count:N0} / {_all.Count:N0} 건";
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
            => Refresh();

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => Close();
    }
}
