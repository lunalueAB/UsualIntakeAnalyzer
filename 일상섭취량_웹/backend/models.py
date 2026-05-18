from pydantic import BaseModel, Field
from typing import Optional, List, Any
from datetime import datetime

# ── Survey Source ─────────────────────────────────────────────────────────────
class ProjectCreate(BaseModel):
    name_ko: str
    project_code: str
    name_en: str = ""
    conducting_org: str = ""
    commission_org: str = ""
    survey_domain: str = ""
    is_builtin: int = 0

class PhaseCreate(BaseModel):
    project_id: str
    phase_no: int
    phase_label: str
    year_start: Optional[int] = None
    year_end: Optional[int] = None
    status: str = "active"
    sample_size: int = 0
    notes: str = ""
    is_builtin: int = 0

class RoundCreate(BaseModel):
    phase_id: str
    round_no: int
    display_label: str
    status: str = "active"
    survey_year: Optional[int] = None
    notes: str = ""
    is_builtin: int = 0

# ── Dataset ───────────────────────────────────────────────────────────────────
class DatasetOut(BaseModel):
    id: str
    type: str
    round_id: str
    filename: str
    original_filename: str
    description: str
    registered_by: str
    row_count: int
    registered_at: str
    source_label: Optional[str] = None  # joined from round/phase/project

class DatasetUpdate(BaseModel):
    round_id: Optional[str] = None
    description: Optional[str] = None

# ── Food Group ────────────────────────────────────────────────────────────────
class FoodGroupCreate(BaseModel):
    name: str
    memo: str = ""

class FoodCodeEntry(BaseModel):
    fcode: str
    food_name: str = ""

class FoodGroupOut(BaseModel):
    id: str
    name: str
    memo: str
    is_builtin: bool
    food_count: int
    code_count: int
    foods: List[str] = []
    codes: List[str] = []

# ── Food Preset ───────────────────────────────────────────────────────────────
class FoodPresetCreate(BaseModel):
    name: str
    description: str = ""
    food_codes: List[str] = []
    food_names: List[str] = []

class FoodPresetOut(BaseModel):
    id: str
    name: str
    description: str
    food_codes: List[str]
    food_names: List[str]
    is_builtin: bool
    created_at: str
    last_analyzed_at: Optional[str]
    has_cache: bool = False

# ── Scenario ──────────────────────────────────────────────────────────────────
class ScenarioCreate(BaseModel):
    name: str
    food_group_id: str = ""
    food_names: List[str] = []
    food_codes: List[str] = []
    x1_ids: List[str]
    x0_ids: List[str]
    sim_time: int = 5
    registered_by: str = ""

class ScenarioOut(BaseModel):
    id: str
    name: str
    food_group_id: str = ""
    food_names: List[str]
    food_codes: List[str]
    x1_ids: List[str]
    x0_ids: List[str]
    sim_time: int
    registered_by: str
    registered_at: str
    last_analyzed_at: Optional[str]

# ── Analysis ──────────────────────────────────────────────────────────────────
class AnalysisRequest(BaseModel):
    scenario_id: str

class QuantileRow(BaseModel):
    sex: str = "ALL"
    age_g_desc: str = "ALL"
    n: int = 0
    average: float = 0
    sd: float = 0
    p1st: float = 0
    p5th: float = 0
    p25th: float = 0
    median: float = 0
    p75th: float = 0
    p90th: float = 0
    p95th: float = 0
    p975th: float = 0
    p99th: float = 0
    min_val: float = 0
    max_val: float = 0

class AnalysisResultOut(BaseModel):
    id: str
    scenario_id: str
    rho_p: float
    rho_a: float
    papa: float
    zero_prevalence: float
    sigma_b2: float
    sigma_w2: float
    reliability: float
    log_transformed: bool
    gamma_shape: float
    gamma_scale: float
    method_used: str
    method_note: str
    result_table: List[dict]
    person_intakes: List[dict]
    additional_result: Optional[dict]
    created_at: str
