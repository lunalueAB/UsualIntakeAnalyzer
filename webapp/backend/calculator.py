"""
Usual Intake Calculator — Python port of C# UsualIntakeCalculator.
Implements NCI (primary) + ISU or MSM (supplementary) methods.
"""
from __future__ import annotations
import math
import random
from dataclasses import dataclass, field
from typing import Dict, List, Optional, Set, Tuple

import numpy as np

from models import AnalysisResult, QuantileRow, SurveyRecord

# ── Constants ────────────────────────────────────────────
MASTER_SEED = 20180412
WEEK = 52          # simulation period = WEEK-1 = 51 days
MAX_ITER = 50
CONV_TOL = 1e-7
EPS = 1e-9
WEPS = 1e-15


# ── Helpers ──────────────────────────────────────────────
def _sigmoid(x: float) -> float:
    if x > 500:
        return 1.0
    if x < -500:
        return 0.0
    return 1.0 / (1.0 + math.exp(-x))


def _pearson(x: List[float], y: List[float]) -> float:
    n = len(x)
    if n < 2:
        return 0.0
    mx, my = sum(x) / n, sum(y) / n
    num = sum((xi - mx) * (yi - my) for xi, yi in zip(x, y))
    dx = math.sqrt(sum((xi - mx) ** 2 for xi in x))
    dy = math.sqrt(sum((yi - my) ** 2 for yi in y))
    denom = dx * dy
    return num / denom if denom > 1e-15 else 0.0


def _quantile(sorted_vals: List[float], p: float) -> float:
    """Linear-interpolation quantile (matches R type=7 / numpy default)."""
    n = len(sorted_vals)
    if n == 0:
        return 0.0
    pos = p * (n - 1)
    lo = int(pos)
    hi = min(lo + 1, n - 1)
    frac = pos - lo
    return sorted_vals[lo] * (1 - frac) + sorted_vals[hi] * frac


def _weighted_mean(vals: List[float], weights: List[float]) -> float:
    sw = sum(weights)
    if sw < WEPS:
        return 0.0
    return sum(v * w for v, w in zip(vals, weights)) / sw


def _weighted_var(vals: List[float], weights: List[float], mean: float) -> float:
    sw = sum(weights)
    if sw < WEPS:
        return 0.0
    return sum(w * (v - mean) ** 2 for v, w in zip(vals, weights)) / sw


def _stddev(vals: List[float]) -> float:
    n = len(vals)
    if n < 2:
        return 0.0
    m = sum(vals) / n
    return math.sqrt(sum((v - m) ** 2 for v in vals) / (n - 1))


def _solve_linear(A: List[List[float]], b: List[float]) -> Optional[List[float]]:
    """Gaussian elimination with partial pivoting."""
    n = len(b)
    mat = [list(row) + [bi] for row, bi in zip(A, b)]
    for col in range(n):
        # pivot
        max_row = max(range(col, n), key=lambda r: abs(mat[r][col]))
        mat[col], mat[max_row] = mat[max_row], mat[col]
        piv = mat[col][col]
        if abs(piv) < 1e-14:
            return None
        for row in range(n):
            if row != col:
                factor = mat[row][col] / piv
                for k in range(col, n + 1):
                    mat[row][k] -= factor * mat[col][k]
        mat[col] = [v / piv for v in mat[col]]
    return [mat[i][n] for i in range(n)]


def _ols_beta(X: List[List[float]], y: List[float]) -> Optional[List[float]]:
    """OLS via normal equations X^T X β = X^T y."""
    n, p = len(X), len(X[0])
    XtX = [[sum(X[k][i] * X[k][j] for k in range(n)) for j in range(p)] for i in range(p)]
    Xty = [sum(X[k][i] * y[k] for k in range(n)) for i in range(p)]
    return _solve_linear(XtX, Xty)


def _design_row(age_g: int, ho_incm: int) -> List[float]:
    """11-column design row: intercept + 7 AgeG dummies + 3 HoIncm dummies."""
    row = [1.0]
    for g in range(2, 9):
        row.append(1.0 if age_g == g else 0.0)
    for h in range(2, 5):
        row.append(1.0 if ho_incm == h else 0.0)
    return row


