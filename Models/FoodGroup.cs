using System;
using System.Collections.Generic;

namespace UsualIntakeAnalyzer.Models
{
    /// <summary>
    /// 식품군 — 사용자가 정의한 식품 묶음 (예: 적색육, 과일류).
    /// 분석 시나리오는 이 식품군을 참조해 1차코드 집합을 가져온다.
    /// 기본 식품군은 시드로 제공되며, 사용자가 직접 추가/수정/삭제할 수 있다.
    /// </summary>
    public class FoodGroup
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Name        { get; set; } = "";
        public string Description { get; set; } = "";

        /// <summary>분석 필터로 사용할 1차코드 목록</summary>
        public List<string> FoodCodes { get; set; } = new();

        /// <summary>표시용 — 코드에 매핑된 식품명 목록 (저장 시점 기준)</summary>
        public List<string> FoodNames { get; set; } = new();

        public bool   IsBuiltIn { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
