"""
NCI 일상섭취량 추정 엔진 — C# UsualIntakeCalculator.cs Python 포팅
 - NCI  : Logistic GLM + Gamma GLM (IRLS) + Monte Carlo simulation
 - ISU  : BLUP (between/within variance decomposition)
 - MSM  : Two-part BLUP for episodic foods
"""
import math, numpy as np
from dataclasses import dataclass, field
from typing import List, Tuple, Optional

WEEK        = 52
MASTER_SEED = 20180412


# ── Data classes ────────────────────────────────────────────────────────────
@dataclass
class PersonRecord:
    id: str
    sex: int          # 1=남, 2=여
    age: int
    age_g: int        # 1-8
    age_g_desc: str
    day: int
    region: int
    wt_ntr: float
    nf_intk: float
    ffq: float = 0.0
    ho_incm: int = 1
    town_t: str = ""
    edu: str = ""
    genertn_type: str = ""
    region_type: str = ""

@dataclass
class PersonIntake:
    id: str
    sex: int
    age_g_desc: str
    intake: float
    raw_intk: float
    weight: float

@dataclass
class QuantileRow:
    sex: str = "ALL"
    age_g_desc: str = "ALL"
    n: int = 0
    average: float = 0.0
    sd: float = 0.0
    p1st: float = 0.0
    p5th: float = 0.0
    p25th: float = 0.0
    median: float = 0.0
    p75th: float = 0.0
    p90th: float = 0.0
    p95th: float = 0.0
    p975th: float = 0.0
    p99th: float = 0.0
    min_val: float = 0.0
    max_val: float = 0.0

@dataclass
class AnalysisResult:
    rho_p: float = 0.0
    rho_a: float = 0.0
    papa: float = 0.0
    zero_prevalence: float = 0.0
    sigma_b2: float = 0.0
    sigma_w2: float = 0.0
    reliability: float = 0.0
    log_transformed: bool = False
    gamma_shape: float = 0.0
    gamma_scale: float = 0.0
    method_used: str = "NCI"
    method_note: str = ""
    person_intakes: List[PersonIntake] = field(default_factory=list)
    result_table: List[QuantileRow] = field(default_factory=list)
    additional_result: Optional['AnalysisResult'] = None


# ── Public entry point ───────────────────────────────────────────────────────
def compute(x0_raw: List[dict], x1_raw: List[dict],
            selected_fcodes: set, sim_time: int = 5,
            progress=None) -> AnalysisResult:
    def rep(msg):
        if progress: progress(msg)

    rep("데이터 필터링 및 집계 중...")
    x0 = _aggregate_x0(x0_raw, selected_fcodes)
    x1 = _aggregate_x1(x1_raw, selected_fcodes)
    if not x0 or not x1:
        raise ValueError("선택한 식품코드에 해당하는 데이터가 없습니다.")

    rep("rho 추정 중 (x0 상관계수)...")
    papa, rho_p, rho_a = _estimate_rho(x0)

    zero_prev = sum(1 for r in x1 if r.nf_intk == 0) / len(x1) * 100 if x1 else 0.0

    rep(f"[NCI] rhoP={rho_p:.4f}, rhoA={rho_a:.4f}, papa={papa:.1f}% · 시뮬레이션 시작...")
    nci_intakes = _run_simulation(x1, rho_p, rho_a, sim_time, rep)

    rep("결과 집계 중...")
    result = _build_result(x1, nci_intakes, rho_p, rho_a, papa)
    result.zero_prevalence = zero_prev
    result.method_used = "NCI"
    result.method_note  = (f"NCI — rhoP={rho_p:.4f}, rhoA={rho_a:.4f}, "
                           f"papa={papa:.1f}%, 0섭취율={zero_prev:.1f}%")

    is_regular  = papa <= 5.0 and zero_prev <= 15.0
    is_episodic = papa >  5.0 or  zero_prev >  15.0

    try:
        if is_regular:
            rep(f"[ISU] 보완 분석 중 (일상적 섭취 패턴)...")
            intakes, sb2, sw2, rel, log_t = _run_isu(x0, x1, rep)
            add = _build_result(x1, intakes, rho_p, rho_a, papa)
            add.zero_prevalence = zero_prev
            add.method_used    = "ISU"
            add.sigma_b2       = sb2
            add.sigma_w2       = sw2
            add.reliability    = rel
            add.log_transformed= log_t
            add.method_note    = (f"ISU 보완 (일상적 섭취)\n"
                                  f"  σ_b²={sb2:.4f}, σ_w²={sw2:.4f}, λ={rel:.3f}"
                                  + (" [로그변환]" if log_t else " [원척도]"))
            result.additional_result = add
        elif is_episodic:
            rep(f"[MSM] 보완 분석 중 (간헐적 섭취 패턴)...")
            intakes, sb2, sw2, rel = _run_msm(x0, x1, rep)
            add = _build_result(x1, intakes, rho_p, rho_a, papa)
            add.zero_prevalence = zero_prev
            add.method_used    = "MSM"
            add.sigma_b2       = sb2
            add.sigma_w2       = sw2
            add.reliability    = rel
            add.log_transformed= True
            add.method_note    = (f"MSM 보완 (간헐적 섭취)\n"
                                  f"  σ_b²={sb2:.4f}, σ_w²={sw2:.4f}, λ={rel:.3f} [로그정규]")
            result.additional_result = add
    except Exception as e:
        result.method_note += f"\n⚠ 보완 분석 실패: {e}"

    return result


