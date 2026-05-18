import uuid, json
from fastapi import APIRouter, HTTPException
from database import get_conn
from models import FoodPresetCreate
from typing import List

router = APIRouter()

def _row_to_dict(row) -> dict:
    d = dict(row)
    for k in ('food_codes', 'food_names'):
        try: d[k] = json.loads(d.get(k) or '[]')
        except: d[k] = []
    d['has_cache'] = False  # 웹버전은 캐시 미지원
    d['is_builtin'] = bool(d.get('is_builtin', 0))
    return d

@router.get("")
def list_presets():
    with get_conn() as conn:
        rows = conn.execute("SELECT * FROM food_presets ORDER BY name").fetchall()
    return [_row_to_dict(r) for r in rows]

@router.post("")
def create_preset(body: FoodPresetCreate):
    pid = str(uuid.uuid4())
    with get_conn() as conn:
        conn.execute(
            "INSERT INTO food_presets(id,name,description,food_codes,food_names) VALUES(?,?,?,?,?)",
            (pid, body.name, body.description,
             json.dumps(body.food_codes, ensure_ascii=False),
             json.dumps(body.food_names, ensure_ascii=False))
        )
    return {"id": pid}

@router.put("/{pid}")
def update_preset(pid: str, body: FoodPresetCreate):
    with get_conn() as conn:
        conn.execute(
            "UPDATE food_presets SET name=?,description=?,food_codes=?,food_names=?,updated_at=datetime('now','localtime') WHERE id=?",
            (body.name, body.description,
             json.dumps(body.food_codes, ensure_ascii=False),
             json.dumps(body.food_names, ensure_ascii=False),
             pid)
        )
    return {"ok": True}

@router.delete("/{pid}")
def delete_preset(pid: str):
    with get_conn() as conn:
        conn.execute("DELETE FROM food_presets WHERE id=?", (pid,))
    return {"ok": True}
