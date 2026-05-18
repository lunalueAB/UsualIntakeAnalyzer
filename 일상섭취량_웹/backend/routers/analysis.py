import uuid, json
from fastapi import APIRouter, HTTPException, BackgroundTasks
from database import get_conn, UPL_DIR
from models import AnalysisRequest
from services.calculator import compute
from services.file_service import parse_file
from dataclasses import asdict

router = APIRouter()

def _load_records(dataset_ids: list) -> list:
    records = []
    with get_conn() as conn:
        for did in dataset_ids:
            row = conn.execute("SELECT filename FROM datasets WHERE id=?", (did,)).fetchone()
            if row:
                path = UPL_DIR / row['filename']
                if path.exists():
                    records.extend(parse_file(str(path)))
    return records

@router.post("/run")
def run_analysis(body: AnalysisRequest):
    with get_conn() as conn:
        sc = conn.execute("SELECT * FROM scenarios WHERE id=?", (body.scenario_id,)).fetchone()
    if not sc:
        raise HTTPException(404, "시나리오를 찾을 수 없습니다.")

    sc = dict(sc)
    x1_ids    = json.loads(sc.get('x1_ids') or '[]')
    x0_ids    = json.loads(sc.get('x0_ids') or '[]')
    food_codes= json.loads(sc.get('food_codes') or '[]')
    sim_time  = int(sc.get('sim_time', 5))

    if not x1_ids or not x0_ids:
        raise HTTPException(400, "1일 또는 2일 조사 데이터셋이 지정되지 않았습니다.")

    messages = []
    def progress(msg): messages.append(msg)

    try:
        x1_raw = _load_records(x1_ids)
        x0_raw = _load_records(x0_ids)
        if not x1_raw: raise ValueError("1일 조사 데이터를 불러올 수 없습니다.")
        if not x0_raw: raise ValueError("2일 조사 데이터를 불러올 수 없습니다.")

        result = compute(x0_raw, x1_raw, set(food_codes), sim_time, progress)
    except Exception as e:
        raise HTTPException(500, f"분석 오류: {e}")

    # 직렬화
    def row_to_dict(r):
        d = asdict(r)
        d['min_val'] = d.pop('min_val', d.get('min_val', 0))
        d['max_val'] = d.pop('max_val', d.get('max_val', 0))
        return d

    table_json   = json.dumps([row_to_dict(r) for r in result.result_table], ensure_ascii=False)
    intakes_json = json.dumps([asdict(p) for p in result.person_intakes], ensure_ascii=False)
    add_json     = None
    if result.additional_result:
        ar = result.additional_result
        add_json = json.dumps({
            'method_used': ar.method_used, 'method_note': ar.method_note,
            'sigma_b2': ar.sigma_b2, 'sigma_w2': ar.sigma_w2,
            'reliability': ar.reliability, 'log_transformed': ar.log_transformed,
            'result_table': [row_to_dict(r) for r in ar.result_table],
        }, ensure_ascii=False)

    rid = str(uuid.uuid4())
    with get_conn() as conn:
        conn.execute(
            "INSERT INTO analysis_results(id,scenario_id,rho_p,rho_a,papa,zero_prevalence,"
            "sigma_b2,sigma_w2,reliability,log_transformed,gamma_shape,gamma_scale,"
            "method_used,method_note,result_table,person_intakes,additional_result)"
            "VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
            (rid, body.scenario_id,
             result.rho_p, result.rho_a, result.papa, result.zero_prevalence,
             result.sigma_b2, result.sigma_w2, result.reliability,
             1 if result.log_transformed else 0,
             result.gamma_shape, result.gamma_scale,
             result.method_used, result.method_note,
             table_json, intakes_json, add_json)
        )
        conn.execute("UPDATE scenarios SET last_analyzed_at=datetime('now','localtime') WHERE id=?",
                     (body.scenario_id,))

    # 결과 반환 (person_intakes 제외 — 너무 큼)
    return {
        'id': rid,
        'scenario_id': body.scenario_id,
        'rho_p': result.rho_p, 'rho_a': result.rho_a,
        'papa': result.papa, 'zero_prevalence': result.zero_prevalence,
        'sigma_b2': result.sigma_b2, 'sigma_w2': result.sigma_w2,
        'reliability': result.reliability, 'log_transformed': result.log_transformed,
        'gamma_shape': result.gamma_shape, 'gamma_scale': result.gamma_scale,
        'method_used': result.method_used, 'method_note': result.method_note,
        'result_table': [row_to_dict(r) for r in result.result_table],
        'additional_result': json.loads(add_json) if add_json else None,
        'progress_log': messages,
    }

@router.get("/history")
def get_history():
    with get_conn() as conn:
        rows = conn.execute(
            "SELECT ar.id, ar.scenario_id, ar.method_used, ar.rho_p, ar.rho_a, ar.papa, "
            "ar.created_at, sc.name AS scenario_name "
            "FROM analysis_results ar LEFT JOIN scenarios sc ON sc.id=ar.scenario_id "
            "ORDER BY ar.created_at DESC LIMIT 100"
        ).fetchall()
    return [dict(r) for r in rows]
