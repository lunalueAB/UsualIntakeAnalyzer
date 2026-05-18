import uuid, json
from fastapi import APIRouter, HTTPException
from database import get_conn
from models import ScenarioCreate
from typing import List

router = APIRouter()

def _row_to_dict(row) -> dict:
    d = dict(row)
    for k in ('food_names','food_codes','x1_ids','x0_ids'):
        try:    d[k] = json.loads(d.get(k) or '[]')
        except: d[k] = []
    return d

@router.get("")
def list_scenarios():
    with get_conn() as conn:
        rows = conn.execute("SELECT * FROM scenarios ORDER BY registered_at DESC").fetchall()
    return [_row_to_dict(r) for r in rows]

@router.get("/{sid}")
def get_scenario(sid: str):
    with get_conn() as conn:
        row = conn.execute("SELECT * FROM scenarios WHERE id=?", (sid,)).fetchone()
    if not row: raise HTTPException(404)
    return _row_to_dict(row)

@router.post("")
def create_scenario(body: ScenarioCreate):
    sid = str(uuid.uuid4())
    with get_conn() as conn:
        conn.execute(
            "INSERT INTO scenarios(id,name,food_group_id,food_names,food_codes,"
            "x1_ids,x0_ids,sim_time,registered_by) VALUES(?,?,?,?,?,?,?,?,?)",
            (sid, body.name, body.food_group_id,
             json.dumps(body.food_names, ensure_ascii=False),
             json.dumps(body.food_codes, ensure_ascii=False),
             json.dumps(body.x1_ids), json.dumps(body.x0_ids),
             body.sim_time, body.registered_by)
        )
    return {"id": sid}

@router.delete("/{sid}")
def delete_scenario(sid: str):
    with get_conn() as conn:
        conn.execute("DELETE FROM scenarios WHERE id=?", (sid,))
        conn.execute("DELETE FROM analysis_results WHERE scenario_id=?", (sid,))
    return {"ok": True}

@router.get("/{sid}/result")
def get_latest_result(sid: str):
    with get_conn() as conn:
        row = conn.execute(
            "SELECT * FROM analysis_results WHERE scenario_id=? ORDER BY created_at DESC LIMIT 1",
            (sid,)).fetchone()
    if not row: raise HTTPException(404, "분석 결과가 없습니다.")
    d = dict(row)
    for k in ('result_table','person_intakes'):
        try:    d[k] = json.loads(d.get(k) or '[]')
        except: d[k] = []
    try:    d['additional_result'] = json.loads(d.get('additional_result') or 'null')
    except: d['additional_result'] = None
    d['log_transformed'] = bool(d.get('log_transformed', 0))
    return d