def _sample_gamma(rng: random.Random, shape: float, rate: float) -> float:
    """Sample from Gamma(shape, rate) using numpy (seeded via rng)."""
    # Match C# MathNet Gamma.Sample(rng, shape, rate): rate = 1/scale
    scale = 1.0 / max(rate, EPS)
    shape = max(shape, EPS)
    # Use numpy's gamma — seed with a value from our rng
    seed_val = rng.randint(0, 2**31 - 1)
    rng_np = np.random.default_rng(seed_val)
    return float(rng_np.standard_gamma(shape) * scale)


# ── Person aggregation ───────────────────────────────────
@dataclass
class PersonDay:
    id: str
    sex: int
    age_g: int
    age_g_desc: str
    wt_ntr: float
    ho_incm: int
    day: int
    intake: float   # summed NfIntk for selected food codes


@dataclass
class PersonX1:
    id: str
    sex: int
    age_g: int
    age_g_desc: str
    wt_ntr: float
    ho_incm: int
    intake: float   # x1 observed intake (may be 0)


def _aggregate_x0(
    records: List[SurveyRecord], food_codes: Set[str]
) -> List[PersonDay]:
    """
    Build skeleton of all (Id, Day) pairs from x0, then fill intakes.
    Critical: preserves zero-consumption days (fixes papa/rhoP bias).
    """
    # Step 1: collect all (id, day) skeletons
    skeleton: Dict[Tuple[str, int], PersonDay] = {}
    for r in records:
        key = (r.id, r.day)
        if key not in skeleton:
            skeleton[key] = PersonDay(
                id=r.id, sex=r.sex, age_g=r.age_g,
                age_g_desc=r.age_g_desc, wt_ntr=max(r.wt_ntr, WEPS),
                ho_incm=max(1, min(4, r.ho_incm or 1)),
                day=r.day, intake=0.0,
            )
    # Step 2: sum intakes for selected food codes
    for r in records:
        key = (r.id, r.day)
        if key in skeleton and r.f_code in food_codes:
            skeleton[key].intake += max(r.nf_intk, 0.0)
    return list(skeleton.values())


def _aggregate_x1(
    records: List[SurveyRecord], food_codes: Set[str]
) -> List[PersonX1]:
    """Sum x1 intakes per person for selected food codes."""
    intake_map: Dict[str, float] = {}
    meta: Dict[str, PersonX1] = {}
    for r in records:
        if r.id not in meta:
            meta[r.id] = PersonX1(
                id=r.id, sex=r.sex, age_g=r.age_g,
                age_g_desc=r.age_g_desc, wt_ntr=max(r.wt_ntr, WEPS),
                ho_incm=max(1, min(4, r.ho_incm or 1)),
                intake=0.0,
            )
        if r.f_code in food_codes:
            meta[r.id].intake += max(r.nf_intk, 0.0)
    return list(meta.values())


# ── Correlations ─────────────────────────────────────────
def _estimate_correlations(
    x0_days: List[PersonDay],
) -> Tuple[float, float, float]:
    """Returns (rho_p, rho_a, papa)."""
    # Build pairs per person
    by_person: Dict[str, List[PersonDay]] = {}
    for pd_ in x0_days:
        by_person.setdefault(pd_.id, []).append(pd_)

    pairs = [(v[0], v[1]) for v in by_person.values() if len(v) == 2]
    if not pairs:
        return 0.95, 0.0, 0.0

    eat1 = [1.0 if p[0].intake > 0 else 0.0 for p in pairs]
    eat2 = [1.0 if p[1].intake > 0 else 0.0 for p in pairs]

    # papa: proportion eating on exactly one day
    papa = sum(1 for a, b in zip(eat1, eat2) if (a > 0) != (b > 0)) / len(pairs)

    # rho_p: Pearson of binary eat vectors
    rho_p = _pearson(eat1, eat2)
    if papa < 0.05:
        rho_p = 0.95

    # rho_a: Pearson of amounts among both-day eaters
    both = [(p[0].intake, p[1].intake) for p in pairs
            if p[0].intake > 0 and p[1].intake > 0]
    if len(both) >= 3:
        rho_a = _pearson([b[0] for b in both], [b[1] for b in both])
    else:
        rho_a = 0.0

    return rho_p, rho_a, papa


