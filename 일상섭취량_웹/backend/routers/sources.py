import uuid
from fastapi import APIRouter, HTTPException
from database import get_conn
from models import ProjectCreate, PhaseCreate, RoundCreate

router = APIRouter()

@router.get("/projects")
def get_projects():
    with get_conn() as conn:
        rows = conn.execute(
            "SELECT p.*, COUNT(DISTINCT ph.id) AS phase_count "
            "FROM survey_projects p LEFT JOIN survey_phases ph ON ph.project_id=p.id "
            "GROUP BY p.id ORDER BY p.name_ko"
        ).fetchall()
        result = []
        for r in rows:
            phases = conn.execute(
                "SELECT ph.*, COUNT(rd.id) AS round_count FROM survey_phases ph "
                "LEFT JOIN survey_rounds rd ON rd.phase_id=ph.id "
                "WHERE ph.project_id=? GROUP BY ph.id ORDER BY ph.phase_no", (r['id'],)
            ).fetchall()
            phlist = []
            for ph in phases:
                rounds = conn.execute(
                    "SELECT * FROM survey_rounds WHERE phase_id=? ORDER BY round_no", (ph['id'],)
                ).fetchall()
                phlist.append({**dict(ph), 'rounds': [dict(rd) for rd in rounds]})
            result.append({**dict(r), 'phases': phlist})
    return result

@router.post("/projects")
def add_project(body: ProjectCreate):
    pid = str(uuid.uuid4())
    with get_conn() as conn:
        conn.execute(
            "INSERT INTO survey_projects(id,name_ko,project_code,name_en,conducting_org,commission_org,survey_domain,is_builtin) "
            "VALUES(?,?,?,?,?,?,?,?)",
            (pid, body.name_ko, body.project_code, body.name_en,
             body.conducting_org, body.commission_org, body.survey_domain, body.is_builtin))
    return {"id": pid}

@router.put("/projects/{pid}")
def update_project(pid: str, body: ProjectCreate):
    with get_conn() as conn:
        conn.execute(
            "UPDATE survey_projects SET name_ko=?,project_code=?,name_en=?,conducting_org=?,commission_org=?,survey_domain=?,is_builtin=? WHERE id=?",
            (body.name_ko, body.project_code, body.name_en,
             body.conducting_org, body.commission_org, body.survey_domain, body.is_builtin, pid))
    return {"ok": True}

@router.delete("/projects/{pid}")
def delete_project(pid: str):
    with get_conn() as conn:
        conn.execute("DELETE FROM survey_projects WHERE id=?", (pid,))
    return {"ok": True}

@router.post("/phases")
def add_phase(body: PhaseCreate):
    pid = str(uuid.uuid4())
    with get_conn() as conn:
        conn.execute(
            "INSERT INTO survey_phases(id,project_id,phase_no,phase_label,year_start,year_end,status,sample_size,notes,is_builtin) "
            "VALUES(?,?,?,?,?,?,?,?,?,?)",
            (pid, body.project_id, body.phase_no, body.phase_label,
             body.year_start, body.year_end, body.status,
             body.sample_size, body.notes, body.is_builtin))
    return {"id": pid}

@router.put("/phases/{pid}")
def update_phase(pid: str, body: PhaseCreate):
    with get_conn() as conn:
        conn.execute(
            "UPDATE survey_phases SET phase_no=?,phase_label=?,year_start=?,year_end=?,status=?,sample_size=?,notes=?,is_builtin=? WHERE id=?",
            (body.phase_no, body.phase_label, body.year_start, body.year_end, body.status,
             body.sample_size, body.notes, body.is_builtin, pid))
    return {"ok": True}

@router.delete("/phases/{pid}")
def delete_phase(pid: str):
    with get_conn() as conn:
        conn.execute("DELETE FROM survey_phases WHERE id=?", (pid,))
    return {"ok": True}

@router.post("/rounds")
def add_round(body: RoundCreate):
    rid = str(uuid.uuid4())
    with get_conn() as conn:
        conn.execute(
            "INSERT INTO survey_rounds(id,phase_id,round_no,display_label,status,survey_year,notes,is_builtin) "
            "VALUES(?,?,?,?,?,?,?,?)",
            (rid, body.phase_id, body.round_no, body.display_label, body.status,
             body.survey_year, body.notes, body.is_builtin))
    return {"id": rid}

@router.put("/rounds/{rid}")
def update_round(rid: str, body: RoundCreate):
    with get_conn() as conn:
        conn.execute(
            "UPDATE survey_rounds SET round_no=?,display_label=?,status=?,survey_year=?,notes=?,is_builtin=? WHERE id=?",
            (body.round_no, body.display_label, body.status,
             body.survey_year, body.notes, body.is_builtin, rid))
    return {"ok": True}

@router.delete("/rounds/{rid}")
def delete_round(rid: str):
    with get_conn() as conn:
        conn.execute("DELETE FROM survey_rounds WHERE id=?", (rid,))
    return {"ok": True}
