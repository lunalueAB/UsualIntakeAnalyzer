import uuid, json
from fastapi import APIRouter, HTTPException
from database import get_conn
from models import FoodGroupCreate, FoodCodeEntry
from typing import List

router = APIRouter()

@router.get("")
def list_groups():
    with get_conn() as conn:
        groups = conn.execute("SELECT * FROM food_groups ORDER BY is_builtin DESC, name").fetchall()
        result = []
        for g in groups:
            codes = conn.execute(
                "SELECT fcode, food_name FROM food_group_codes WHERE group_id=? ORDER BY fcode",
                (g['id'],)).fetchall()
            result.append({
                **dict(g),
                'food_count': len(codes),
                'code_count': len(codes),
                'foods': [c['food_name'] for c in codes if c['food_name']],
                'codes': [c['fcode'] for c in codes],
            })
    return result

@router.post("")
def create_group(body: FoodGroupCreate):
    gid = str(uuid.uuid4())
    with get_conn() as conn:
        conn.execute("INSERT INTO food_groups(id,name,memo) VALUES(?,?,?)",
                     (gid, body.name, body.memo))
    return {"id": gid}

@router.put("/{gid}")
def update_group(gid: str, body: FoodGroupCreate):
    with get_conn() as conn:
        conn.execute("UPDATE food_groups SET name=?,memo=? WHERE id=?",
                     (body.name, body.memo, gid))
    return {"ok": True}

@router.delete("/{gid}")
def delete_group(gid: str):
    with get_conn() as conn:
        row = conn.execute("SELECT is_builtin FROM food_groups WHERE id=?", (gid,)).fetchone()
        if row and row['is_builtin']:
            raise HTTPException(400, "기본 제공 식품군은 삭제할 수 없습니다.")
        conn.execute("DELETE FROM food_groups WHERE id=?", (gid,))
    return {"ok": True}

@router.get("/{gid}/codes")
def get_codes(gid: str):
    with get_conn() as conn:
        rows = conn.execute(
            "SELECT fcode, food_name FROM food_group_codes WHERE group_id=? ORDER BY fcode",
            (gid,)).fetchall()
    return [dict(r) for r in rows]

@router.put("/{gid}/codes")
def set_codes(gid: str, codes: List[FoodCodeEntry]):
    with get_conn() as conn:
        conn.execute("DELETE FROM food_group_codes WHERE group_id=?", (gid,))
        for c in codes:
            conn.execute("INSERT OR REPLACE INTO food_group_codes(group_id,fcode,food_name) VALUES(?,?,?)",
                         (gid, c.fcode, c.food_name))
    return {"ok": True, "count": len(codes)}