# ── Logistic Regression (Newton-Raphson) ─────────────────
def _logistic_nr(
    X: List[List[float]], y: List[float], w: List[float]
) -> Optional[List[float]]:
    p = len(X[0])
    beta = [0.0] * p
    for _ in range(MAX_ITER):
        prob = [_sigmoid(sum(X[i][j] * beta[j] for j in range(p))) for i in range(len(X))]
        # gradient: X^T W (y - prob)
        resid = [w[i] * (y[i] - prob[i]) for i in range(len(X))]
        grad = [sum(X[i][j] * resid[i] for i in range(len(X))) for j in range(p)]
        # Hessian: -X^T W diag(prob*(1-prob)) X
        H = [[0.0] * p for _ in range(p)]
        for i in range(len(X)):
            wpp = w[i] * prob[i] * (1 - prob[i])
            for a in range(p):
                for b in range(p):
                    H[a][b] -= wpp * X[i][a] * X[i][b]
        # delta = -H^{-1} grad
        neg_H = [[-H[a][b] for b in range(p)] for a in range(p)]
        delta = _solve_linear(neg_H, grad)
        if delta is None:
            break
        beta = [beta[j] + delta[j] for j in range(p)]
        if math.sqrt(sum(d ** 2 for d in delta)) < CONV_TOL:
            break
    return beta


# ── Gamma GLM (IRLS, log link) ───────────────────────────
def _gamma_glm_irls(
    X: List[List[float]], y: List[float], w: List[float]
) -> Tuple[Optional[List[float]], float]:
    """Returns (beta, phi) where phi = dispersion."""
    p = len(X[0])
    n = len(y)
    # OLS init on log(y)
    log_y = [math.log(max(yi, EPS)) for yi in y]
    beta = _ols_beta(X, log_y) or [0.0] * p
    phi = 1.0

    for _ in range(MAX_ITER):
        mu = [max(math.exp(sum(X[i][j] * beta[j] for j in range(p))), EPS)
              for i in range(n)]
        # working response and weights
        z = [math.log(mu[i]) + (y[i] - mu[i]) / mu[i] for i in range(n)]
        wt = [w[i] * mu[i] ** 2 for i in range(n)]  # Gamma variance ~ mu^2

        # Weighted normal equations
        XtWX = [[sum(wt[k] * X[k][a] * X[k][b] for k in range(n))
                 for b in range(p)] for a in range(p)]
        XtWz = [sum(wt[k] * X[k][a] * z[k] for k in range(n)) for a in range(p)]
        new_beta = _solve_linear(XtWX, XtWz)
        if new_beta is None:
            break
        delta = [new_beta[j] - beta[j] for j in range(p)]
        beta = new_beta
        if math.sqrt(sum(d ** 2 for d in delta)) < CONV_TOL:
            break

    # Estimate dispersion phi via Pearson chi-sq
    mu = [max(math.exp(sum(X[i][j] * beta[j] for j in range(p))), EPS) for i in range(n)]
    chi2 = sum(w[i] * ((y[i] - mu[i]) / mu[i]) ** 2 for i in range(n))
    df = max(n - p, 1)
    phi = chi2 / df
    return beta, max(phi, EPS)


# ── Quantile table ────────────────────────────────────────
_SEX_LABEL = {1: "남자", 2: "여자"}

def _make_quantile_table(
    persons: List[Tuple[str, int, str, float, float]]  # (id, sex, age_g_desc, wt, intake)
) -> Tuple[List[QuantileRow], float, float, float, float, float]:
    """Build QuantileRow table and overall statistics."""
    rows: List[QuantileRow] = []

    def _row(label_sex: str, label_age: str, subset):
        if not subset:
            return None
        vals = sorted([s[4] for s in subset])
        weights = [s[3] for s in subset]
        wmean = _weighted_mean([s[4] for s in subset], weights)
        wvar = _weighted_var([s[4] for s in subset], weights, wmean)
        wsd = math.sqrt(max(wvar, 0.0))
        return QuantileRow(
            sex=label_sex, age_g_desc=label_age, n=len(subset),
            average=wmean, sd=wsd,
            p1=_quantile(vals, 0.01), p5=_quantile(vals, 0.05),
            p25=_quantile(vals, 0.25), median=_quantile(vals, 0.50),
            p75=_quantile(vals, 0.75), p90=_quantile(vals, 0.90),
            p95=_quantile(vals, 0.95), p975=_quantile(vals, 0.975),
            p99=_quantile(vals, 0.99),
            min_val=vals[0], max_val=vals[-1],
        )

    all_persons = persons
    all_vals = sorted([p[4] for p in all_persons])
    all_weights = [p[3] for p in all_persons]
    all_mean = _weighted_mean([p[4] for p in all_persons], all_weights)
    all_var  = _weighted_var([p[4] for p in all_persons], all_weights, all_mean)
    all_sd   = math.sqrt(max(all_var, 0.0))
    p95_total = _quantile(all_vals, 0.95)
    p99_total = _quantile(all_vals, 0.99)
    median_total = _quantile(all_vals, 0.50)

    # Overall row
    r = _row("전체", "전체", all_persons)
    if r:
        rows.append(r)

    for sex in (1, 2):
        sex_label = _SEX_LABEL.get(sex, str(sex))
        sex_sub = [p for p in all_persons if p[1] == sex]
        r = _row(sex_label, "전체", sex_sub)
        if r:
            rows.append(r)
        # age subgroups
        by_age: Dict[str, list] = {}
        for p in sex_sub:
            by_age.setdefault(p[2], []).append(p)
        for age_desc, age_sub in sorted(by_age.items()):
            r = _row(sex_label, age_desc, age_sub)
            if r:
                rows.append(r)

    return rows, all_mean, median_total, all_sd, p95_total, p99_total


