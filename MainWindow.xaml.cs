using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace UsualIntakeAnalyzer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // DB탭에서 코드집/자료원 변경 시 분석탭 갱신
            DbTabCtrl.CodebookChanged += () => AnalysisTabCtrl.RefreshCodebook();

            // 방법론 탭의 "분석하러 가기" → 분석 탭으로 전환
            MethodTabCtrl.GoToAnalysisRequested += () => SelectNav(NavAnalysis);

            // 메모리 표시 (1초 단위 업데이트)
            var ramTimer = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromSeconds(2) };
            ramTimer.Tick += (_, _) =>
            {
                long mb = GC.GetTotalMemory(false) / (1024 * 1024);
                TxtStatusRam.Text = $"RAM {mb} MB";
            };
            ramTimer.Start();

            UpdateContextStatus();
        }

        // ── Window controls ──────────────────────────────────────────────
        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized
                             ? WindowState.Normal
                             : WindowState.Maximized;

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // ── Sidebar nav (라디오 그룹) ────────────────────────────────────
        private void NavToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton tb) SelectNav(tb);
        }

        private void SelectNav(ToggleButton selected)
        {
            // 라디오처럼 동작: 다른 toggle은 false
            NavMethod.IsChecked   = (selected == NavMethod);
            NavAnalysis.IsChecked = (selected == NavAnalysis);
            NavDb.IsChecked       = (selected == NavDb);

            UpdateNavIconStates();

            MethodTabCtrl  .Visibility = selected == NavMethod   ? Visibility.Visible : Visibility.Collapsed;
            AnalysisTabCtrl.Visibility = selected == NavAnalysis ? Visibility.Visible : Visibility.Collapsed;
            DbTabCtrl      .Visibility = selected == NavDb       ? Visibility.Visible : Visibility.Collapsed;

            UpdateContextStatus();
        }

        /// <summary>토글 상태에 따라 좌측 아이콘 박스 색을 액센트/회색으로 전환</summary>
        private void UpdateNavIconStates()
        {
            var accent = (Brush)FindResource("AccentBrush");
            var hover  = (Brush)FindResource("BgHoverBrush");

            NavMethodIcon  .Background = NavMethod  .IsChecked == true ? accent : hover;
            NavAnalysisIcon.Background = NavAnalysis.IsChecked == true ? accent : hover;
            NavDbIcon      .Background = NavDb      .IsChecked == true ? accent : hover;

            // 아이콘 안 텍스트 색도 함께 조정
            SetIconForeground(NavMethodIcon,   NavMethod  .IsChecked == true);
            SetIconForeground(NavAnalysisIcon, NavAnalysis.IsChecked == true);
            SetIconForeground(NavDbIcon,       NavDb      .IsChecked == true);
        }

        private static void SetIconForeground(Border iconHolder, bool active)
        {
            if (iconHolder.Child is TextBlock tb)
                tb.Foreground = active
                    ? Brushes.White
                    : (Brush)Application.Current.FindResource("TextBrush");
        }

        // ── 상태바 컨텍스트 문구 ─────────────────────────────────────────
        private void UpdateContextStatus()
        {
            string ctx;
            if (NavMethod.IsChecked == true)        ctx = "방법론 비교 — NCI · ISU · MSM";
            else if (NavAnalysis.IsChecked == true) ctx = "시나리오 기반 분석";
            else if (NavDb.IsChecked == true)       ctx = "등록된 자료 조회·관리";
            else                                    ctx = "";
            TxtStatusContext.Text = ctx;
        }

        private void MethodTabCtrl_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}
