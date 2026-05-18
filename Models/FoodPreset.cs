using System;
using System.Collections.Generic;

namespace UsualIntakeAnalyzer.Models
{
    /// <summary>
    /// 산출식품 — 사용자가 명명한 식품 조합 프리셋.
    /// 한 번 시뮬레이션한 결과는 (차수 · 시뮬횟수 · 식품조합) 키로 캐시되어
    /// 명칭 클릭 만으로 즉시 결과를 볼 수 있다.
    /// </summary>
    public class FoodPreset
    {
        public string Id          { get; set; } = Guid.NewGuid().ToString();
        public string Name        { get; set; } = "";
        public string Description { get; set; } = "";

        /// <summary>1차코드 목록 (분석 필터로 사용)</summary>
        public List<string> FoodCodes { get; set; } = new();

        /// <summary>표시용 — 저장 당시 선택된 식품명 목록 (UI 안내)</summary>
        public List<string> FoodNames { get; set; } = new();

        public DateTime CreatedAt   { get; set; } = DateTime.Now;
        public DateTime UpdatedAt   { get; set; } = DateTime.Now;

        /// <summary>마지막 분석 시각(가장 최근 캐시 기준, 표시용)</summary>
        public DateTime? LastAnalyzedAt { get; set; }
    }
}