# ── NCI Simulation ────────────────────────────────────────
def _run_nci(
    x1: List[PersonX1],
    rho_p: float,
    rho_a: float,
    sim_time: int,
) -> AnalysisResult:
    """Monte Carlo NCI simulation."""
    n = len(x1)
    if n == 0:
        return AnalysisResult(method_used="NCI", method_note="데이터 없음")

    # Design matrix and targets
    X = [_design_row(p.age_g, p.ho_incm) for p in x1]
    eat_p = [1.0 if p.intake > 0 else 0.0 for p in x1]
    eat_a = [p.intake for p in x1 if p.intake > 0]
    X_pos = [X[i] for i, p in enumerate(x1) if p.intake > 0]
    w_all = [p.wt_ntr for p in x1]
    w_pos = [x1[i].wt_ntr for i, p in enumerate(x1) if p.intake > 0]

    zero_prevalence = sum(1 for p in x1 if p.intake == 0) / n

    # Logistic regression
    beta_logit = _logistic_nr(X, eat_p, w_all)
    if beta_logit is None:
        beta_logit = [0.0] * len(X[0])

    # Gamma GLM on positive records
    if len(X_pos) < 3:
        beta_gamma = [0.0] * len(X[0])
        phi = 1.0
    else:
        beta_gamma, phi = _gamma_glm_irls(X_pos, eat_a, w_pos)
        if beta_gamma is None:
            beta_gamma = [0.0] * len(X[0])

    alpha = max(1.0 / phi, EPS)
    sq_rho = math.copysign(math.sqrt(abs(rho_a)), rho_a)

    # Adjusted per-person probability and initial amount
    p_logit = len(X[0])
    prob = [_sigmoid(sum(X[i][j] * beta_logit[j] for j in range(p_logit))) for i in range(n)]
    mu = [max(math.exp(sum(X[i][j] * beta_gamma[j] for j in range(p_logit))), EPS) for i in range(n)]

    i_prob = []
    i_amount = []
    master_rng = random.Random(MASTER_SEED)
    # Draw per-iteration seeds
    iter_seeds = [master_rng.randint(1, 100001) for _ in range(sim_time)]

    accumulated = [0.0] * n

    for seed in iter_seeds:
        rng = random.Random(seed)
        i_prob_iter = []
        i_amount_iter = []
        for i in range(n):
            if eat_p[i] > 0:
                ip = prob[i] + (1 - prob[i]) * rho_p
                ia = x1[i].intake
            else:
                ip = prob[i] * (1 - rho_p)
                ia = _sample_gamma(rng, alpha, alpha / mu[i])
            i_prob_iter.append(min(max(ip, 0.0), 1.0))
            i_amount_iter.append(max(ia, 0.0))

        # Simulate WEEK-1 days per person
        beta1 = max(alpha / mu[0], EPS) / (1 + sq_rho) if n > 0 else 1.0
        alpha1 = alpha * (1 - sq_rho) / (1 + sq_rho) if (1 + sq_rho) > EPS else alpha

        for i in range(n):
            b1_i = max(alpha / mu[i], EPS) / max(1 + sq_rho, EPS)
            a1_i = max(alpha * (1 - sq_rho) / max(1 + sq_rho, EPS), EPS)
            total = 0.0
            for _ in range(WEEK - 1):
                ate = 1 if rng.random() < i_prob_iter[i] else 0
                if ate:
                    z = _sample_gamma(rng, a1_i, b1_i)
                    food = sq_rho * i_amount_iter[i] + z
                    total += max(food, 0.0)
            accumulated[i] += total / (WEEK - 1)

    usual_intakes = [accumulated[i] / sim_time for i in range(n)]

    # Gamma fit (MOM, weighted) on positive usual intakes
    pos_ui = [(ui, x1[i].wt_ntr) for i, ui in enumerate(usual_intakes) if ui > 0]
    if pos_ui:
        ui_vals, ui_wts = zip(*pos_ui)
        wm = _weighted_mean(list(ui_vals), list(ui_wts))
        wv = _weighted_var(list(ui_vals), list(ui_wts), wm)
        gamma_shape = wm ** 2 / max(wv, EPS)
        gamma_scale = wm / max(gamma_shape, EPS)
    else:
        gamma_shape = gamma_scale = 0.0

    # Build result
    persons_for_table = [
        (x1[i].id, x1[i].sex, x1[i].age_g_desc, x1[i].wt_ntr, usual_intakes[i])
        for i in range(n)
    ]
    table, mean_t, med_t, sd_t, p95_t, p99_t = _make_quantile_table(persons_for_table)

    return AnalysisResult(
        rho_p=rho_p, rho_a=rho_a, papa=0.0,  # papa filled by caller
        zero_prevalence=zero_prevalence,
        gamma_shape=gamma_shape, gamma_scale=gamma_scale,
        method_used="NCI",
        result_table=table,
        n_total=n, mean_total=mean_t, median_total=med_t,
        sd_total=sd_t, p95_total=p95_t, p99_total=p99_t,
    )


