using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Distributions;
using UsualIntakeAnalyzer.Models;

namespace UsualIntakeAnalyzer.Services
{
    /// <summary>
    /// NCI 일상섭취량 추정 알고리즘 (R 코드 C# 포팅)
    /// - rhoP/rhoA: x0(2일) Pearson 상관계수
    /// - 시뮬레이션: Logistic GLM + Gamma GLM (IRLS)
    /// </summary>
    public static class UsualIntakeCalculator
    {
        private const int Week = 52;

        // ── 진입점 ──────────────────────────────────────────────────────────
        /// <summary>
        /// NCI를 기본으로 실행하고, 자료 특성에 따라 ISU 또는 MSM 보완 분석을 자동 추가한다.
        ///   papa ≤ 5% AND 0섭취율 ≤ 15%  → ISU BLUP 보완 (일상적 섭취 식품)
        ///   papa > 5%  OR  0섭취율 > 15% → MSM      보완 (간헐적 섭취 식품)
        /// </summary>
        public static AnalysisResult Compute(
            List<SurveyRecord> x0Raw,
            List<SurveyRecord> x1Raw,
            HashSet<string> selectedFCodes,
            int simTime = 5,
            IProgress<string>? progress = null)
        {
            progress?.Report("데이터 필터링 및 집계 중...");

            // 1. fcode 필터 + 집계
            var x0Persons = AggregateX0(x0Raw, selectedFCodes);
            var x1Persons = AggregateX1(x1Raw, selectedFCodes);

            if (x0Persons.Count == 0 || x1Persons.Count == 0)
                throw new InvalidOperationException("선택한 식품코드에 해당하는 데이터가 없습니다.");

            progress?.Report("rho 추정 중 (x0 상관계수)...");

            // 2. rhoP, rhoA, papa 추정 (x0 wide format)
            var (papa, rhoP, rhoA) = EstimateRho(x0Persons);

            double zeroPrevalence = x1Persons.Count > 0
                ? (double)x1Persons.Count(r => r.NfIntk == 0) / x1Persons.Count * 100.0
                : 0;

            // 3. NCI 기본 분석 (항상 실행)
            progress?.Report($"[NCI] rhoP={rhoP:F4}, rhoA={rhoA:F4}, papa={papa:F1}% · 시뮬레이션 시작...");
            double[] nciIntakes = RunSimulation(x1Persons, rhoP, rhoA, simTime, progress);

            progress?.Report("결과 집계 중...");
            var result = BuildResult(x1Persons, nciIntakes, rhoP, rhoA, papa);
            result.ZeroPrevalence = zeroPrevalence;
            result.MethodUsed     = "NCI";
            result.MethodNote     = $"NCI — rhoP={rhoP:F4}, rhoA={rhoA:F4}, papa={papa:F1}%, 0섭취율={zeroPrevalence:F1}%";

            // 4. 자료 특성 자동 감지 → 보완 분석
            bool isRegular  = papa <= 5.0 && zeroPrevalence <= 15.0;   // 일상적 섭취 → ISU
            bool isEpisodic = papa >  5.0 || zeroPrevalence >  15.0;   // 간헐적 섭취 → MSM

            try
            {
                if (isRegular)
                {
                    progress?.Report($"[ISU] 보완 분석 중 (일상적 섭취 패턴 감지: papa={papa:F1}%, 0섭취율={zeroPrevalence:F1}%)...");
                    var (isuIntakes, sb2, sw2, rel, logT) = RunISU(x0Persons, x1Persons, progress);
                    var isuResult = BuildResult(x1Persons, isuIntakes, rhoP, rhoA, papa);
                    isuResult.ZeroPrevalence = zeroPrevalence;
                    isuResult.MethodUsed     = "ISU";
                    isuResult.SigmaB2        = sb2;
                    isuResult.SigmaW2        = sw2;
                    isuResult.Reliability    = rel;
                    isuResult.LogTransformed = logT;
                    isuResult.MethodNote     = $"ISU 보완 적용 (일상적 섭취 식품)" +
                                               $"\n  σ_b²={sb2:F4}, σ_w²={sw2:F4}, λ={rel:F3}" +
                                               (logT ? " [로그변환]" : " [원척도]");
                    result.AdditionalResult = isuResult;
                }
                else if (isEpisodic)
                {
                    progress?.Report($"[MSM] 보완 분석 중 (간헐적 섭취 패턴 감지: papa={papa:F1}%, 0섭취율={zeroPrevalence:F1}%)...");
                    var (msmIntakes, sb2, sw2, rel) = RunMSM(x0Persons, x1Persons, progress);
                    var msmResult = BuildResult(x1Persons, msmIntakes, rhoP, rhoA, papa);
                    msmResult.ZeroPrevalence = zeroPrevalence;
                    msmResult.MethodUsed     = "MSM";
                    msmResult.SigmaB2        = sb2;
                    msmResult.SigmaW2        = sw2;
                    msmResult.Reliability    = rel;
                    msmResult.LogTransformed = true;
                    msmResult.MethodNote     = $"MSM 보완 적용 (간헐적 섭취 식품)" +
                                               $"\n  σ_b²={sb2:F4}, σ_w²={sw2:F4}, λ={rel:F3} [로그정규]";
                    result.AdditionalResult = msmResult;
                }
            }
            catch (Exception ex)
            {
                // 보완 분석 실패 시 NCI 결과는 그대로 반환
                result.MethodNote += $"\n⚠ 보완 분석 실패: {ex.Message}";
            }

            return result;
        }