# ── 1. 데이터 집계 ────────────────────────────────────────────────────────────
def _aggregate_x0(raw: List[dict], codes: set) -> List[PersonRecord]:
    # 골격: (id, day) → PersonRecord (nf_intk=0)
    skeleton: dict = {}
    for r in raw:
        key = (r['id'], r['day'])
        if key not in skeleton:
            skeleton[key] = PersonRecord(
                id=r['id'], sex=r.get('sex',1), age=r.get('age',0),
                age_g=r.get('age_g',1), age_g_desc=r.get('age_g_desc',''),
                day=r['day'], region=r.get('region',0), wt_ntr=r.get('wt_ntr',1.0),
                nf_intk=0.0, ffq=r.get('ffq',0.0), ho_incm=r.get('ho_incm',1),
                town_t=r.get('town_t',''), edu=r.get('edu',''),
                genertn_type=r.get('genertn_type',''), region_type=r.get('region_type','')
            )
    # 선택 코드 합산
    for r in raw:
        if not codes or r.get('fcode','') in codes:
            key = (r['id'], r['day'])
            if key in skeleton:
                skeleton[key].nf_intk += r.get('nf_intk', 0.0)
    return sorted(skeleton.values(), key=lambda x: (x.id, x.day))


def _aggregate_x1(raw: List[dict], codes: set) -> List[PersonRecord]:
    by_id: dict = {}
    for r in raw:
        if codes and r.get('fcode','') not in codes:
            continue
        pid = r['id']
        if pid not in by_id:
            by_id[pid] = PersonRecord(
                id=pid, sex=r.get('sex',1), age=r.get('age',0),
                age_g=r.get('age_g',1), age_g_desc=r.get('age_g_desc',''),
                day=1, region=r.get('region',0), wt_ntr=r.get('wt_ntr',1.0),
                nf_intk=0.0, ffq=r.get('ffq',0.0), ho_incm=r.get('ho_incm',1),
                town_t=r.get('town_t',''), edu=r.get('edu',''),
                genertn_type=r.get('genertn_type',''), region_type=r.get('region_type','')
            )
        by_id[pid].nf_intk += r.get('nf_intk', 0.0)
    return sorted(by_id.values(), key=lambda x: x.id)


# ── 2. rhoP/rhoA/papa 추정 ───────────────────────────────────────────────────
def _estimate_rho(x0: List[PersonRecord]) -> Tuple[float, float, float]:
    by_id: dict = {}
    for r in x0:
        by_id.setdefault(r.id, []).append(r)
    pairs = [(sorted(v, key=lambda x: x.day)[0].nf_intk,
              sorted(v, key=lambda x: x.day)[1].nf_intk)
             for v in by_id.values() if len(v) >= 2]
    if not pairs:
        return 0.0, 0.5, 0.5

    a1 = np.array([p[0] for p in pairs])
    a2 = np.array([p[1] for p in pairs])

    class_one = int(np.sum((a1 == 0) != (a2 == 0)))
    papa = class_one / len(pairs) * 100

    ep1 = (a1 > 0).astype(float)
    ep2 = (a2 > 0).astype(float)
    rho_p_raw = float(np.corrcoef(ep1, ep2)[0, 1]) if np.std(ep1) > 1e-15 and np.std(ep2) > 1e-15 else 0.0
    rho_p = 0.95 if papa < 5 else (0.5 if math.isnan(rho_p_raw) else rho_p_raw)

    eaters = [(p[0], p[1]) for p in pairs if p[0] > 0 and p[1] > 0]
    if len(eaters) >= 3:
        ea1 = np.array([e[0] for e in eaters])
        ea2 = np.array([e[1] for e in eaters])
        rho_a_raw = float(np.corrcoef(ea1, ea2)[0, 1]) if np.std(ea1) > 1e-15 and np.std(ea2) > 1e-15 else 0.0
        rho_a = 0.5 if math.isnan(rho_a_raw) else rho_a_raw
    else:
        rho_a = 0.0

    return papa, rho_p, rho_a


