import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent.parent))

from datetime import datetime
from typing import List
from fastapi import APIRouter, HTTPException

from config import RAW_DATA_DIR
from models import RunAnalysisRequest, AnalysisResult, Scenario
import storage
import parser as pr
import calculator

router = APIRouter()

def _find_file(ds_id: str) -> Path:
    for suffix in (".csv", ".xlsx", ".xls"):
        fp = RAW_DATA_DIR / f"{ds_id}{suffix}"
        if fp.exists():
            return fp
    raise FileNotFoundError(ds_id)

@router.post("/run", response_model=AnalysisResult)
def run_analysis(req: RunAnalysisRequest):
    # Load food group
    groups = {g.id: g for g in storage.load_groups()}
    group = groups.get(req.food_group_id)
    if not group:
        raise HTTPException(404, "식품군을 찾을 수 없습니다.")
    food_codes = group.food_codes
    if not food_codes:
        raise HTTPException(400, "선택된 식품군에 식품 코드가 없습니다.")

    # Load x1 records
    x1_records = []
    for ds_id in req.x1_ids:
        ds = storage.get_dataset(ds_id)
        if not ds:
            continue
        try:
            fp = _find_file(ds_id)
            x1_records.extend(pr.parse_survey_records(fp))
        except Exception as e:
            raise HTTPException(400, f"x1 파일 읽기 실패 ({ds_id}): {e}")

    # Load x0 records
    x0_records = []
    for ds_id in req.x0_ids:
        ds = storage.get_dataset(ds_id)
        if not ds:
            continue
        try:
            fp = _find_file(ds_id)
            x0_records.extend(pr.parse_survey_records(fp))
        except Exception as e:
            raise HTTPException(400, f"x0 파일 읽기 실패 ({ds_id}): {e}")

    if not x1_records:
        raise HTTPException(400, "1일 조사(x1) 데이터가 없습니다.")

    # Run analysis
    try:
        result = calculator.compute(x0_records, x1_records, food_codes, req.sim_time)
    except Exception as e:
        raise HTTPException(500, f"분석 중 오류: {e}")

    # Save scenario
    sc = Scenario(
        name=group.name,
        food_group_id=req.food_group_id,
        food_names=group.food_names,
        food_codes=food_codes,
        x1_ids=req.x1_ids,
        x0_ids=req.x0_ids,
        sim_time=req.sim_time,
        registered_by=req.registered_by,
        last_analyzed_at=datetime.now(),
    )
    if req.scenario_id:
        sc.id = req.scenario_id
    storage.add_scenario(sc)
    result.scenario_id = sc.id
    return result

@router.get("/history", response_model=List[Scenario])
def list_history():
    scenarios = storage.load_scenarios()
    return list(reversed(scenarios))

@router.get("/history/{sc_id}", response_model=Scenario)
def get_scenario(sc_id: str):
    sc = storage.get_scenario(sc_id)
    if not sc:
        raise HTTPException(404)
    return sc
