using System.Collections.Generic;
using ClosedXML.Excel;
using UsualIntakeAnalyzer.Models;

namespace UsualIntakeAnalyzer.Services
{
    public static class ExcelParserService
    {
        /// <summary>코드집(codezip.xlsx) 파싱</summary>
        public static List<FoodCodeEntry> ParseCodebook(string path)
        {
            var result = new List<FoodCodeEntry>();
            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet(1);

            bool first = true;
            foreach (var row in ws.RowsUsed())
            {
                if (first) { first = false; continue; } // 헤더 스킵

                var entry = new FoodCodeEntry
                {
                    No        = GetStr(row, 1),
                    Code      = GetStr(row, 2),
                    CodeName  = GetStr(row, 3),
                    FoodGroup = GetStr(row, 4),
                    MimsCode  = GetStr(row, 5),
                    MimsName  = GetStr(row, 6),
                    FoodName  = GetStr(row, 7),
                    SubCat1   = GetStr(row, 8),
                    SubCat2   = GetStr(row, 9)
                };

                if (!string.IsNullOrWhiteSpace(entry.Code))
                    result.Add(entry);
            }
            return result;
        }

        private static string GetStr(IXLRow row, int col)
        {
            var cell = row.Cell(col);
            return cell.IsEmpty() ? "" : cell.GetString().Trim();
        }
    }
}