# ── ISU BLUP ─────────────────────────────────────────────
def _run_isu(
    x0_days: List[PersonDay],
    x1: List[PersonX1],
) -> Optional[AnalysisResult]:
    """ISU (Iowa State) BLUP — for everyday foods."""
    by_person: Dict[str, List[PersonDay]] = {}
    for pd_ in x0_days:
        by_person.setdefault(pd_.id, []).append(pd_)

    pairs = [(v[0], v[1]) for v in by_person.values() if len(v) == 2]
    if len(pairs) < 5:
        return None

    # Decide log transform
    all_pos = all(p[0].intake > 0 and p[1].intake > 0 for p in pairs)
    x1_pos = all(p.intake > 0 for p in x1)
    log_transform = all_pos and x1_pos

    def T(v):
        return math.log(v) if log_transform else v

    def Tinv(v):
        return math.exp(v) if log_transform else v

    # Within-person variance (weighted)
    weights = [p[0].wt_ntr for p in pairs]
    sw = sum(weights)
    sigma_w2 = sum(
        w * (T(p[0].intake) - T(p[1].intake)) ** 2 / 2
        for w, p in zip(weights, pairs)
    ) / max(sw, WEPS)

    # Total variance from x1
    x1_t = [T(p.intake) for p in x1]
    x1_w = [p.wt_ntr for p in x1]
    mu_x1 = _weighted_mean(x1_t, x1_w)
    sigma_total2 = _weighted_var(x1_t, x1_w, mu_x1)
    sigma_b2 = max(0.0, sigma_total2 - sigma_w2 / 2)
    reliability = sigma_b2 / max(sigma_b2 + sigma_w2, WEPS)

    # BLUP per x1 person
    usual = []
    for p in x1:
        blup_t = mu_x1 + reliability * (T(p.intake) - mu_x1)
        ui = max(Tinv(blup_t), 0.0)
        usual.append(ui)

    persons_for_table = [
        (x1[i].id, x1[i].sex, x1[i].age_g_desc, x1[i].wt_ntr, usual[i])
        for i in range(len(x1))
    ]
    table, mean_t, med_t, sd_t, p95_t, p99_t = _make_quantile_table(persons_for_table)

    return AnalysisResult(
        sigma_b2=sigma_b2, sigma_w2=sigma_w2, reliability=reliability,
        log_transformed=log_transform,
        method_used="ISU",
        result_table=table,
        n_total=len(x1), mean_total=mean_t, median_total=med_t,
        sd_total=sd_t, p95_total=p95_t, p99_total=p99_t,
    )