        // ── 1. 데이터 집계 ──────────────────────────────────────────────────
        /// <summary>x0: fcode 필터 → id+day 별 합산 → Wide(1인 2행) 목록 반환
        /// [수정] 전체 참여자 기준으로 (Id, Day) 골격을 먼저 구성한 뒤
        ///        선택 식품 섭취량을 합산하여 비섭취일을 NfIntk=0으로 보존.
        ///        이전 방식은 선택 식품을 한 번도 먹지 않은 날의 레코드가 사라져
        ///        papa가 항상 0%로 계산되고 rhoP가 0.95에 고정되는 버그가 있었음.
        /// </summary>
        private static List<PersonRecord> AggregateX0(List<SurveyRecord> raw, HashSet<string> codes)
        {
            // 1단계: 전체 raw에서 (Id, Day) 골격 구성 — 모든 참여자·모든 조사일 포함
            var skeleton = raw
                .GroupBy(r => (r.Id, r.Day))
                .Select(g =>
                {
                    var first = g.First();
                    return new PersonRecord
                    {
                        Id          = first.Id,
                        Sex         = first.Sex,
                        Age         = first.Age,
                        AgeG        = first.AgeG,
                        AgeGDesc    = first.AgeGDesc,
                        Day         = first.Day,
                        Region      = first.Region,
                        WtNtr       = first.WtNtr,
                        NfIntk      = 0,   // 2단계에서 채움
                        Ffq         = first.Ffq,
                        TownT       = first.TownT,
                        HoIncm      = first.HoIncm,
                        Edu         = first.Edu,
                        GenertnType = first.GenertnType,
                        RegionType  = first.RegionType
                    };
                })
                .ToDictionary(p => (p.Id, p.Day));

            // 2단계: 선택 식품 필터 후 (Id, Day)별 섭취량 합산 → 골격에 채움
            var intakeByIdDay = (codes.Count == 0 ? raw : raw.Where(r => codes.Contains(r.FCode)))
                .GroupBy(r => (r.Id, r.Day))
                .ToDictionary(g => g.Key, g => g.Sum(r => r.NfIntk));

            foreach (var kv in intakeByIdDay)
            {
                if (skeleton.TryGetValue(kv.Key, out var rec))
                    rec.NfIntk = kv.Value;
            }

            var grouped = skeleton.Values
                .OrderBy(r => r.Id).ThenBy(r => r.Day)
                .ToList();

            return grouped;
        }

        /// <summary>x1: fcode 필터 → id 별 합산</summary>
        private static List<PersonRecord> AggregateX1(List<SurveyRecord> raw, HashSet<string> codes)
        {
            IEnumerable<SurveyRecord> filtered = codes.Count == 0
                ? raw
                : raw.Where(r => codes.Contains(r.FCode) || r.FCode == "*");

            return filtered
                .GroupBy(r => r.Id)
                .Select(g =>
                {
                    var first = g.First();
                    return new PersonRecord
                    {
                        Id          = first.Id,
                        Sex         = first.Sex,
                        Age         = first.Age,
                        AgeG        = first.AgeG,
                        AgeGDesc    = first.AgeGDesc,
                        Day         = 1,
                        Region      = first.Region,
                        WtNtr       = first.WtNtr,
                        NfIntk      = g.Sum(r => r.NfIntk),
                        Ffq         = first.Ffq,
                        TownT       = first.TownT,
                        HoIncm      = first.HoIncm,
                        Edu         = first.Edu,
                        GenertnType = first.GenertnType,
                        RegionType  = first.RegionType
                    };
                })
                .OrderBy(r => r.Id)
                .ToList();
        }

