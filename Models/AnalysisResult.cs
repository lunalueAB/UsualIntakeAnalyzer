using System.Collections.Generic;

namespace UsualIntakeAnalyzer.Models
{
    /// <summary>성별·연령군 별 일상섭취량 통계</summary>
    public class QuantileRow
    {
        public string Sex      { get; set; } = "ALL";
        public string AgeGDesc { get; set; } = "ALL";
        public int    N        { get; set; }
        public double Average  { get; set; }
        public double Sd       { get; set; }
        public double P1st     { get; set; }
        public double P5th     { get; set; }
        public double P25th    { get; set; }
        public double Median   { get; set; }
        public double P75th    { get; set; }
        public double P90th    { get; set; }
        public double P95th    { get; set; }
        public double P975th   { get; set; }
        public double P99th    { get; set; }
        public double Min      { get; set; }
        public double Max      { get; set; }
    }

    /// <summary>분석 실행 결과 전체</summary>
    public class AnalysisResult
    {
        // ── NCI / 공통 파라미터 ─────────────────────────────────────────────
        public double RhoP  { get; set; }
        public double RhoA  { get; set; }
        public double Papa  { get; set; }           // % 1일만 섭취
        public double ZeroPrevalence { get; set; }  // % 0 섭취

        // ── 분산성분 (ISU/MSM 방법 사용 시 채워짐) ──────────────────────────
        public double SigmaB2     { get; set; }   // 개인 간 분산 (변환 공간)
        public double SigmaW2     { get; set; }   // 개인 내 분산 (변환 공간)
        public double Reliability { get; set; }   // σ_b²/(σ_b²+σ_w²)
        public bool   LogTransformed { get; set; }

        // ── 이론적 감마 분포 파라미터 (NCI 전용) ───────────────────────────
        public double GammaShape { get; set; }
        public double GammaScale { get; set; }

        // ── 사용된 방법 정보 ────────────────────────────────────────────────
        /// <summary>실제 적용된 방법: "NCI" | "ISU" | "MSM"</summary>
        public string MethodUsed { get; set; } = "NCI";
        /// <summary>방법 선택 근거 메시지</summary>
        public string MethodNote { get; set; } = "";

        /// <summary>개인별 일상섭취 추정값 (시각화용)</summary>
        public List<PersonIntake> PersonIntakes { get; set; } = new();

        /// <summary>전체 + 성별/연령 그룹 통계 테이블</summary>
        public List<QuantileRow> ResultTable { get; set; } = new();

        /// <summary>
        /// 보완 분석 결과 (NCI 기본 + 자동 선택)
        ///   papa ≤ 5% AND 0섭취율 ≤ 15%  → ISU 보완
        ///   papa > 5%  OR  0섭취율 > 15% → MSM 보완
        /// </summary>
        public AnalysisResult? AdditionalResult { get; set; }
    }

    public class PersonIntake
    {
        public string Id       { get; set; } = "";
        public int    Sex      { get; set; }
        public string AgeGDesc { get; set; } = "";
        public double Intake   { get; set; }   // 일상섭취 추정량
        public double RawIntk  { get; set; }   // 1일 실측 섭취량(x1)
        public double Weight   { get; set; }
    }
}
