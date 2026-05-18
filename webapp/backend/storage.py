"""JSON file storage layer — reads/writes the same files as the WPF app."""
from __future__ import annotations
import json
from pathlib import Path
from typing import Any, List, Optional, Type, TypeVar
from pydantic import BaseModel

from config import (
    DATASETS_FILE, CODEBOOK_FILE, PROJECTS_FILE, PHASES_FILE, ROUNDS_FILE,
    ACTIVE_SRC_FILE, GROUPS_FILE, SCENARIOS_FILE, ensure_dirs,
)
from models import (
    DatasetInfo, CodebookInfo, SurveyProject, SurveyPhase, SurveyRound,
    ActiveSourceState, FoodGroup, Scenario,
)

T = TypeVar("T", bound=BaseModel)


def _read_list(path: Path, model: Type[T]) -> List[T]:
    ensure_dirs()
    if not path.exists():
        return []
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
        if not isinstance(data, list):
            return []
        return [model.model_validate(item) for item in data]
    except Exception:
        return []


def _write_list(path: Path, items: List[BaseModel]) -> None:
    ensure_dirs()
    path.write_text(
        json.dumps([item.model_dump(mode="json") for item in items],
                   ensure_ascii=False, indent=2),
        encoding="utf-8",
    )


def _read_obj(path: Path, model: Type[T]) -> Optional[T]:
    ensure_dirs()
    if not path.exists():
        return None
    try:
        return model.model_validate(json.loads(path.read_text(encoding="utf-8")))
    except Exception:
        return None


def _write_obj(path: Path, obj: BaseModel) -> None:
    ensure_dirs()
    path.write_text(
        json.dumps(obj.model_dump(mode="json"), ensure_ascii=False, indent=2),
        encoding="utf-8",
    )


# ── Datasets ─────────────────────────────────────────────
def load_datasets() -> List[DatasetInfo]:
    return _read_list(DATASETS_FILE, DatasetInfo)


def save_datasets(items: List[DatasetInfo]) -> None:
    _write_list(DATASETS_FILE, items)


def add_dataset(ds: DatasetInfo) -> None:
    items = load_datasets()
    items.append(ds)
    save_datasets(items)


def remove_dataset(ds_id: str) -> bool:
    items = load_datasets()
    new = [d for d in items if d.id != ds_id]
    if len(new) == len(items):
        return False
    save_datasets(new)
    return True


def get_dataset(ds_id: str) -> Optional[DatasetInfo]:
    return next((d for d in load_datasets() if d.id == ds_id), None)


# ── Codebook ─────────────────────────────────────────────
def load_codebook() -> Optional[CodebookInfo]:
    return _read_obj(CODEBOOK_FILE, CodebookInfo)


def save_codebook(cb: Optional[CodebookInfo]) -> None:
    if cb is None:
        if CODEBOOK_FILE.exists():
            CODEBOOK_FILE.unlink()
        return
    _write_obj(CODEBOOK_FILE, cb)


# ── Survey Hierarchy ─────────────────────────────────────
def load_projects() -> List[SurveyProject]:
    items = _read_list(PROJECTS_FILE, SurveyProject)
    if not items:
        items = _seed_projects()
        _write_list(PROJECTS_FILE, items)
        _seed_phases_and_rounds()
    return items


def load_phases() -> List[SurveyPhase]:
    return _read_list(PHASES_FILE, SurveyPhase)


def load_rounds() -> List[SurveyRound]:
    return _read_list(ROUNDS_FILE, SurveyRound)


def save_projects(items: List[SurveyProject]) -> None:
    _write_list(PROJECTS_FILE, items)


def save_phases(items: List[SurveyPhase]) -> None:
    _write_list(PHASES_FILE, items)


def save_rounds(items: List[SurveyRound]) -> None:
    _write_list(ROUNDS_FILE, items)


def load_active_source() -> ActiveSourceState:
    s = _read_obj(ACTIVE_SRC_FILE, ActiveSourceState)
    return s if s else ActiveSourceState()


def save_active_source(state: ActiveSourceState) -> None:
    _write_obj(ACTIVE_SRC_FILE, state)


# ── Food Groups ──────────────────────────────────────────
def load_groups() -> List[FoodGroup]:
    return _read_list(GROUPS_FILE, FoodGroup)


def save_groups(items: List[FoodGroup]) -> None:
    _write_list(GROUPS_FILE, items)


def add_group(g: FoodGroup) -> None:
    items = load_groups()
    items.append(g)
    save_groups(items)


def update_group(g: FoodGroup) -> bool:
    items = load_groups()
    for i, existing in enumerate(items):
        if existing.id == g.id:
            items[i] = g
            save_groups(items)
            return True
    return False


def remove_group(gid: str) -> bool:
    items = load_groups()
    new = [g for g in items if g.id != gid]
    if len(new) == len(items):
        return False
    save_groups(new)
    return True


# ── Scenarios ─────────────────────────────────────────────
def load_scenarios() -> List[Scenario]:
    return _read_list(SCENARIOS_FILE, Scenario)


def save_scenarios(items: List[Scenario]) -> None:
    _write_list(SCENARIOS_FILE, items)


def add_scenario(sc: Scenario) -> None:
    items = load_scenarios()
    items.append(sc)
    save_scenarios(items)


def get_scenario(sc_id: str) -> Optional[Scenario]:
    return next((s for s in load_scenarios() if s.id == sc_id), None)


# ── Seed Data (mirrors SurveySourceService.Seed) ─────────
def _seed_projects() -> List[SurveyProject]:
    return [
        SurveyProject(id="p-knhanes", project_code="KNHANES",
                      name_ko="국민건강영양조사", name_en="KNHANES",
                      conducting_org="질병관리청", is_built_in=True),
        SurveyProject(id="p-kpnc", project_code="KPNC",
                      name_ko="정밀영양조사사업", name_en="KPNC",
                      conducting_org="가천대학교", is_built_in=True),
    ]


def _seed_phases_and_rounds() -> None:
    from datetime import datetime
    phases: List[SurveyPhase] = []
    rounds: List[SurveyRound] = []

    # KNHANES phases 1-9
    phase_meta = [
        ("1기", 1998, 1998), ("2기", 2001, 2002), ("3기", 2005, 2005),
        ("4기", 2007, 2009), ("5기", 2010, 2012), ("6기", 2013, 2015),
        ("7기", 2016, 2018), ("8기", 2019, 2021), ("9기", 2022, 2024),
    ]
    for i, (label, ys, ye) in enumerate(phase_meta, 1):
        pid = f"ph-kn-{i}"
        phases.append(SurveyPhase(id=pid, project_id="p-knhanes",
                                   phase_no=i, phase_label=label,
                                   year_start=ys, year_end=ye, is_built_in=True))
        if i == 8:
            for rno, yr in [(1, 2019), (2, 2020), (3, 2021)]:
                rounds.append(SurveyRound(id=f"r-kn-8-{rno}", phase_id=pid,
                                           round_no=rno, survey_year=yr, is_built_in=True))

    # KPNC phase 1
    phases.append(SurveyPhase(id="ph-kpnc-1", project_id="p-kpnc",
                               phase_no=1, phase_label="1기",
                               year_start=2025, year_end=2029, is_built_in=True))
    rounds.append(SurveyRound(id="r-kpnc-1-1", phase_id="ph-kpnc-1",
                               round_no=1, survey_year=2025, is_built_in=True))

    save_phases(phases)
    save_rounds(rounds)