        // ── 2. rhoP/rhoA 추정 ───────────────────────────────────────────────
        private static (double papa, double rhoP, double rhoA) EstimateRho(List<PersonRecord> x0)
        {
            // Wide format: 같은 id에서 day1/day2 쌍 만들기
            var byId = x0
                .GroupBy(r => r.Id)
                .Where(g => g.Count() == 2)
                .Select(g =>
                {
                    var arr = g.OrderBy(r => r.Day).ToArray();
                    return (a1: arr[0].NfIntk, a2: arr[1].NfIntk);
                })
                .ToList();

            if (byId.Count == 0) return (0, 0.5, 0.5);

            // papa: 정확히 한 날만 섭취한 비율
            int classOne = byId.Count(p => (p.a1 == 0) != (p.a2 == 0));
            double papa = (double)classOne / byId.Count * 100;

            // eatP(섭취 여부) 간 Pearson 상관
            double[] ep1 = byId.Select(p => p.a1 > 0 ? 1.0 : 0.0).ToArray();
            double[] ep2 = byId.Select(p => p.a2 > 0 ? 1.0 : 0.0).ToArray();
            double corP = PearsonCorr(ep1, ep2);

            // eatA(양) 간 Pearson 상관 - 양쪽 모두 섭취한 사람만
            var eaters = byId.Where(p => p.a1 > 0 && p.a2 > 0).ToList();
            double corA = eaters.Count >= 3
                ? PearsonCorr(eaters.Select(p => p.a1).ToArray(), eaters.Select(p => p.a2).ToArray())
                : 0;

            double rhoP = papa < 5 ? 0.95 : (double.IsNaN(corP) ? 0.5 : corP);
            double rhoA = double.IsNaN(corA) ? 0.5 : corA;

            return (papa, rhoP, rhoA);
        }

        // ── 3-A. ISU BLUP 추정 ──────────────────────────────────────────────
        /// <summary>
        /// ISU(Iowa State University) 방법 — BLUP 기반 일상섭취량 추정
        ///
        /// 원리:
        ///   x0(2일 반복 자료)로 개인 내(σ_w²) / 개인 간(σ_b²) 분산을 추정하고,
        ///   x1(1일 자료)의 각 개인에 대해 BLUP(Best Linear Unbiased Predictor)를 계산한다.
        ///
        ///   BLUP_i = μ + λ·(y_i − μ)    where λ = σ_b²/(σ_b² + σ_w²)
        ///
        ///   분포가 우편향일 때는 로그변환 공간에서 추정 후 역변환.
        ///
        /// 적합 대상:
        ///   papa ≤ 5%, 0섭취율 ≤ 15% 인 '일상적 섭취' 식품·영양소
        ///   (예: 에너지, 나트륨, 쌀, 김치 등)
        /// </summary>
        private static (double[] intakes, double sigmaB2, double sigmaW2,
                         double reliability, bool logTransformed)
            RunISU(List<PersonRecord> x0, List<PersonRecord> x1,
                   IProgress<string>? progress)
        {
            progress?.Report("[ISU] 분산성분 추정 중 (x0 2일 자료)...");

            // 2일 쌍 구성 (x0)
            var pairs = x0
                .GroupBy(r => r.Id)
                .Where(g => g.Count() >= 2)
                .Select(g =>
                {
                    var arr = g.OrderBy(r => r.Day).ToArray();
                    return (d1: arr[0].NfIntk, d2: arr[1].NfIntk, w: arr[0].WtNtr);
                })
                .ToList();

            if (pairs.Count < 5)
                throw new InvalidOperationException(
                    "ISU 추정을 위한 2일 반복자료 쌍이 부족합니다 (최소 5쌍 필요).");

            // 로그변환 가능 여부 판단 (전체 양수이면 로그 적용)
            bool useLog = pairs.All(p => p.d1 > 0 && p.d2 > 0)
                       && x1.All(r => r.NfIntk > 0);

            Func<double, double> T    = useLog ? v => Math.Log(v) : v => v;
            Func<double, double> Tinv = useLog ? v => Math.Exp(v) : v => v;

            // 분산성분 추정 (가중치 적용)
            double sw = pairs.Sum(p => p.w);

            // σ_w²: within-person variance = E[(t(d1)-t(d2))² / 2]
            double sigmaW2 = pairs.Sum(p => p.w * Math.Pow(T(p.d1) - T(p.d2), 2) / 2.0) / sw;

            // 개인 평균 → σ_total²
            var personMeans = pairs.Select(p =>
                (mean: (T(p.d1) + T(p.d2)) / 2.0, w: p.w)).ToList();

            double grandMean0 = personMeans.Sum(m => m.w * m.mean) / sw;
            double sigmaTotal2 = personMeans.Sum(m => m.w * Math.Pow(m.mean - grandMean0, 2)) / sw;

            // σ_b²: between-person variance = max(0, σ_total² − σ_w²/2)
            double sigmaB2 = Math.Max(0, sigmaTotal2 - sigmaW2 / 2.0);

            // 신뢰도 λ
            double reliability = (sigmaB2 + sigmaW2) > 1e-15
                ? sigmaB2 / (sigmaB2 + sigmaW2)
                : 0.5;

            progress?.Report($"[ISU] σ_b²={sigmaB2:F4}, σ_w²={sigmaW2:F4}, " +
                             $"λ={reliability:F3}{(useLog ? " (로그변환)" : "")}");

            // x1 grand mean (변환 공간)
            double swX1 = x1.Sum(r => r.WtNtr);
            double muX1 = swX1 > 1e-15
                ? x1.Sum(r => r.WtNtr * T(Math.Max(r.NfIntk, 1e-9))) / swX1
                : x1.Average(r => T(Math.Max(r.NfIntk, 1e-9)));

            // BLUP 계산 후 역변환
            double[] intakes = x1.Select(r =>
            {
                double ty  = T(Math.Max(r.NfIntk, 1e-9));
                double blup = muX1 + reliability * (ty - muX1);
                return Math.Max(0.0, Tinv(blup));
            }).ToArray();

            progress?.Report($"[ISU] 완료 — {x1.Count:N0}명 추정");

            return (intakes, sigmaB2, sigmaW2, reliability, useLog);
        }

