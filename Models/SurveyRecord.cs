namespace UsualIntakeAnalyzer.Models
{
    /// <summary>원시 CSV 행 하나(x0 또는 x1 파일의 개별 식품 섭취 행)</summary>
    public class SurveyRecord
    {
        public string Id          { get; set; } = "";
        public int    Sex         { get; set; }   // 1=남, 2=여
        public int    Age         { get; set; }
        public int    AgeG        { get; set; }   // 1-8
        public string AgeGDesc    { get; set; } = "";
        public int    Day         { get; set; }   // x0: 1 or 2, x1: 1
        public int    Region      { get; set; }
        public double WtNtr       { get; set; }   // 영양섭취 가중치
        public string FCode       { get; set; } = "";
        public double NfIntk      { get; set; }   // 해당 식품 섭취량
        public double Ffq         { get; set; }
        public string TownT       { get; set; } = "";
        public int    HoIncm      { get; set; }   // 소득분위 1-4
        public string Edu         { get; set; } = "";
        public string GenertnType { get; set; } = "";
        public string RegionType  { get; set; } = "";
    }

    /// <summary>식품코드 필터링+합산 후 1인 1일 레코드</summary>
    public class PersonRecord
    {
        public string Id          { get; set; } = "";
        public int    Sex         { get; set; }
        public int    Age         { get; set; }
        public int    AgeG        { get; set; }
        public string AgeGDesc    { get; set; } = "";
        public int    Day         { get; set; }   // x1은 항상 1
        public double WtNtr       { get; set; }
        public double NfIntk      { get; set; }   // 선택 식품 합산 섭취량
        public double Ffq         { get; set; }
        public int    HoIncm      { get; set; }
        public string TownT       { get; set; } = "";
        public string Edu         { get; set; } = "";
        public string GenertnType { get; set; } = "";
        public string RegionType  { get; set; } = "";
        public int    Region      { get; set; }
    }
}
