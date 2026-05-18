using System;

namespace UsualIntakeAnalyzer.Models
{
    public enum DatasetType
    {
        X0,
        X1,
        /// <summary>식이기록법 정밀영양 데이터 — 파서 미구현, 등록만 가능.</summary>
        PrecisionNutrition
    }

    /// <summary>등록된 국건영 자료의 메타데이터</summary>
    public class DatasetInfo
    {
        public string      Id          { get; set; } = Guid.NewGuid().ToString();
        public DatasetType Type        { get; set; }

        /// <summary>소속 차수(SurveyRound.Id). 미지정(=구 데이터)은 빈 문자열.</summary>
        public string      RoundId     { get; set; } = "";

        public DateTime    RegisteredAt{ get; set; } = DateTime.Now;
        public string      Description { get; set; } = "";
        public string      RegisteredBy{ get; set; } = "";
        public string      FileName    { get; set; } = "";   // 저장된 csv 파일명
        public int         RowCount    { get; set; }
    }

    /// <summary>등록된 코드집 메타데이터 (차수 단위)</summary>
    public class CodebookInfo
    {
        public string   Id         { get; set; } = Guid.NewGuid().ToString();

        /// <summary>소속 차수(SurveyRound.Id). 차수와 1:1 매핑 (한 차수에 코드집 1개).</summary>
        public string   RoundId    { get; set; } = "";

        public DateTime UploadedAt { get; set; } = DateTime.Now;
        public string   FileName   { get; set; } = "";
        public int      RowCount   { get; set; }
    }
}
