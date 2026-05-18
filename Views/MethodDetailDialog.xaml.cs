using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace UsualIntakeAnalyzer.Views
{
    /// <summary>방법론 상세 모달. 카드별로 상세 콘텐츠를 동적으로 채운다.</summary>
    public partial class MethodDetailDialog : Window
    {
        public MethodDetailDialog(MethodKind kind)
        {
            InitializeComponent();
            BuildContent(kind);
        }

        private void BuildContent(MethodKind kind)
        {
            switch (kind)
            {
                case MethodKind.Nci: BuildNci(); break;
                case MethodKind.Isu: BuildIsu(); break;
                case MethodKind.Msm: BuildMsm(); break;
            }
        }

        // ── NCI ─────────────────────────────────────────────────────────
        private void BuildNci()
        {
            Title         = "NCI 방법 — 자세히";
            TxtIcon.Text  = "🎯";
            TxtTitle.Text = "NCI Method";
            TxtSubtitle.Text =
                "National Cancer Institute Method (Tooze et al., 2006; 2010)";

            AddSection("개요",
                "미국 국립암연구소(NCI)에서 식이 조사 자료(24시간 회상법 등)로부터 " +
                "개인의 일상섭취량(usual intake)을 추정하기 위해 개발된 표준 통계 방법입니다. " +
                "비매일 섭취되는(episodic) 식품·영양소 모두에 적용 가능합니다.");

            AddSection("핵심 모델",
                "두 부분(two-part) 모델로 구성됩니다.");
            AddBullet("1단계 (확률 부분, probability):",
                      "특정일에 해당 식품을 섭취할 확률을 로지스틱 회귀로 추정. 임의효과(random effect)로 개인 간 이질성을 반영합니다.");
            AddBullet("2단계 (섭취량 부분, amount):",
                      "섭취일의 섭취량을 Box-Cox 변환 후 선형 혼합효과 모형으로 추정. 변환 후 정규성 가정을 만족하도록 합니다.");
            AddBullet("결합 (combination):",
                      "개인 일상섭취량 = E[섭취 확률] × E[섭취량 | 섭취일]. " +
                      "두 단계의 결합 분포에서 몬테카를로 시뮬레이션으로 분위수를 추정합니다.");

            AddSection("주요 통계량",
                "본 프로그램이 출력하는 핵심 지표:");
            AddBullet("rhoP", "확률 부분의 ICC(intra-class correlation). 0~1 사이, 클수록 개인 간 이질성이 큼.");
            AddBullet("rhoA", "섭취량 부분의 ICC. 변환 공간의 개인 간 분산 비율.");
            AddBullet("papa", "1일만 섭취 비율 (% one-day-only consumers).");
            AddBullet("0섭취율", "전체 응답에서 섭취하지 않은 비율(zero prevalence).");

            AddSection("강점 / 한계",
                "장점: 비매일 섭취 식품에 강건, 0 섭취가 많은 자료에도 적용 가능. " +
                "정책·기준 수립용 권장 표준.");
            AddNote(
                "한계: 0 섭취가 매우 많거나(>15%) 1일만 섭취 비율이 높을 때(>5%) " +
                "안정성이 떨어질 수 있어 본 프로그램은 자동으로 ISU 또는 MSM으로 보완합니다.");

            AddSection("참고 문헌",
                "Tooze JA, et al. \"A new statistical method for estimating the usual " +
                "intake of episodically consumed foods.\" J Am Diet Assoc, 2006.\n" +
                "Tooze JA, et al. \"A mixed-effects model approach for estimating the " +
                "distribution of usual intake of nutrients.\" Stat Med, 2010.");
        }

        // ── ISU ─────────────────────────────────────────────────────────
        private void BuildIsu()
        {
            Title         = "ISU 방법 — 자세히";
            TxtIcon.Text  = "📊";
            TxtTitle.Text = "ISU Method";
            TxtSubtitle.Text =
                "Iowa State University Method (Nusser, Carriquiry et al., 1996)";

            AddSection("개요",
                "아이오와 주립대 통계연구소(CSAFE)가 개발한 분포 기반(distribution-based) 방법입니다. " +
                "주로 매일 섭취하는 영양소·식품의 일상섭취량 분포 추정에 적합합니다.");

            AddSection("핵심 절차",
                "다음 4단계로 구성됩니다.");
            AddBullet("1) 정규화", "Box-Cox 변환으로 일별 섭취량을 근사 정규로 변환.");
            AddBullet("2) 분산 분리", "개인 내 분산(σ_w²)과 개인 간 분산(σ_b²)을 분산성분 분석(REML 등)으로 추정.");
            AddBullet("3) 축소(shrinkage)", "신뢰도 R = σ_b²/(σ_b²+σ_w²)을 이용해 개인 일상섭취 추정값을 축소(평균 쪽으로 끌어당김).");
            AddBullet("4) 역변환", "추정된 일상섭취량을 원래 단위로 역 Box-Cox 변환.");

            AddSection("자동 적용 조건 (본 프로그램)",
                "다음 두 조건을 모두 만족할 때 NCI 보완 방법으로 자동 적용됩니다.");
            AddBullet("papa ≤ 5%", "1일만 섭취한 응답자 비율이 낮음 (반복 섭취 양호).");
            AddBullet("0섭취율 ≤ 15%", "0 섭취 비율이 낮아 분포 변환이 안정적임.");

            AddSection("강점 / 한계",
                "장점: 계산 단순, 매일 섭취 식품(쌀·우유 등)에서 NCI 보다 안정. " +
                "분산 분리가 직관적.");
            AddNote(
                "한계: 0 섭취가 많은 비매일 섭취 식품에는 부적합. " +
                "이 경우 MSM 또는 NCI 사용을 권장합니다.");

            AddSection("참고 문헌",
                "Nusser SM, Carriquiry AL, Dodd KW, Fuller WA. " +
                "\"A semiparametric transformation approach to estimating usual daily " +
                "intake distributions.\" J Am Stat Assoc, 1996, 91:1440-1449.");
        }

        // ── MSM ─────────────────────────────────────────────────────────
        private void BuildMsm()
        {
            Title         = "MSM 방법 — 자세히";
            TxtIcon.Text  = "🧮";
            TxtTitle.Text = "MSM (Multiple Source Method)";
            TxtSubtitle.Text =
                "Multiple Source Method (Harttig, Haubrock et al., 2011 — BfR/EFSA)";

            AddSection("개요",
                "독일 연방위해평가원(BfR)에서 개발하고 EFSA(유럽식품안전청)에서 채택한 방법입니다. " +
                "단기 식이조사(보통 2일치)와 식품섭취빈도조사(FFQ)를 결합해 " +
                "비매일 섭취 식품의 일상섭취량 분포를 추정합니다.");

            AddSection("핵심 모델",
                "개인 일상섭취량을 두 가지 요소의 곱으로 분해합니다.");
            AddBullet("섭취 일수 비율 (intake probability)",
                      "FFQ 또는 24시간 회상에서 추정한 개인의 섭취 확률.");
            AddBullet("섭취일의 섭취량 (consumption amount)",
                      "섭취한 일자에 한해 변환·축소·평균화한 섭취량.");
            AddBullet("결합",
                      "개인 일상섭취량 = (섭취 확률) × (평균 섭취량). " +
                      "두 분포에서 시뮬레이션으로 일상섭취량 분위수 추정.");

            AddSection("자동 적용 조건 (본 프로그램)",
                "다음 중 하나라도 해당하면 NCI 보완 방법으로 자동 적용됩니다.");
            AddBullet("papa > 5%", "1일만 섭취한 응답자 비율이 높음 (저빈도 섭취).");
            AddBullet("0섭취율 > 15%", "0 섭취가 많은 episodic 식품 (예: 어류·견과류).");

            AddSection("강점 / 한계",
                "장점: 0 섭취가 많은 식품에 강건. 24시간 회상만으로도 적용 가능. " +
                "EFSA 식이노출평가 기본 도구로 채택됨.");
            AddNote(
                "한계: 매일 섭취 식품에서는 ISU 보다 분산 추정이 다소 불안정할 수 있음. " +
                "이 경우 NCI 또는 ISU 결과와 비교 권장.");

            AddSection("참고 문헌",
                "Harttig U, Haubrock J, Knüppel S, Boeing H. " +
                "\"The MSM program: web-based statistics package for estimating usual " +
                "dietary intake using the Multiple Source Method.\" " +
                "Eur J Clin Nutr, 2011;65(S1):S87-S91.");
        }

        // ── 콘텐츠 빌더 헬퍼 ──────────────────────────────────────────
        private void AddSection(string title, string body)
        {
            PnlBody.Children.Add(new TextBlock
            {
                Text       = title,
                FontSize   = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextBrush"),
                Margin     = new Thickness(0, 14, 0, 6)
            });
            PnlBody.Children.Add(new TextBlock
            {
                Text         = body,
                FontSize     = 12,
                LineHeight   = 22,
                TextWrapping = TextWrapping.Wrap,
                Foreground   = (Brush)FindResource("TextBrush"),
                Margin       = new Thickness(0, 0, 0, 4)
            });
        }

        private void AddBullet(string label, string body)
        {
            var tb = new TextBlock
            {
                FontSize     = 12,
                LineHeight   = 22,
                TextWrapping = TextWrapping.Wrap,
                Foreground   = (Brush)FindResource("TextBrush"),
                Margin       = new Thickness(12, 2, 0, 2)
            };
            tb.Inlines.Add(new Run("• ")
                { Foreground = (Brush)FindResource("AccentBrush") });
            tb.Inlines.Add(new Run(label + " ") { FontWeight = FontWeights.SemiBold });
            tb.Inlines.Add(new Run(body)
                { Foreground = (Brush)FindResource("TextSecBrush") });
            PnlBody.Children.Add(tb);
        }

        private void AddNote(string body)
        {
            var border = new Border
            {
                Background      = (Brush)FindResource("ElevatedBrush"),
                BorderBrush     = (Brush)FindResource("WarningBrush"),
                BorderThickness = new Thickness(3, 0, 0, 0),
                Padding         = new Thickness(12, 8, 12, 8),
                Margin          = new Thickness(0, 8, 0, 4),
                CornerRadius    = new CornerRadius(2),
                Child           = new TextBlock
                {
                    Text         = body,
                    FontSize     = 12,
                    LineHeight   = 20,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground   = (Brush)FindResource("TextSecBrush")
                }
            };
            PnlBody.Children.Add(border);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => Close();
    }
}