        // ── 3-B. MSM (Multiple Source Method) ──────────────────────────────
        /// <summary>
        /// MSM 방법 — 간헐적 섭취 식품에 대한 두 단계 모델
        ///
        /// 원리:
        ///   usual_i = p̂ × E(섭취량 | 섭취일)_i
        ///
        ///   p̂    = x1 전체 중 섭취자 비율 (섭취일 확률)
        ///   E(·)  = 섭취일 로그정규 분포에 BLUP 적용:
        ///             log_blup_i = μ_log + λ·(log(y_i) - μ_log)
        ///             amount_i   = exp(log_blup_i + (1-λ)·σ_w²/2)   ← 로그정규 보정
        ///
        ///   x0의 '양일 모두 섭취' 쌍에서 σ_w²(within), σ_b²(between) 추정
        ///   비섭취자(x1 = 0)는 모집단 평균 사용: p̂·exp(μ_log + σ_w²/2)
        ///
        /// 적합 대상:
        ///   papa > 5% 또는 0섭취율 > 15% 인 간헐적·에피소딕 식품
        ///   (예: 어패류, 견과류, 특정 채소류 등)
        /// </summary>
        private static (double[] intakes, double sigmaB2, double sigmaW2, double reliability)
            RunMSM(List<PersonRecord> x0, List<PersonRecord> x1, IProgress<string>? progress)
        {
            progress?.Report("[MSM] 섭취 확률 및 로그정규 파라미터 추정 중...");

            // ── 1. 섭취 확률 추정 ────────────────────────────────────────────
            double pHat = x1.Count > 0
                ? (double)x1.Count(r => r.NfIntk > 0) / x1.Count
                : 0;

            if (pHat < 1e-9)
                throw new InvalidOperationException("MSM: 섭취 기록이 전혀 없습니다 (p̂ = 0).");

            // ── 2. 섭취일 로그정규 파라미터 추정 (x1 양수 기록) ─────────────
            var posX1 = x1.Where(r => r.NfIntk > 0).ToList();
            if (posX1.Count < 5)
                throw new InvalidOperationException("MSM: 섭취일 기록이 부족합니다 (최소 5건 필요).");

            double swPos  = posX1.Sum(r => r.WtNtr);
            if (swPos < 1e-15) swPos = posX1.Count;

            double muLog = posX1.Sum(r => r.WtNtr * Math.Log(r.NfIntk)) / swPos;
            double sigmaTotal2 = posX1.Sum(r =>
                r.WtNtr * Math.Pow(Math.Log(r.NfIntk) - muLog, 2)) / swPos;

            // ── 3. 분산성분 추정 (x0 '양일 모두 섭취' 쌍) ──────────────────
            progress?.Report("[MSM] x0 2일 반복 자료로 분산성분 추정 중...");

            var consumingPairs = x0
                .GroupBy(r => r.Id)
                .Where(g => g.Count() >= 2)
                .Select(g =>
                {
                    var arr = g.OrderBy(r => r.Day).ToArray();
                    return (d1: arr[0].NfIntk, d2: arr[1].NfIntk, w: arr[0].WtNtr);
                })
                .Where(p => p.d1 > 0 && p.d2 > 0)   // 양일 모두 섭취한 쌍만
                .ToList();

            double sigmaW2, sigmaB2, reliability;

            if (consumingPairs.Count >= 3)
            {
                double sw2 = consumingPairs.Sum(p => p.w);
                if (sw2 < 1e-15) sw2 = consumingPairs.Count;

                // σ_w² = E[(log(d1) - log(d2))² / 2]  (within-person, 섭취일 기준)
                sigmaW2 = consumingPairs.Sum(p =>
                    p.w * Math.Pow(Math.Log(p.d1) - Math.Log(p.d2), 2) / 2.0) / sw2;

                // σ_b² = max(0, σ_total² - σ_w²/2)
                sigmaB2 = Math.Max(0, sigmaTotal2 - sigmaW2 / 2.0);
                reliability = (sigmaB2 + sigmaW2) > 1e-15
                    ? sigmaB2 / (sigmaB2 + sigmaW2)
                    : 0.5;
            }
            else
            {
                // 양일 섭취 쌍이 부족: σ_total²를 절반씩 배분 (보수적 추정)
                sigmaW2     = sigmaTotal2 * 0.5;
                sigmaB2     = sigmaTotal2 * 0.5;
                reliability = 0.5;
                progress?.Report("[MSM] ⚠ 양일 섭취 쌍 부족 — 분산성분 기본값 사용 (λ=0.5)");
            }

            progress?.Report($"[MSM] p̂={pHat:F3}, μ_log={muLog:F3}, σ_w²={sigmaW2:F4}, λ={reliability:F3}");

            // ── 4. 개인별 일상섭취 추정 (BLUP + 역변환) ─────────────────────
            // 모집단 섭취일 평균 (로그정규 역변환, lognormal correction 포함)
            double popAmount = Math.Exp(muLog + sigmaW2 / 2.0);

            double[] intakes = x1.Select(r =>
            {
                if (r.NfIntk > 0)
                {
                    // 섭취자: 로그 공간 BLUP → 역변환
                    double logY    = Math.Log(r.NfIntk);
                    double logBlup = muLog + reliability * (logY - muLog);
                    // 로그정규 보정: 추정 오차 분산 (1-λ)σ_w² 반영
                    double amount  = Math.Exp(logBlup + (1.0 - reliability) * sigmaW2 / 2.0);
                    return pHat * amount;
                }
                else
                {
                    // 비섭취자: 모집단 평균으로 대체
                    return pHat * popAmount;
                }
            }).ToArray();

            progress?.Report($"[MSM] 완료 — {x1.Count:N0}명 추정");

            return (intakes, sigmaB2, sigmaW2, reliability);
        }