# ── MSM ──────────────────────────────────────────────────
def _run_msm(
    x0_days: List[PersonDay],
    x1: List[PersonX1],
) -> Optional[AnalysisResult]:
    """Multiple Source Method — for episodic foods."""
    pos_x1 = [p for p in x1 if p.intake > 0]
    if len(pos_x1) < 5:
        return None

    p_hat = len(pos_x1) / max(len(x1), 1)

    # Log-normal params from x1 positive records (weighted)
    log_vals = [math.log(max(p.intake, EPS)) for p in pos_x1]
    pos_w = [p.wt_ntr for p in pos_x1]
    mu_log = _weighted_mean(log_vals, pos_w)
    sigma_total2 = _weighted_var(log_vals, pos_w, mu_log)

    # Variance components from both-day consuming x0 pairs
    by_person: Dict[str, List[PersonDay]] = {}
    for pd_ in x0_days:
        by_person.setdefault(pd_.id, []).append(pd_)
    both_pairs = [(v[0], v[1]) for v in by_person.values()
                  if len(v) == 2 and v[0].intake > 0 and v[1].intake > 0]

    if len(both_pairs) >= 3:
        pair_w = [p[0].wt_ntr for p in both_pairs]
        sw = sum(pair_w)
        sigma_w2 = sum(
            w * (math.log(max(p[0].intake, EPS)) - math.log(max(p[1].intake, EPS))) ** 2 / 2
            for w, p in zip(pair_w, both_pairs)
        ) / max(sw, WEPS)
        sigma_b2 = max(0.0, sigma_total2 - sigma_w2 / 2)
    else:
        sigma_w2 = sigma_b2 = sigma_total2 * 0.5

    reliability = sigma_b2 / max(sigma_b2 + sigma_w2, WEPS)
    pop_amount = math.exp(mu_log + sigma_w2 / 2)

    usual = []
    for p in x1:
        if p.intake > 0:
            log_blup = mu_log + reliability * (math.log(max(p.intake, EPS)) - mu_log)
            amount = math.exp(log_blup + (1 - reliability) * sigma_w2 / 2)
            ui = p_hat * amount
        else:
            ui = p_hat * pop_amount
        usual.append(max(ui, 0.0))

    persons_for_table = [
        (x1[i].id, x1[i].sex, x1[i].age_g_desc, x1[i].wt_ntr, usual[i])
        for i in range(len(x1))
    ]
    table, mean_t, med_t, sd_t, p95_t, p99_t = _make_quantile_table(persons_for_table)

    return AnalysisResult(
        sigma_b2=sigma_b2, sigma_w2=sigma_w2, reliability=reliability,
        method_used="MSM",
        result_table=table,
        n_total=len(x1), mean_total=mean_t, median_total=med_t,
        sd_total=sd_t, p95_total=p95_t, p99_total=p99_t,
    )


# ── Main entry point ─────────────────────────────────────
def compute(
    x0_records: List[SurveyRecord],
    x1_records: List[SurveyRecord],
    food_codes: List[str],
    sim_time: int = 5,
) -> AnalysisResult:
    """
    Run full usual intake analysis.
    x0_records: raw 2-day recall records
    x1_records: raw 1-day recall records
    food_codes: filter codes from the selected food group
    """
    codes_set = set(food_codes)

    # 1. Aggregate
    x0_days = _aggregate_x0(x0_records, codes_set)
    x1 = _aggregate_x1(x1_records, codes_set)

    if not x1:
        return AnalysisResult(method_used="NCI", method_note="x1 데이터가 없습니다.")

    # 2. Correlations
    rho_p, rho_a, papa = _estimate_correlations(x0_days)

    # 3. NCI
    nci = _run_nci(x1, rho_p, rho_a, sim_time)
    nci.papa = papa
    nci.rho_p = rho_p
    nci.rho_a = rho_a
    zero_prev = nci.zero_prevalence

    # 4. Supplementary method
    is_regular = papa <= 0.05 and zero_prev <= 0.15
    try:
        if is_regular:
            supp = _run_isu(x0_days, x1)
            note = "ISU BLUP (보완)"
        else:
            supp = _run_msm(x0_days, x1)
            note = "MSM (보완)"
        if supp:
            supp.rho_p = rho_p
            supp.rho_a = rho_a
            supp.papa = papa
            nci.additional_result = supp
            nci.method_note = f"NCI (기본) + {note}"
        else:
            nci.method_note = "NCI (기본)"
    except Exception as e:
        nci.method_note = f"NCI (기본, 보완 실패: {e})"

    return nci
