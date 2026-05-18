using System;
using System.Collections.Generic;

namespace UsualIntakeAnalyzer.Models
{
    /// <summary>
    /// 분석 시나리오 — 식품군(이름 + 1차코드 집합) + 사용할 x1·x0 데이터셋 + 시뮬 횟수 + 등록자.
    /// 한 번 등록하면 클릭 한 번으로 동일 조건 재분석이 가능하다.
    /// </summary>
    public class Scenario
    {
        public string Id           { get; set; } = Guid.NewGuid().ToString();

        /// <summary>식품군명 (예: 적색육) — 식품군 DB의 Name과 동일</summary>
        public string Name         { get; set; } = "";

        /// <summary>참조하는 식품군 Id (사이드 패널에서 선택한 FoodGroup.Id)</summary>
        public string FoodGroupId  { get; set; } = "";

        /// <summary>표시용 — 등록 당시 선택한 식품명 목록</summary>B
        public List<string> FoodNames { get; set; } = new();

        /// <summary>분석 필터로 사용할 1차코드 목록</summary>
        public List<string> FoodCodes { get; set; } = new();

        /// <summary>분석에 사용할 1일 조사(x1) 데이터셋 Id 목록 (다중 선택)</summary>
        public List<string> X1Ids { get; set; } = new();

        /// <summary>분석에 사용할 2일 조사(x0) 데이터셋 Id 목록 (다중 선택)</summary>
        public List<string> X0Ids { get; set; } = new();

        /// <summary>[하위 호환] 단일 x1 Id — 구 버전 JSON 역직렬화용. 신규 코드는 X1Ids 사용.</summary>
        public string X1Id { get; set; } = "";

        /// <summary>[하위 호환] 단일 x0 Id — 구 버전 JSON 역직렬화용. 신규 코드는 X0Ids 사용.</summary>
        public string X0Id { get; set; } = "";

        public int      SimTime      { get; set; } = 5;
        public string   RegisteredBy { get; set; } = "";
        public DateTime RegisteredAt { get; set; } = DateTime.Now;
        public DateTime? LastAnalyzedAt { get; set; }
    }
}
