import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent.parent))

from datetime import datetime
from typing import List, Optional
from fastapi import APIRouter, HTTPException, UploadFile, File, Form
from fastapi.responses import Response
import uuid

from config import RAW_DATA_DIR
from models import DatasetInfo, DatasetType, CodebookInfo, DatasetRow
import storage
import parser as pr

router = APIRouter()

def _type_label(t: DatasetType) -> str:
    return {"X1": "1일 조사 (x1)", "X0": "2일 조사 (x0)", "CB": "코드집 (CB)"}.get(t.value, t.value)

def _resolve_source(round_id: str) -> str:
    if not round_id:
        return "(자료원 미지정)"
    rounds = {r.id: r for r in storage.load_rounds()}
    phases = {p.id: p for p in storage.load_phases()}
    projects = {p.id: p for p in storage.load_projects()}
    r = rounds.get(round_id)
    if not r:
        return "(알 수 없음)"
    ph = phases.get(r.phase_id)
    pr_obj = projects.get(ph.project_id) if ph else None
    parts = []
    if pr_obj:
        parts.append(pr_obj.name_ko)
    if ph:
        parts.append(ph.phase_label)
    parts.append(r.display_label)
    return " · ".join(parts)

@router.get("", response_model=List[DatasetRow])
def list_datasets():
    known_rounds = {r.id for r in storage.load_rounds()}
    rows = []
    for d in storage.load_datasets():
        is_orphan = bool(d.round_id) and d.round_id not in known_rounds
        rows.append(DatasetRow(
            id=d.id, type=d.type, type_label=_type_label(d.type),
            file_name=d.file_name, source=_resolve_source(d.round_id),
            round_id=d.round_id, row_count=d.row_count,
            registered_at=d.registered_at.strftime("%Y-%m-%d"),
            description=d.description, is_orphan=is_orphan,
        ))
    return rows

@router.post("/upload", response_model=DatasetInfo)
async def upload_dataset(
    type: str = Form(...),
    round_id: str = Form(""),
    description: str = Form(""),
    registered_by: str = Form(""),
    file: UploadFile = File(...),
):
    suffix = Path(file.filename).suffix.lower()
    if suffix not in (".csv", ".xlsx", ".xls"):
        raise HTTPException(400, "CSV 또는 XLSX 파일만 지원합니다.")
    ds_id = str(uuid.uuid4())
    save_path = RAW_DATA_DIR / f"{ds_id}{suffix}"
    content = await file.read()
    save_path.write_bytes(content)
    row_count = pr.count_rows(save_path)
    ds = DatasetInfo(
        id=ds_id, type=DatasetType(type.upper()),
        round_id=round_id, description=description,
        registered_by=registered_by, file_name=file.filename,
        row_count=row_count,
    )
    storage.add_dataset(ds)
    return ds

@router.delete("/{ds_id}", status_code=204)
def delete_dataset(ds_id: str):
    ds = storage.get_dataset(ds_id)
    if not ds:
        raise HTTPException(404, "데이터셋을 찾을 수 없습니다.")
    # Remove file
    for suffix in (".csv", ".xlsx", ".xls"):
        fp = RAW_DATA_DIR / f"{ds_id}{suffix}"
        if fp.exists():
            fp.unlink()
    storage.remove_dataset(ds_id)
    return Response(status_code=204)

@router.get("/{ds_id}/preview")
def preview_dataset(ds_id: str, rows: int = 50):
    ds = storage.get_dataset(ds_id)
    if not ds:
        raise HTTPException(404)
    for suffix in (".csv", ".xlsx", ".xls"):
        fp = RAW_DATA_DIR / f"{ds_id}{suffix}"
        if fp.exists():
            df = pr.read_raw_file(fp)
            return df.head(rows).fillna("").to_dict(orient="records")
    raise HTTPException(404, "파일을 찾을 수 없습니다.")

@router.post("/codebook/upload", response_model=CodebookInfo)
async def upload_codebook(
    round_id: str = Form(""),
    file: UploadFile = File(...),
):
    suffix = Path(file.filename).suffix.lower()
    if suffix not in (".xlsx", ".xls"):
        raise HTTPException(400, "XLSX 파일만 지원합니다.")
    save_path = RAW_DATA_DIR / f"codebook{suffix}"
    content = await file.read()
    save_path.write_bytes(content)
    row_count = pr.count_rows(save_path)
    cb = CodebookInfo(round_id=round_id, file_name=file.filename, row_count=row_count)
    storage.save_codebook(cb)
    return cb

@router.get("/codebook/info")
def get_codebook():
    cb = storage.load_codebook()
    if not cb:
        return None
    return cb