        // ── 3-D. 시뮬레이션 (NCI) ───────────────────────────────────────────
        private static double[] RunSimulation(
            List<PersonRecord> x1, double rhoP, double rhoA, int simTime,
            IProgress<string>? progress)
        {
            int n = x1.Count;
            double[] eatA = x1.Select(r => r.NfIntk).ToArray();
            double[] eatP = eatA.Select(a => a > 0 ? 1.0 : 0.0).ToArray();

            // 설계행렬: [intercept, ageG2..8(7개), hoIncm2..4(3개)] = 11열
            double[][] X = BuildDesignMatrix(x1);

            double[] accIntake = new double[n];

            // 시드 고정 (R 코드와 동일 방식)
            var masterRng = new Random(20180412);
            int[] seeds = Enumerable.Range(0, simTime)
                                    .Select(_ => masterRng.Next(1, 100001))
                                    .ToArray();

            double lastAlpha = 1, lastBeta = 1;

            for (int iter = 0; iter < simTime; iter++)
            {
                progress?.Report($"시뮬레이션 {iter + 1}/{simTime} 반복 중...");
                var rng = new Random(seeds[iter]);

                // (a) Logistic regression → Prob
                double[] prob = LogisticFitted(X, eatP);

                // (b) Gamma GLM (log link) → Amount, Phi
                int[] posIdx = Enumerable.Range(0, n).Where(i => eatA[i] > 0).ToArray();
                double[][] Xpos = posIdx.Select(i => X[i]).ToArray();
                double[] ypos   = posIdx.Select(i => eatA[i]).ToArray();

                double[] betaGamma = GammaGlmFitted(Xpos, ypos);
                double[] allMu     = ComputeMu(X, betaGamma);
                double phi         = ComputePhi(Xpos, ypos, betaGamma);

                double alpha = Math.Max(1.0 / phi, 1e-6);

                // (c) 조정 확률 / 조정 섭취량
                double sqRho = Math.Sign(rhoA) * Math.Sqrt(Math.Abs(rhoA));
                double[] Iprob   = new double[n];
                double[] Iamount = new double[n];

                for (int j = 0; j < n; j++)
                {
                    Iprob[j] = eatP[j] == 1
                        ? prob[j] + (1 - prob[j]) * rhoP
                        : prob[j] * (1 - rhoP);

                    double betaJ = alpha / Math.Max(allMu[j], 1e-9);
                    Iamount[j] = eatP[j] == 1
                        ? eatA[j]
                        : SampleGamma(rng, alpha, betaJ);

                    lastAlpha = alpha;
                    lastBeta  = betaJ;
                }

                // (d) 개인별 주간 시뮬레이션
                double[] intake = new double[n];
                for (int j = 0; j < n; j++)
                {
                    double betaJ  = alpha / Math.Max(allMu[j], 1e-9);
                    double beta1  = betaJ  / (1 + sqRho);
                    double alpha1 = alpha  * (1 - sqRho) / (1 + sqRho);
                    alpha1 = Math.Max(alpha1, 1e-9);
                    beta1  = Math.Max(beta1,  1e-9);

                    double sum = 0;
                    for (int w = 0; w < Week - 1; w++)
                    {
                        int ate  = rng.NextDouble() < Iprob[j] ? 1 : 0;
                        double z = SampleGamma(rng, alpha1, beta1);
                        double food = sqRho * Iamount[j] + z;
                        if (food < 0) food = 0;
                        sum += food * ate;
                    }
                    intake[j] = sum / (Week - 1);
                }

                for (int j = 0; j < n; j++)
                    accIntake[j] += double.IsNaN(intake[j]) ? 0 : intake[j];
            }

            // 평균
            for (int j = 0; j < n; j++)
                accIntake[j] /= simTime;

            return accIntake;
        }

