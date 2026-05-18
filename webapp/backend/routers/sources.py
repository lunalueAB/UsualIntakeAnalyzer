import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent.parent))

from typing import List, Optional
from fastapi import APIRouter, HTTPException
from fastapi.responses import Response

from models import SurveyProject, SurveyPhase, SurveyRound, ProjectCreate, PhaseCreate, RoundCreate
import storage

router = APIRouter()

# ── Projects ──────────────────────────────────────────────
@router.get("/projects", response_model=List[SurveyProject])
def list_projects():
    return storage.load_projects()

@router.post("/projects", response_model=SurveyProject)
def create_project(body: ProjectCreate):
    p = SurveyProject(**body.model_dump())
    items = storage.load_projects()
    items.append(p)
    storage.save_projects(items)
    return p

@router.put("/projects/{pid}", response_model=SurveyProject)
def update_project(pid: str, body: SurveyProject):
    items = storage.load_projects()
    for i, p in enumerate(items):
        if p.id == pid:
            items[i] = body
            storage.save_projects(items)
            return body
    raise HTTPException(404)

@router.delete("/projects/{pid}", status_code=204)
def delete_project(pid: str):
    projects = [p for p in storage.load_projects() if p.id != pid]
    phases = storage.load_phases()
    deleted_phase_ids = {ph.id for ph in phases if ph.project_id == pid}
    phases = [ph for ph in phases if ph.project_id != pid]
    rounds = [r for r in storage.load_rounds() if r.phase_id not in deleted_phase_ids]
    storage.save_projects(projects)
    storage.save_phases(phases)
    storage.save_rounds(rounds)
    return Response(status_code=204)

# ── Phases ────────────────────────────────────────────────
@router.get("/phases", response_model=List[SurveyPhase])
def list_phases(project_id: Optional[str] = None):
    phases = storage.load_phases()
    if project_id:
        phases = [p for p in phases if p.project_id == project_id]
    return phases

@router.post("/phases", response_model=SurveyPhase)
def create_phase(body: PhaseCreate):
    ph = SurveyPhase(**body.model_dump())
    items = storage.load_phases()
    items.append(ph)
    storage.save_phases(items)
    return ph

@router.put("/phases/{phid}", response_model=SurveyPhase)
def update_phase(phid: str, body: SurveyPhase):
    items = storage.load_phases()
    for i, p in enumerate(items):
        if p.id == phid:
            items[i] = body
            storage.save_phases(items)
            return body
    raise HTTPException(404)

@router.delete("/phases/{phid}", status_code=204)
def delete_phase(phid: str):
    phases = [p for p in storage.load_phases() if p.id != phid]
    rounds = [r for r in storage.load_rounds() if r.phase_id != phid]
    storage.save_phases(phases)
    storage.save_rounds(rounds)
    return Response(status_code=204)

# ── Rounds ────────────────────────────────────────────────
@router.get("/rounds", response_model=List[SurveyRound])
def list_rounds(phase_id: Optional[str] = None):
    rounds = storage.load_rounds()
    if phase_id:
        rounds = [r for r in rounds if r.phase_id == phase_id]
    return rounds

@router.post("/rounds", response_model=SurveyRound)
def create_round(body: RoundCreate):
    r = SurveyRound(**body.model_dump())
    items = storage.load_rounds()
    items.append(r)
    storage.save_rounds(items)
    return r

@router.put("/rounds/{rid}", response_model=SurveyRound)
def update_round(rid: str, body: SurveyRound):
    items = storage.load_rounds()
    for i, r in enumerate(items):
        if r.id == rid:
            items[i] = body
            storage.save_rounds(items)
            return body
    raise HTTPException(404)

@router.delete("/rounds/{rid}", status_code=204)
def delete_round(rid: str):
    rounds = [r for r in storage.load_rounds() if r.id != rid]
    storage.save_rounds(rounds)
    return Response(status_code=204)

# ── Tree ──────────────────────────────────────────────────
@router.get("/tree")
def get_tree():
    projects = storage.load_projects()
    phases = storage.load_phases()
    rounds = storage.load_rounds()
    result = []
    for p in sorted(projects, key=lambda x: x.name_ko):
        p_phases = sorted([ph for ph in phases if ph.project_id == p.id], key=lambda x: x.phase_no)
        phase_list = []
        for ph in p_phases:
            ph_rounds = sorted([r for r in rounds if r.phase_id == ph.id], key=lambda x: x.round_no)
            phase_list.append({**ph.model_dump(), "rounds": [r.model_dump() for r in ph_rounds]})
        result.append({**p.model_dump(), "phases": phase_list})
    return result
