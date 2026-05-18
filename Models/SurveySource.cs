using System;
using System.Collections.Generic;

namespace UsualIntakeAnalyzer.Models
{
    /// <summary>조사사업 마스터 — 대분류 (예: 국민건강영양조사, 정밀영양조사)</summary>
    public class SurveyProject
    {
        public string Id            { get; set; } = Guid.NewGuid().ToString();
        public string ProjectCode   { get; set; } = "";   // 약어코드 (KNHANES, KPNC …)
        public string NameKo        { get; set; } = "";   // 사업명 (한글)
        public string NameEn        { get; set; } = "";   // 사업명 (영문)
        public string ConductingOrg { get; set; } = "";   // 수행기관
        public string CommissionOrg { get; set; } = "";   // 의뢰/위탁기관
        public string SurveyDomain  { get; set; } = "";   // 조사 분야
        public string Description   { get; set; } = "";   // 비고
        public bool   IsBuiltIn     { get; set; }         // 시드 데이터 여부
    }

    /// <summary>기수 (1기, 2기, 3기 …) — 설계 변경 단위</summary>
    public class SurveyPhase
    {
        public string Id          { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId   { get; set; } = "";    // FK -> SurveyProject.Id
        public int    PhaseNo     { get; set; }          // 기수 번호 (1, 2, 3 …)
        public string PhaseLabel  { get; set; } = "";    // "제1기"
        public int?   YearStart   { get; set; }
        public int?   YearEnd     { get; set; }
        public string Status      { get; set; } = "";    // 완료 / 진행중 / 예정
        public int?   SampleSize  { get; set; }
        public string Notes       { get; set; } = "";
        public bool   IsBuiltIn   { get; set; }
    }

    /// <summary>차수 (연도·회차) — 실제 조사 수행 단위</summary>
    public class SurveyRound
    {
        public string Id         { get; set; } = Guid.NewGuid().ToString();
        public string PhaseId    { get; set; } = "";    // FK -> SurveyPhase.Id
        public int    RoundNo    { get; set; }          // 차수 번호 (1, 2, 3 …)
        public int?   SurveyYear { get; set; }
        public string FieldEnd   { get; set; } = "";    // 현장조사 종료
        public string Status     { get; set; } = "";    // 완료 / 중단 / 진행중
        public string Notes      { get; set; } = "";
        public bool   IsBuiltIn  { get; set; }

        /// <summary>화면 표시용 — 예: "1차 (2025)"</summary>
        public string DisplayLabel
            => SurveyYear.HasValue ? $"{RoundNo}차 ({SurveyYear})" : $"{RoundNo}차";
    }

    /// <summary>현재 활성 차수(분석에 사용될 자료원) 식별자</summary>
    public class ActiveSourceState
    {
        public string ActiveRoundId { get; set; } = "";
    }
}
