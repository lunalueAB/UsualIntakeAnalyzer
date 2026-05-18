using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using UsualIntakeAnalyzer.Models;

namespace UsualIntakeAnalyzer.Views
{
    /// <summary>
    /// 산출식품(프리셋) 추가/편집. 식품 목록은 보기·삭제만 지원하며,
    /// 새 식품을 추가하려면 분석 탭에서 선택을 갱신한 뒤 다시 등록한다.
    /// </summary>
    public partial class FoodPresetEditDialog : Window
    {
        public FoodPreset? Result { get; private set; }
        private readonly FoodPreset _src;
        private readonly bool _isNew;

        public class FoodRow
        {
            public string Name { get; set; } = "";
            public string Code { get; set; } = "";
        }

        private ObservableCollection<FoodRow> _rows = new();

        public FoodPresetEditDialog(FoodPreset src, bool isNew)
        {
            InitializeComponent();
            _src   = src;
            _isNew = isNew;

            Title = isNew ? "산출식품 추가" : "산출식품 편집";
            TxtName.Text = src.Name;
            TxtDesc.Text = src.Description;

            // 식품 행 빌드 — Name과 Code를 zip
            var names = src.FoodNames ?? new List<string>();
            var codes = src.FoodCodes ?? new List<string>();
            int max = System.Math.Max(names.Count, codes.Count);

            // 이름이 없으면 코드만 표시
            if (names.Count == 0 && codes.Count > 0)
            {
                foreach (var c in codes) _rows.Add(new FoodRow { Name = "(코드만)", Code = c });
            }
            else
            {
                // 이름 단위로 한 행씩
                for (int i = 0; i < names.Count; i++)
                {
                    _rows.Add(new FoodRow
                    {
                        Name = names[i],
                        Code = i < codes.Count ? codes[i] : ""
                    });
                }
            }

            GridFoods.ItemsSource = _rows;
            UpdateInfo();
        }

        private void UpdateInfo()
        {
            TxtCount.Text = $"식품 {_rows.Count}개  ·  코드 {_src.FoodCodes.Count}개";
            TxtCodes.Text = _src.FoodCodes.Count == 0
                ? ""
                : "1차코드: " + string.Join(", ", _src.FoodCodes.Take(40))
                  + (_src.FoodCodes.Count > 40 ? $"  외 {_src.FoodCodes.Count - 40}개" : "");
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (GridFoods.SelectedItem is not FoodRow row) return;
            // 식품 삭제: 이름 한 개 + 같은 인덱스 코드(있다면) 정렬 무관 — 보수적으로 매칭만 제거
            _rows.Remove(row);
            _src.FoodNames.Remove(row.Name);
            if (!string.IsNullOrEmpty(row.Code))
                _src.FoodCodes.Remove(row.Code);
            UpdateInfo();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                MessageBox.Show("명칭은 필수입니다.", "확인",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_src.FoodCodes.Count == 0)
            {
                if (MessageBox.Show(
                        "코드가 없는 산출식품입니다. 그대로 저장할까요?",
                        "확인", MessageBoxButton.YesNo, MessageBoxImage.Question)
                    != MessageBoxResult.Yes) return;
            }
            _src.Name        = TxtName.Text.Trim();
            _src.Description = TxtDesc.Text.Trim();
            Result = _src;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