        // ── 4. 결과 빌드 ────────────────────────────────────────────────────
        private static AnalysisResult BuildResult(
            List<PersonRecord> x1, double[] intakes,
            double rhoP, double rhoA, double papa)
        {
            int n = x1.Count;
            var personIntakes = new List<PersonIntake>(n);
            for (int i = 0; i < n; i++)
            {
                personIntakes.Add(new PersonIntake
                {
                    Id       = x1[i].Id,
                    Sex      = x1[i].Sex,
                    AgeGDesc = x1[i].AgeGDesc,
                    Intake   = intakes[i],
                    RawIntk  = x1[i].NfIntk,
                    Weight   = x1[i].WtNtr
                });
            }

            var rows = new List<QuantileRow>();
            rows.Add(CalcRow("ALL", "ALL", intakes));

            var groups = personIntakes
                .GroupBy(p => (p.Sex.ToString(), p.AgeGDesc))
                .OrderBy(g => g.Key.Item1).ThenBy(g => g.Key.Item2);

            foreach (var g in groups)
            {
                var vals = g.Select(p => p.Intake).ToArray();
                rows.Add(CalcRow(g.Key.Item1 == "1" ? "남자" : "여자", g.Key.Item2, vals));
            }

            // 이론적 감마 분포: 양수 추정값에 가중 MOM 적합 (R의 alpha_c/beta_c)
            var (gammaShape, gammaScale) = FitWeightedGamma(personIntakes);

            return new AnalysisResult
            {
                RhoP       = rhoP,  RhoA      = rhoA, Papa = papa,
                GammaShape = gammaShape,
                GammaScale = gammaScale,
                PersonIntakes = personIntakes,
                ResultTable   = rows
            };
        }

        /// <summary>가중 감마 적합 (Method of Moments, R의 alpha_c/beta_c 대응)</summary>
        private static (double shape, double scale) FitWeightedGamma(List<PersonIntake> persons)
        {
            var pos = persons.Where(p => p.Intake > 0).ToList();
            if (pos.Count < 3) return (1, 1);

            double sw   = pos.Sum(p => p.Weight);
            double swx  = pos.Sum(p => p.Weight * p.Intake);
            double swxx = pos.Sum(p => p.Weight * p.Intake * p.Intake);

            if (sw <= 0) return (1, 1);

            double a = swx  / sw;           // E[X]
            double b = swxx / sw;           // E[X²]
            double varW = b - a * a;        // Var[X]
            if (varW <= 0) return (1, 1);

            double shape = (a * a) / varW;  // α = μ²/σ²
            double scale = a / shape;        // β = μ/α  (scale = 1/rate)

            return (shape, scale);
        }

