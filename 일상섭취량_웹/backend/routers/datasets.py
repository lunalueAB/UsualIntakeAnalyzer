import uuid
from fastapi import APIRouter, UploadFile, File, Form, HTTPException, Query
from fastapi.responses import FileResponse
from database import get_conn, UPL_DIR
from models import DatasetOut, DatasetUpdate
from services.file_service import save_upload
from typing import Optional

router = APIRouter()

def _source_label(conn, round_id: str) -> str:
    if not round_id: return ""
    rd = conn.execute("SELECT * FROM survey_rounds WHERE id=?", (round_id,)).fetchone()
    if not rd: return ""
    ph = conn.execute("SELECT * FROM survey_phases WHERE id=?", (rd['phase_id'],)).fetchone()
    if not ph: return rd['display_label']
    pr = conn.execute("SELECT * FROM survey_projects WHERE id=?", (ph['project_id'],)).fetchone()
    if not pr: return f"{ph['phase_label']} · {rd['display_label']}"
    return f"{pr['name_ko']} · {ph['phase_label']} · {rd['display_label']}"

@router.get("")
def list_datasets(
    type: Optional[str]     = Query(None),
    round_id: Optional[str] = Query(None),
    search: Optional[str]   = Query(None)
):
    with get_conn() as conn:
        q  = "SELECT * FROM datasets WHERE 1=1"
        ps = []
        if type:     q += " AND type=?";         ps.append(type)
        if round_id: q += " AND round_id=?";     ps.append(round_id)
        q += " ORDER BY registered_at DESC"
        rows = conn.execute(q, ps).fetchall()
        result = []
        for r in rows:
            d = dict(r)
            if search:
                s = search.lower()
                if s not in d.get('original_filename','').lower() and \
                   s not in d.get('description','').lower() and \
                   s not in _source_label(conn, d['round_id']).lower():
                    continue
            d['source_label'] = _source_label(conn, d['round_id'])
            result.append(d)
    return result

@router.post("")
async def upload_dataset(
    file:         UploadFile    = File(...),
    type:         str           = Form(...),
    round_id:     str           = Form(""),
    description:  str           = Form(""),
    registered_by:str           = Form(""),
):
    content  = await file.read()
    fname, row_count = save_upload(content, file.filename)
    did = str(uuid.uuid4())
    with get_conn() as conn:
        conn.execute(
            "INSERT INTO datasets(id,type,round_id,filename,original_filename,"
            "description,registered_by,row_count) VALUES(?,?,?,?,?,?,?,?)",
            (did, type, round_id, fname, file.filename, description, registered_by, row_count)
        )
    return {"id": did, "row_count": row_count}

@router.get("/{did}/download")
def download_dataset(did: str):
    with get_conn() as conn:
        row = conn.execute("SELECT * FROM datasets WHERE id=?", (did,)).fetchone()
    if not row:
        raise HTTPException(404, "데이터셋을 찾을 수 없습니다.")
    path = UPL_DIR / row['filename']
    if not path.exists():
        raise HTTPException(404, "파일이 서버에 없습니다.")
    return FileResponse(str(path), filename=row['original_filename'])

@router.put("/{did}")
def update_dataset(did: str, body: DatasetUpdate):
    with get_conn() as conn:
        if body.round_id is not None:
            conn.execute("UPDATE datasets SET round_id=? WHERE id=?", (body.round_id, did))
        if body.description is not None:
            conn.execute("UPDATE datasets SET description=? WHERE id=?", (body.description, did))
    return {"ok": True}

@router.delete("/{did}")
def delete_dataset(did: str):
    with get_conn() as conn:
        row = conn.execute("SELECT filename FROM datasets WHERE id=?", (did,)).fetchone()
        if row:
            p = UPL_DIR / row['filename']
            if p.exists(): p.unlink()
        conn.execute("DELETE FROM datasets WHERE id=?", (did,))
    return {"ok": True}