# ── 3-A. ISU BLUP ─────────────────────────────────────────────────────────────
def _run_isu(x0, x1, rep) -> Tuple[np.ndarray, float, float, float, bool]:
    by_id: dict = {}
    for r in x0:
        by_id.setdefault(r.id, []).append(r)
    pairs = []
    for v in by_id.values():
        if len(v) >= 2:
            arr = sorted(v, key=lambda x: x.day)
            pairs.append((arr[0].nf_intk, arr[1].nf_intk, arr[0].wt_ntr))
    if len(pairs) < 5:
        raise ValueError("ISU 추정을 위한 2일 반복자료 쌍이 부족합니다 (최소 5쌍).")

    use_log = all(p[0] > 0 and p[1] > 0 for p in pairs) and all(r.nf_intk > 0 for r in x1)
    T    = (lambda v: math.log(max(v, 1e-9))) if use_log else (lambda v: v)
    Tinv = (lambda v: math.exp(v)) if use_log else (lambda v: v)

    sw = sum(p[2] for p in pairs)
    sigma_w2 = sum(p[2] * (T(p[0]) - T(p[1]))**2 / 2 for p in pairs) / sw
    person_means = [(( T(p[0]) + T(p[1])) / 2, p[2]) for p in pairs]
    grand_mean = sum(m * w for m, w in person_means) / sw
    sigma_total2 = sum(w * (m - grand_mean)**2 for m, w in person_means) / sw
    sigma_b2 = max(0.0, sigma_total2 - sigma_w2 / 2)
    rel = sigma_b2 / (sigma_b2 + sigma_w2) if (sigma_b2 + sigma_w2) > 1e-15 else 0.5

    sw_x1 = sum(r.wt_ntr for r in x1)
    mu_x1 = sum(r.wt_ntr * T(max(r.nf_intk, 1e-9)) for r in x1) / sw_x1 if sw_x1 > 1e-15 else 0.0

    intakes = np.array([max(0.0, Tinv(mu_x1 + rel * (T(max(r.nf_intk, 1e-9)) - mu_x1))) for r in x1])
    return intakes, sigma_b2, sigma_w2, rel, use_log


# ── 3-B. MSM ──────────────────────────────────────────────────────────────────
def _run_msm(x0, x1, rep) -> Tuple[np.ndarray, float, float, float]:
    p_hat = sum(1 for r in x1 if r.nf_intk > 0) / len(x1) if x1 else 0.0
    if p_hat < 1e-9:
        raise ValueError("MSM: 섭취 기록이 전혀 없습니다 (p̂=0).")

    pos_x1 = [r for r in x1 if r.nf_intk > 0]
    if len(pos_x1) < 5:
        raise ValueError("MSM: 섭취일 기록이 부족합니다 (최소 5건).")

    sw_pos = sum(r.wt_ntr for r in pos_x1)
    if sw_pos < 1e-15: sw_pos = len(pos_x1)
    mu_log = sum(r.wt_ntr * math.log(r.nf_intk) for r in pos_x1) / sw_pos
    sigma_total2 = sum(r.wt_ntr * (math.log(r.nf_intk) - mu_log)**2 for r in pos_x1) / sw_pos

    by_id: dict = {}
    for r in x0:
        by_id.setdefault(r.id, []).append(r)
    cp = []
    for v in by_id.values():
        if len(v) >= 2:
            arr = sorted(v, key=lambda x: x.day)
            d1, d2, w = arr[0].nf_intk, arr[1].nf_intk, arr[0].wt_ntr
            if d1 > 0 and d2 > 0:
                cp.append((d1, d2, w))

    if len(cp) >= 3:
        sw2 = sum(p[2] for p in cp)
        if sw2 < 1e-15: sw2 = len(cp)
        sigma_w2 = sum(p[2] * (math.log(p[0]) - math.log(p[1]))**2 / 2 for p in cp) / sw2
        sigma_b2 = max(0.0, sigma_total2 - sigma_w2 / 2)
        rel = sigma_b2 / (sigma_b2 + sigma_w2) if (sigma_b2 + sigma_w2) > 1e-15 else 0.5
    else:
        sigma_w2 = sigma_total2 * 0.5
        sigma_b2 = sigma_total2 * 0.5
        rel = 0.5

    pop_amount = math.exp(mu_log + sigma_w2 / 2)

    intakes = []
    for r in x1:
        if r.nf_intk > 0:
            log_blup = mu_log + rel * (math.log(r.nf_intk) - mu_log)
            amount   = math.exp(log_blup + (1 - rel) * sigma_w2 / 2)
            intakes.append(p_hat * amount)
        else:
            intakes.append(p_hat * pop_amount)
    return np.array(intakes), sigma_b2, sigma_w2, rel


