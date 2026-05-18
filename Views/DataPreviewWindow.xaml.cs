using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using UsualIntakeAnalyzer.Models;

namespace UsualIntakeAnalyzer.Views
{
    public partial class DataPreviewWindow : Window
    {
        private readonly DatasetInfo _info;
        private readonly string      _csvPath;

        public DataPreviewWindow(DatasetInfo info, string csvPath)
        {
            InitializeComponent();
            _info    = info;
            _csvPath = csvPath;
            LoadData();
        }

        private void LoadData()
        {
            TxtMeta1.Text = $"설명: {_info.Description}";
            TxtMeta2.Text = $"등록자: {_info.RegisteredBy}";
            TxtMeta3.Text = $"등록일: {_info.RegisteredAt:yyyy-MM-dd HH:mm}";

            try
            {
                var dt = ReadCsvToDataTable(_csvPath, maxRows: 1000);
                GridData.ItemsSource = dt.DefaultView;
                TxtRowInfo.Text = $"미리보기 {Math.Min(dt.Rows.Count, 1000):N0}행  (전체 {_info.RowCount:N0}행)";
            }
            catch (Exception ex)
            {
                MessageBox.Show("데이터 로드 실패: " + ex.Message);
            }
        }

        private static DataTable ReadCsvToDataTable(string path, int maxRows = 1000)
        {
            var dt = new DataTable();
            using var reader = new StreamReader(path, System.Text.Encoding.UTF8);
            string? header = reader.ReadLine();
            if (header == null) return dt;

            foreach (var col in header.Split(','))
                dt.Columns.Add(col.Trim('"'));

            int count = 0;
            string? line;
            while ((line = reader.ReadLine()) != null && count < maxRows)
            {
                var parts = SplitLine(line);
                var row = dt.NewRow();
                for (int i = 0; i < Math.Min(parts.Length, dt.Columns.Count); i++)
                    row[i] = parts[i].Trim('"');
                dt.Rows.Add(row);
                count++;
            }
            return dt;
        }

        private static string[] SplitLine(string line)
        {
            var fields = new List<string>();
            bool inQ = false;
            var cur = new System.Text.StringBuilder();
            foreach (char c in line)
            {
                if (c == '"') { inQ = !inQ; continue; }
                if (c == ',' && !inQ) { fields.Add(cur.ToString()); cur.Clear(); continue; }
                cur.Append(c);
            }
            fields.Add(cur.ToString());
            return fields.ToArray();
        }

        private void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                Title      = "CSV 저장",
                Filter     = "CSV (*.csv)|*.csv",
                FileName   = _info.FileName
            };
            if (sfd.ShowDialog() != true) return;
            try
            {
                File.Copy(_csvPath, sfd.FileName, overwrite: true);
                MessageBox.Show("저장 완료: " + sfd.FileName, "완료",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("저장 실패: " + ex.Message, "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
