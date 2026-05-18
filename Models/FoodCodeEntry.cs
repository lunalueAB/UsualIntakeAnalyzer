namespace UsualIntakeAnalyzer.Models
{
    /// <summary>코드집(codezip.xlsx) 한 행</summary>
    public class FoodCodeEntry
    {
        public string No         { get; set; } = "";
        public string Code       { get; set; } = "";   // 1차코드 (fcode 필터 기준)
        public string CodeName   { get; set; } = "";   // 1차코드명
        public string FoodGroup  { get; set; } = "";   // 국건영 식품군
        public string MimsCode   { get; set; } = "";
        public string MimsName   { get; set; } = "";   // MIMS품목명
        public string FoodName   { get; set; } = "";   // 식품명 (사용자 선택 기준)
        public string SubCat1    { get; set; } = "";   // 상세분류
        public string SubCat2    { get; set; } = "";   // 소분류

        // UI용: 사용자가 체크했는지
        public bool IsSelected   { get; set; }
    }
}