        private static QuantileRow CalcRow(string sex, string ageGDesc, double[] vals)
        {
            if (vals.Length == 0)
                return new QuantileRow { Sex = sex, AgeGDesc = ageGDesc };
            Array.Sort(vals);
            return new QuantileRow
            {
                Sex      = sex,
                AgeGDesc = ageGDesc,
                N        = vals.Length,
                Average  = vals.Average(),
                Sd       = StdDev(vals),
                P1st     = Quantile(vals, 0.01),
                P5th     = Quantile(vals, 0.05),
                P25th    = Quantile(vals, 0.25),
                Median   = Quantile(vals, 0.50),
                P75th    = Quantile(vals, 0.75),
                P90th    = Quantile(vals, 0.90),
                P95th    = Quantile(vals, 0.95),
                P975th   = Quantile(vals, 0.975),
                P99th    = Quantile(vals, 0.99),
                Min      = vals[0],
                Max      = vals[vals.Length - 1]
            };
        }

        // ── 통계 헬퍼 ───────────────────────────────────────────────────────
        private static double PearsonCorr(double[] x, double[] y)
        {
            int n = x.Length;
            if (n < 2) return double.NaN;
            double mx = x.Average(), my = y.Average();
            double num = 0, dx = 0, dy = 0;
            for (int i = 0; i < n; i++)
            {
                num += (x[i] - mx) * (y[i] - my);
                dx  += (x[i] - mx) * (x[i] - mx);
                dy  += (y[i] - my) * (y[i] - my);
            }
            double denom = Math.Sqrt(dx * dy);
            return denom < 1e-15 ? 0 : num / denom;
        }

        private static double StdDev(double[] v)
        {
            double mean = v.Average();
            double ss   = v.Sum(x => (x - mean) * (x - mean));
            return Math.Sqrt(ss / (v.Length - 1));
        }

        private static double Quantile(double[] sortedV, double p)
        {
            int n = sortedV.Length;
            double pos = p * (n - 1);
            int lo = (int)pos;
            int hi = Math.Min(lo + 1, n - 1);
            return sortedV[lo] + (pos - lo) * (sortedV[hi] - sortedV[lo]);
        }

        // ── 설계행렬 ────────────────────────────────────────────────────────
        /// <summary>intercept + ageG(7 dummies) + hoIncm(3 dummies) = 11 열</summary>
        private static double[][] BuildDesignMatrix(List<PersonRecord> persons)
        {
            return persons.Select(p =>
            {
                var row = new double[11];
                row[0] = 1.0; // intercept
                int ag = p.AgeG;
                if (ag >= 2 && ag <= 8) row[ag - 1] = 1.0;   // idx 1-7
                int hi = p.HoIncm;
                if (hi >= 2 && hi <= 4) row[7 + hi - 1] = 1.0; // idx 8-10
                return row;
            }).ToArray();
        }

        // ── Logistic Regression (Newton-Raphson) ───────────────────────────
        private static double[] LogisticFitted(double[][] X, double[] y)
        {
            int n = X.Length, p = X[0].Length;
            double[] beta = new double[p];

            for (int iter = 0; iter < 50; iter++)
            {
                double[] prob = X.Select(xi => Sigmoid(Dot(xi, beta))).ToArray();
                // Gradient: X^T (y - p)
                double[] grad = new double[p];
                for (int j = 0; j < p; j++)
                    for (int i = 0; i < n; i++)
                        grad[j] += X[i][j] * (y[i] - prob[i]);

                // Hessian: X^T W X  (W = diag(p*(1-p)))
                double[][] H = new double[p][];
                for (int j = 0; j < p; j++) H[j] = new double[p];
                for (int i = 0; i < n; i++)
                {
                    double w = prob[i] * (1 - prob[i]);
                    for (int j = 0; j < p; j++)
                        for (int k = 0; k < p; k++)
                            H[j][k] += w * X[i][j] * X[i][k];
                }

                double[] delta = SolveLinear(H, grad);
                double norm = 0;
                for (int j = 0; j < p; j++) { beta[j] += delta[j]; norm += delta[j] * delta[j]; }
                if (Math.Sqrt(norm) < 1e-7) break;
            }

            return X.Select(xi => Sigmoid(Dot(xi, beta))).ToArray();
        }

