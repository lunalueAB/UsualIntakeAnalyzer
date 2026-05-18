using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UsualIntakeAnalyzer.Models;

namespace UsualIntakeAnalyzer.Services
{
    public static class CsvParserService
    {
        // ── 공개 진입점 ───────────────────────────────────────────────────────

        /// <summary>x0 CSV(2일 조사) 파싱. day 컬럼 필수.</summary>
        public static List<SurveyRecord> ParseX0(string path)
            => ParseByHeader(path, defaultDay: null);

        /// <summary>x1 CSV(1일 조사) 파싱. day 컬럼 없으면 Day=1 고정.</summary>
        public static List<SurveyRecord> ParseX1(string path)
            => ParseByHeader(path, defaultDay: 1);

        /// <summary>
        /// x0 또는 fcode 컬럼을 가진 CSV에서 unique fcode 값만 추려 반환.
        /// 풀 ParseX0/X1보다 훨씬 가볍다 — 식품 목록 빌드용.
        /// </summary>
        public static HashSet<string> ScanFCodes(string path)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (!File.Exists(path)) return set;

            using var reader = new StreamReader(path, DetectEncoding(path));
            string? header = reader.ReadLine();
            if (header == null) return set;

            var headers = SplitCsv(header);
            int fIdx = IndexOf(headers, "fcode");
            if (fIdx < 0) return set;   // fcode 컬럼 없음 (이미 집계된 x1 등)

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var cols = SplitCsv(line);
                if (cols.Length <= fIdx) continue;
                var v = Clean(cols[fIdx]);
                if (string.IsNullOrEmpty(v) || v == "*") continue;
                set.Add(v);
            }
            return set;
        }

        // ── 핵심 파싱 로직 (헤더명 기반 자동 매핑) ──────────────────────────

        /// <summary>
        /// 헤더행을 읽어 컬럼명→인덱스 사전을 구성한 뒤,
        /// 이름 기반으로 각 행을 SurveyRecord로 변환한다.
        /// 컬럼 순서나 앞에 추가된 컬럼(year, survey_no, survey_seq 등)에
        /// 영향받지 않아 향후 구조 변경에도 자동 적응한다.
        /// </summary>
        /// <param name="path">CSV 파일 경로</param>
        /// <param name="defaultDay">
        ///   day 컬럼이 없을 때 사용할 기본값.
        ///   null이면 day 컬럼이 반드시 있어야 한다(x0용).
        ///   1이면 1일 조사로 간주(x1용).
        /// </param>
        private static List<SurveyRecord> ParseByHeader(string path, int? defaultDay)
        {
            var result = new List<SurveyRecord>();
            using var reader = new StreamReader(path, DetectEncoding(path));

            string? headerLine = reader.ReadLine();
            if (headerLine == null) return result;

            // 헤더명 → 인덱스 사전 (대소문자 무시)
            var rawHeaders = SplitCsv(headerLine);
            var col = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rawHeaders.Length; i++)
                col[rawHeaders[i].Trim('"').Trim()] = i;

            bool hasFcode = col.ContainsKey("fcode");
            bool hasDay   = col.ContainsKey("day");

            // 필수 컬럼 존재 확인
            if (!col.ContainsKey("id")      || !col.ContainsKey("sex")     ||
                !col.ContainsKey("age")     || !col.ContainsKey("ageg")    ||
                !col.ContainsKey("ageg_desc") || !col.ContainsKey("wt_ntr") ||
                !col.ContainsKey("nf_intk"))
            {
                // 필수 컬럼 누락 — 파싱 불가
                return result;
            }

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var c = SplitCsv(line);

                string id = GetStr(c, col, "id");
                if (string.IsNullOrEmpty(id)) continue;

                result.Add(new SurveyRecord
                {
                    Id          = id,
                    Sex         = GetInt(c, col, "sex"),
                    Age         = GetInt(c, col, "age"),
                    AgeG        = GetInt(c, col, "ageg"),
                    AgeGDesc    = UnDateify(GetStr(c, col, "ageg_desc")),
                    Day         = hasDay ? GetInt(c, col, "day") : (defaultDay ?? 1),
                    Region      = GetInt(c, col, "region"),
                    WtNtr       = GetDbl(c, col, "wt_ntr"),
                    FCode       = hasFcode ? GetStr(c, col, "fcode") : "*",
                    NfIntk      = GetDbl(c, col, "nf_intk"),
                    Ffq         = GetDbl(c, col, "ffq"),
                    TownT       = GetStr(c, col, "town_t"),
                    HoIncm      = GetInt(c, col, "ho_incm"),
                    Edu         = GetStr(c, col, "edu"),
                    GenertnType = GetStr(c, col, "genertn_type"),
                    RegionType  = GetStr(c, col, "region_type")
                });
            }
            return result;
        }

        // ── 컬럼 접근 헬퍼 ──────────────────────────────────────────────────

        /// <summary>
        /// 엑셀이 "10-19" 같은 연령 구간을 날짜로 자동변환한 값을 원래 구간 표기로 복원한다.
        /// 변환 패턴 예시:
        ///   "10월19일"  → "10-19"
        ///   "10월 19일" → "10-19"  (공백 허용)
        ///   "1월2일"    → "01-02"  (복원 후 2자리 정규화)
        /// 날짜 패턴이 아닌 경우에도 "1-9" → "01-09" 형태로 정규화한다.
        /// </summary>
        private static readonly Regex _datePattern =
            new(@"^(\d{1,2})월\s*(\d{1,2})일$", RegexOptions.Compiled);

        private static readonly Regex _ageRangePattern =
            new(@"^(\d{1,2})-(\d{1,2})$", RegexOptions.Compiled);

        private static string UnDateify(string s)
        {
            s = s.Trim();

            // 날짜로 자동변환된 값 복원: "10월19일" → "10-19"
            var dm = _datePattern.Match(s);
            if (dm.Success)
                s = $"{dm.Groups[1].Value}-{dm.Groups[2].Value}";

            // 연령 구간 형식이면 앞의 0 제거: "01-09" → "1-9", "1-9" → "1-9"
            var rm = _ageRangePattern.Match(s);
            if (rm.Success)
            {
                int lo = int.Parse(rm.Groups[1].Value);
                int hi = int.Parse(rm.Groups[2].Value);
                return $"{lo}-{hi}";
            }

            return s;
        }

        private static int IndexOf(string[] headers, string name)
        {
            for (int i = 0; i < headers.Length; i++)
                if (string.Equals(headers[i].Trim('"').Trim(), name,
                                  StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        private static string GetStr(string[] cols,
            Dictionary<string, int> col, string name)
        {
            return col.TryGetValue(name, out int i) && i < cols.Length
                ? Clean(cols[i]) : "";
        }

        private static int GetInt(string[] cols,
            Dictionary<string, int> col, string name)
            => ParseInt(GetStr(cols, col, name));

        private static double GetDbl(string[] cols,
            Dictionary<string, int> col, string name)
            => ParseDouble(GetStr(cols, col, name));

        // ── 인코딩 자동 감지 ─────────────────────────────────────────────

        /// <summary>
        /// UTF-8 BOM 확인 → UTF-8 유효성 검사 → 실패 시 EUC-KR(CP949) 순으로 인코딩을 결정한다.
        /// 국건영 CSV는 UTF-8 또는 CP949(EUC-KR)로 배포된다.
        /// </summary>
        private static System.Text.Encoding DetectEncoding(string path)
        {
            const int SampleSize = 8192;
            byte[] sample;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var buf = new byte[SampleSize];
                int read = fs.Read(buf, 0, buf.Length);
                sample = read < buf.Length ? buf[..read] : buf;
            }

            // BOM(EF BB BF) 확인
            if (sample.Length >= 3 &&
                sample[0] == 0xEF && sample[1] == 0xBB && sample[2] == 0xBF)
                return new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

            // UTF-8 유효성 검사 (엄격 모드)
            try
            {
                var decoder = System.Text.Encoding.UTF8.GetDecoder();
                decoder.Fallback = System.Text.DecoderFallback.ExceptionFallback;
                var chars = new char[sample.Length];
                decoder.GetChars(sample, 0, sample.Length, chars, 0);
                return System.Text.Encoding.UTF8;   // 유효한 UTF-8
            }
            catch (System.Text.DecoderFallbackException)
            {
                // UTF-8 아님 → EUC-KR / CP949
                try
                {
                    return System.Text.Encoding.GetEncoding("ks_c_5601-1987");
                }
                catch
                {
                    try
                    {
                        return System.Text.Encoding.GetEncoding(949); // 코드페이지 직접 지정
                    }
                    catch
                    {
                        return System.Text.Encoding.UTF8; // 최후 폴백
                    }
                }
            }
        }

        // ── CSV 분리 및 기타 헬퍼 ─────────────────────────────────────────

        private static string[] SplitCsv(string line)
        {
            var fields = new List<string>();
            bool inQuote = false;
            var cur = new System.Text.StringBuilder();
            foreach (char ch in line)
            {
                if (ch == '"') { inQuote = !inQuote; continue; }
                if (ch == ',' && !inQuote) { fields.Add(cur.ToString()); cur.Clear(); continue; }
                cur.Append(ch);
            }
            fields.Add(cur.ToString());
            return fields.ToArray();
        }

        private static string Clean(string s) => s.Trim().Trim('"');

        private static int ParseInt(string s)
        {
            s = Clean(s);
            return int.TryParse(s, out int v) ? v : 0;
        }

        private static double ParseDouble(string s)
        {
            s = Clean(s);
            if (s == "NA" || s == "") return 0;
            return double.TryParse(s, NumberStyles.Any,
                                   CultureInfo.InvariantCulture, out double v) ? v : 0;
        }
    }
}
