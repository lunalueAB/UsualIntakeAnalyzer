using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UsualIntakeAnalyzer.Models;
using UsualIntakeAnalyzer.Services;

namespace UsualIntakeAnalyzer.Views
{
    /// <summary>
    /// 식품군 추가/편집 다이얼로그.
    /// 좌측: 모든 x0(2일 조사)에 등장한 식품 체크리스트.
    /// 우측: 선택된 식품 (✕로 제거 가능).
    /// </summary>
    public partial class FoodGroupEditDialog : Window
    {
        public FoodGroup? Result { get; private set; }
        private readonly FoodGroup? _src;

        public class FoodItem : INotifyPropertyChanged
        {
            private bool _isSelected;
            public string FoodName  { get; set; } = "";
            public string SubInfo   { get; set; } = "";
            public string CodeBadge { get; set; } = "";
            public HashSet<string> Codes { get; set; } = new();
            public bool IsSelected
            {
                get => _isSelected;
                set { _isSelected = value; PropertyChanged?.Invoke(this, new(nameof(IsSelected))); }
            }
            public event PropertyChangedEventHandler? PropertyChanged;
        }

        private List<FoodItem> _allFoods = new();
        private ObservableCollection<FoodItem> _shown    = new();
        private ObservableCollection<FoodItem> _selected = new();

        public FoodGroupEditDialog(FoodGroup? src = null)
        {
            InitializeComponent();
            _src = src;
            FoodList        .ItemsSource = _shown;
            SelectedFoodList.ItemsSource = _selected;
            Loaded += (_, _) => Init();
        }

        private void Init()
        {
            Title = _src == null ? "식품군 추가" : "식품군 편집";
            TxtTitle.Text = Title;

            BuildFoodList();
            ApplyFilter();

            if (_src != null)
            {
                TxtName.Text = _src.Name;
                TxtDesc.Text = _src.Description;
                var codeSet = new HashSet<string>(_src.FoodCodes);
                foreach (var f in _allFoods)
                    f.IsSelected = f.Codes.Any(c => codeSet.Contains(c));
                UpdateSummary();
            }
            UpdateSaveEnabled();
        }

        private void BuildFoodList()
        {
            _allFoods.Clear();
            if (!AppDataService.CodebookExists()) return;

            try
            {
                // 모든 x0의 fcode union
                var fcodeUnion = new HashSet<string>(StringComparer.Ordinal);
                foreach (var d in AppDataService.GetDatasetsByType(DatasetType.X0))
                {
                    var path = AppDataService.GetDatasetCsvPath(d.Id);
                    if (File.Exists(path))
                        fcodeUnion.UnionWith(CsvParserService.ScanFCodes(path));
                }

                var entries = ExcelParserService.ParseCodebook(AppDataService.GetCodebookPath());
                var filtered = entries
                    .Where(e => !string.IsNullOrWhiteSpace(e.FoodName))
                    .Where(e => fcodeUnion.Count == 0 || fcodeUnion.Contains(e.Code));
                var groups = filtered.GroupBy(e => e.FoodName).OrderBy(g => g.Key);
                foreach (var g in groups)
                {
                    var codes = g.Select(e => e.Code)
                                 .Where(c => !string.IsNullOrWhiteSpace(c)).ToHashSet();
                    var groups2 = g.Select(e => e.FoodGroup)
                                   .Where(fg => !string.IsNullOrWhiteSpace(fg))
                                   .Distinct().OrderBy(fg => fg).ToList();
                    var item = new FoodItem
                    {
                        FoodName  = g.Key,
                        SubInfo   = string.Join(" · ", groups2),
                        CodeBadge = $"{codes.Count}코드",
                        Codes     = codes
                    };
                    item.PropertyChanged += (_, _) => UpdateSummary();
                    _allFoods.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("식품 목록 빌드 실패: " + ex.Message, "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilter()
        {
            string kw = TxtSearch.Text.Trim();
            _shown.Clear();
            foreach (var f in _allFoods)
            {
                if (string.IsNullOrEmpty(kw) ||
                    f.FoodName.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    f.SubInfo .Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    f.Codes.Any(c => c.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                    _shown.Add(f);
            }
        }

        private void TxtSearch_TextChanged(object s, TextChangedEventArgs e)
            => ApplyFilter();

        private void BtnSelectAll_Click(object s, RoutedEventArgs e)
        {
            foreach (var f in _shown) f.IsSelected = true;
            UpdateSummary();
        }
        private void BtnDeselectAll_Click(object s, RoutedEventArgs e)
        {
            foreach (var f in _allFoods) f.IsSelected = false;
            UpdateSummary();
        }

        private void FoodItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is FoodItem f)
            {
                f.IsSelected = !f.IsSelected;
                UpdateSummary();
            }
        }

        private void BtnRemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is FoodItem f)
            {
                f.IsSelected = false;
                UpdateSummary();
            }
        }

        private void OnAnyChange(object sender, TextChangedEventArgs e)
            => UpdateSaveEnabled();

        private void UpdateSummary()
        {
            int sel  = _allFoods.Count(f => f.IsSelected);
            int code = _allFoods.Where(f => f.IsSelected).Sum(f => f.Codes.Count);
            TxtSummary.Text       = sel == 0 ? "선택 0개" : $"선택 {sel}개 식품  ·  {code:N0}개 코드";
            TxtSelectedHeader.Text = $"선택된 식품 ({sel})";

            _selected.Clear();
            foreach (var f in _allFoods.Where(x => x.IsSelected)
                                       .OrderBy(x => x.FoodName))
                _selected.Add(f);
            TxtSelectedEmpty.Visibility = sel == 0
                ? Visibility.Visible : Visibility.Collapsed;
            UpdateSaveEnabled();
        }

        private void UpdateSaveEnabled()
        {
            BtnSave.IsEnabled =
                !string.IsNullOrWhiteSpace(TxtName.Text) &&
                _allFoods.Any(f => f.IsSelected);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var sel = _allFoods.Where(f => f.IsSelected).ToList();
            var g = _src ?? new FoodGroup();
            g.Name        = TxtName.Text.Trim();
            g.Description = TxtDesc.Text.Trim();
            g.FoodCodes   = sel.SelectMany(f => f.Codes).Distinct().ToList();
            g.FoodNames   = sel.Select(f => f.FoodName).ToList();
            Result = g;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