        // ── Gamma GLM, log link (IRLS) ────────────────────────────────────
        private static double[] GammaGlmFitted(double[][] X, double[] y)
        {
            int n = X.Length, p = X[0].Length;
            // 초기값: OLS on log(y)
            double[] logy = y.Select(v => Math.Log(Math.Max(v, 1e-9))).ToArray();
            double[] beta = OlsBeta(X, logy);

            for (int iter = 0; iter < 50; iter++)
            {
                double[] mu = X.Select(xi => Math.Exp(Dot(xi, beta))).ToArray();
                // Working response: z_i = log(mu_i) + y_i/mu_i - 1
                double[] z = new double[n];
                for (int i = 0; i < n; i++)
                    z[i] = Math.Log(Math.Max(mu[i], 1e-9)) + y[i] / mu[i] - 1;

                double[] betaNew = OlsBeta(X, z);
                double norm = 0;
                for (int j = 0; j < p; j++) norm += (betaNew[j] - beta[j]) * (betaNew[j] - beta[j]);
                beta = betaNew;
                if (Math.Sqrt(norm) < 1e-7) break;
            }
            return beta;
        }

        private static double[] ComputeMu(double[][] X, double[] beta)
            => X.Select(xi => Math.Exp(Dot(xi, beta))).ToArray();

        private static double ComputePhi(double[][] Xpos, double[] ypos, double[] beta)
        {
            int n = Xpos.Length, p = beta.Length;
            double ss = 0;
            for (int i = 0; i < n; i++)
            {
                double mu = Math.Exp(Dot(Xpos[i], beta));
                double r  = (ypos[i] - mu) / mu;
                ss += r * r;
            }
            int df = Math.Max(n - p, 1);
            return Math.Max(ss / df, 1e-6);
        }

        // ── OLS ────────────────────────────────────────────────────────────
        private static double[] OlsBeta(double[][] X, double[] y)
        {
            int n = X.Length, p = X[0].Length;
            double[][] XtX = new double[p][];
            for (int j = 0; j < p; j++) XtX[j] = new double[p];
            double[] Xty = new double[p];

            for (int i = 0; i < n; i++)
                for (int j = 0; j < p; j++)
                {
                    Xty[j] += X[i][j] * y[i];
                    for (int k = 0; k < p; k++)
                        XtX[j][k] += X[i][j] * X[i][k];
                }
            return SolveLinear(XtX, Xty);
        }

        // ── 선형시스템 (Gaussian elimination with partial pivot) ────────────
        private static double[] SolveLinear(double[][] A, double[] b)
        {
            int n = A.Length;
            // 복사
            double[][] M = new double[n][];
            double[] rhs = (double[])b.Clone();
            for (int i = 0; i < n; i++) M[i] = (double[])A[i].Clone();

            // 전진소거
            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                for (int row = col + 1; row < n; row++)
                    if (Math.Abs(M[row][col]) > Math.Abs(M[pivot][col])) pivot = row;

                (M[col], M[pivot]) = (M[pivot], M[col]);
                (rhs[col], rhs[pivot]) = (rhs[pivot], rhs[col]);

                if (Math.Abs(M[col][col]) < 1e-15) continue;
                double inv = 1.0 / M[col][col];
                for (int row = col + 1; row < n; row++)
                {
                    double factor = M[row][col] * inv;
                    rhs[row] -= factor * rhs[col];
                    for (int k = col; k < n; k++) M[row][k] -= factor * M[col][k];
                }
            }
            // 후진대입
            double[] x = new double[n];
            for (int i = n - 1; i >= 0; i--)
            {
                double s = rhs[i];
                for (int j = i + 1; j < n; j++) s -= M[i][j] * x[j];
                x[i] = Math.Abs(M[i][i]) < 1e-15 ? 0 : s / M[i][i];
            }
            return x;
        }

        // ── 수학 유틸 ────────────────────────────────────────────────────────
        private static double Sigmoid(double x)
            => x > 500 ? 1 : x < -500 ? 0 : 1.0 / (1 + Math.Exp(-x));

        private static double Dot(double[] a, double[] b)
        {
            double s = 0;
            for (int i = 0; i < a.Length; i++) s += a[i] * b[i];
            return s;
        }

        private static double SampleGamma(Random rng, double shape, double rate)
        {
            // MathNet.Numerics Gamma.Sample(rng, shape, rate) — rate = 1/scale
            double safeRate  = Math.Max(rate,  1e-15);
            double safeShape = Math.Max(shape, 1e-6);
            return Gamma.Sample(rng, safeShape, safeRate);
        }
    }
}