# ── 3-C. NCI Simulation ───────────────────────────────────────────────────────
def _run_simulation(x1: List[PersonRecord], rho_p: float, rho_a: float,
                    sim_time: int, rep) -> np.ndarray:
    n    = len(x1)
    eat_a = np.array([r.nf_intk for r in x1])
    eat_p = (eat_a > 0).astype(float)
    X    = _build_design_matrix(x1)

    acc = np.zeros(n)
    rng_master = np.random.RandomState(MASTER_SEED)
    seeds = rng_master.randint(1, 100001, size=sim_time)

    for it in range(sim_time):
        rep(f"시뮬레이션 {it+1}/{sim_time} 반복 중...")
        rng = np.random.RandomState(int(seeds[it]))

        prob    = _logistic_fitted(X, eat_p)
        pos_idx = np.where(eat_a > 0)[0]
        Xpos    = X[pos_idx]
        ypos    = eat_a[pos_idx]

        beta_g  = _gamma_glm_fitted(Xpos, ypos)
        all_mu  = np.exp(X @ beta_g)
        phi     = _compute_phi(Xpos, ypos, beta_g)
        alpha   = max(1.0 / phi, 1e-6)

        sq_rho  = math.copysign(math.sqrt(abs(rho_a)), rho_a)

        I_prob   = np.where(eat_p == 1,
                            prob + (1 - prob) * rho_p,
                            prob * (1 - rho_p))
        beta_arr = alpha / np.maximum(all_mu, 1e-9)
        I_amount = np.where(eat_p == 1,
                            eat_a,
                            rng.gamma(shape=alpha, scale=1.0 / np.maximum(beta_arr, 1e-9)))

        intake = np.zeros(n)
        for j in range(n):
            bj   = alpha / max(all_mu[j], 1e-9)
            b1   = bj / (1 + sq_rho)
            a1   = alpha * (1 - sq_rho) / (1 + sq_rho)
            a1   = max(a1, 1e-9)
            b1   = max(b1, 1e-9)
            s    = 0.0
            ip   = float(I_prob[j])
            iamt = float(I_amount[j])
            for _ in range(WEEK - 1):
                ate  = 1 if rng.random() < ip else 0
                z    = rng.gamma(shape=a1, scale=1.0 / b1)
                food = sq_rho * iamt + z
                if food < 0: food = 0.0
                s   += food * ate
            intake[j] = s / (WEEK - 1)

        acc += np.where(np.isnan(intake), 0, intake)

    return acc / sim_time


# ── 4. 결과 빌드 ──────────────────────────────────────────────────────────────
def _build_result(x1, intakes, rho_p, rho_a, papa) -> AnalysisResult:
    person_intakes = [
        PersonIntake(id=x1[i].id, sex=x1[i].sex, age_g_desc=x1[i].age_g_desc,
                     intake=float(intakes[i]), raw_intk=x1[i].nf_intk,
                     weight=x1[i].wt_ntr)
        for i in range(len(x1))
    ]
    rows = [_calc_row("ALL", "ALL", intakes)]
    from itertools import groupby
    key_fn = lambda p: (p.sex, p.age_g_desc)
    for (sex, agd), grp in groupby(sorted(person_intakes, key=key_fn), key=key_fn):
        vals = np.array([p.intake for p in grp])
        rows.append(_calc_row("남자" if sex == 1 else "여자", agd, vals))

    shape, scale = _fit_weighted_gamma(person_intakes)
    r = AnalysisResult(rho_p=rho_p, rho_a=rho_a, papa=papa,
                       gamma_shape=shape, gamma_scale=scale,
                       person_intakes=person_intakes, result_table=rows)
    return r


def _calc_row(sex: str, age_g_desc: str, vals: np.ndarray) -> QuantileRow:
    if len(vals) == 0:
        return QuantileRow(sex=sex, age_g_desc=age_g_desc)
    v = np.sort(vals)
    def q(p): return float(np.percentile(v, p * 100))
    return QuantileRow(
        sex=sex, age_g_desc=age_g_desc, n=len(v),
        average=float(v.mean()), sd=float(v.std(ddof=1)) if len(v) > 1 else 0.0,
        p1st=q(.01), p5th=q(.05), p25th=q(.25), median=q(.50),
        p75th=q(.75), p90th=q(.90), p95th=q(.95),
        p975th=q(.975), p99th=q(.99),
        min_val=float(v[0]), max_val=float(v[-1])
    )


def _fit_weighted_gamma(persons: List[PersonIntake]) -> Tuple[float, float]:
    pos = [p for p in persons if p.intake > 0]
    if len(pos) < 3: return 1.0, 1.0
    sw   = sum(p.weight for p in pos)
    swx  = sum(p.weight * p.intake for p in pos)
    swxx = sum(p.weight * p.intake * p.intake for p in pos)
    if sw <= 0: return 1.0, 1.0
    a    = swx / sw
    b    = swxx / sw
    varw = b - a * a
    if varw <= 0: return 1.0, 1.0
    shape = (a * a) / varw
    scale = a / shape
    return shape, scale


# ── 통계 / 선형대수 ───────────────────────────────────────────────────────────
def _build_design_matrix(x1: List[PersonRecord]) -> np.ndarray:
    """intercept + ageG(7 dummies) + hoIncm(3 dummies) = 11 cols"""
    n, p = len(x1), 11
    X = np.zeros((n, p))
    X[:, 0] = 1.0
    for i, r in enumerate(x1):
        if 2 <= r.age_g <= 8:
            X[i, r.age_g - 1] = 1.0
        if 2 <= r.ho_incm <= 4:
            X[i, 7 + r.ho_incm - 1] = 1.0
    return X


def _logistic_fitted(X: np.ndarray, y: np.ndarray) -> np.ndarray:
    def sigmoid(z): return np.where(z > 500, 1.0, np.where(z < -500, 0.0, 1.0 / (1 + np.exp(-z))))
    n, p = X.shape
    beta = np.zeros(p)
    for _ in range(50):
        prob = sigmoid(X @ beta)
        W    = prob * (1 - prob)
        grad = X.T @ (y - prob)
        H    = (X.T * W) @ X
        try:
            delta = np.linalg.solve(H + np.eye(p) * 1e-10, grad)
        except np.linalg.LinAlgError:
            break
        beta += delta
        if np.linalg.norm(delta) < 1e-7: break
    return sigmoid(X @ beta)


def _gamma_glm_fitted(X: np.ndarray, y: np.ndarray) -> np.ndarray:
    """Gamma GLM with log link (IRLS)"""
    n, p = X.shape
    logy = np.log(np.maximum(y, 1e-9))
    try:
        beta = np.linalg.lstsq(X, logy, rcond=None)[0]
    except Exception:
        beta = np.zeros(p)
    for _ in range(50):
        mu   = np.exp(np.clip(X @ beta, -30, 30))
        z    = np.log(np.maximum(mu, 1e-9)) + y / mu - 1
        try:
            beta_new = np.linalg.lstsq(X, z, rcond=None)[0]
        except Exception:
            break
        if np.linalg.norm(beta_new - beta) < 1e-7: break
        beta = beta_new
    return beta


def _compute_phi(Xpos, ypos, beta):
    mu = np.exp(np.clip(Xpos @ beta, -30, 30))
    r  = (ypos - mu) / np.maximum(mu, 1e-9)
    df = max(len(ypos) - len(beta), 1)
    return max(float(np.sum(r * r) / df), 1e-6)
