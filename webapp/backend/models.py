"""Pydantic models mirroring the C# data models."""
from __future__ import annotations
from datetime import datetime
from enum import Enum
from typing import List, Optional
from pydantic import BaseModel, Field
import uuid


def new_id() -> str:
    return str(uuid.uuid4())


# ── Enums ────────────────────────────────────────────────
class DatasetType(str, Enum):
    X0 = "X0"
    X1 = "X1"
    CB = "CB"


# ── Survey Hierarchy ─────────────────────────────────────
class SurveyProject(BaseModel):
    id: str = Field(default_factory=new_id)
    project_code: str = ""
    name_ko: str
    name_en: str = ""
    conducting_org: str = ""
    commission_org: str = ""
    survey_domain: str = ""
    description: str = ""
    is_built_in: bool = False


class SurveyPhase(BaseModel):
    id: str = Field(default_factory=new_id)
    project_id: str
    phase_no: int = 1
    phase_label: str
    year_start: Optional[int] = None
    year_end: Optional[int] = None
    status: str = "active"
    sample_size: Optional[int] = None
    notes: str = ""
    is_built_in: bool = False

    @property
    def display_label(self) -> str:
        if self.year_start and self.year_end:
            if self.year_start == self.year_end:
                return f"{self.phase_label} · {self.year_start}"
            return f"{self.phase_label} · {self.year_start}–{self.year_end}"
        return self.phase_label


class SurveyRound(BaseModel):
    id: str = Field(default_factory=new_id)
    phase_id: str
    round_no: int = 1
    survey_year: Optional[int] = None
    field_end: Optional[str] = None
    status: str = "active"
    notes: str = ""
    is_built_in: bool = False

    @property
    def display_label(self) -> str:
        return f"{self.round_no}차"


class ActiveSourceState(BaseModel):
    active_round_id: str = ""


# ── Dataset ──────────────────────────────────────────────
class DatasetInfo(BaseModel):
    id: str = Field(default_factory=new_id)
    type: DatasetType
    round_id: str = ""
    registered_at: datetime = Field(default_factory=datetime.now)
    description: str = ""
    registered_by: str = ""
    file_name: str
    row_count: int = 0


class CodebookInfo(BaseModel):
    id: str = Field(default_factory=new_id)
    round_id: str = ""
    uploaded_at: datetime = Field(default_factory=datetime.now)
    file_name: str
    row_count: int = 0


# ── Food ─────────────────────────────────────────────────
class FoodGroup(BaseModel):
    id: str = Field(default_factory=new_id)
    name: str
    description: str = ""
    food_codes: List[str] = Field(default_factory=list)
    food_names: List[str] = Field(default_factory=list)
    is_built_in: bool = False
    created_at: datetime = Field(default_factory=datetime.now)
    updated_at: datetime = Field(default_factory=datetime.now)


class FoodCodeEntry(BaseModel):
    no: str = ""
    code: str
    code_name: str = ""
    food_group: str = ""
    mims_code: str = ""
    mims_name: str = ""
    food_name: str = ""
    sub_cat1: str = ""
    sub_cat2: str = ""


# ── Scenario ─────────────────────────────────────────────
class Scenario(BaseModel):
    id: str = Field(default_factory=new_id)
    name: str
    food_group_id: str = ""
    food_names: List[str] = Field(default_factory=list)
    food_codes: List[str] = Field(default_factory=list)
    x1_ids: List[str] = Field(default_factory=list)
    x0_ids: List[str] = Field(default_factory=list)
    sim_time: int = 5
    registered_by: str = ""
    registered_at: datetime = Field(default_factory=datetime.now)
    last_analyzed_at: Optional[datetime] = None


# ── Analysis ─────────────────────────────────────────────
class SurveyRecord(BaseModel):
    """Single row from raw CSV/XLSX survey file."""
    id: str
    sex: int
    age: int
    age_g: int
    age_g_desc: str
    day: int
    region: int = 0
    wt_ntr: float = 1.0
    f_code: str = ""
    nf_intk: float = 0.0
    ffq: float = 0.0
    ho_incm: int = 1


class QuantileRow(BaseModel):
    sex: str
    age_g_desc: str
    n: int
    average: float
    sd: float
    p1: float = 0.0
    p5: float = 0.0
    p25: float = 0.0
    median: float = 0.0
    p75: float = 0.0
    p90: float = 0.0
    p95: float = 0.0
    p975: float = 0.0
    p99: float = 0.0
    min_val: float = 0.0
    max_val: float = 0.0


class AnalysisResult(BaseModel):
    scenario_id: str = ""
    rho_p: float = 0.0
    rho_a: float = 0.0
    papa: float = 0.0
    zero_prevalence: float = 0.0
    sigma_b2: float = 0.0
    sigma_w2: float = 0.0
    reliability: float = 0.0
    log_transformed: bool = False
    gamma_shape: float = 0.0
    gamma_scale: float = 0.0
    method_used: str = "NCI"
    method_note: str = ""
    result_table: List[QuantileRow] = Field(default_factory=list)
    additional_result: Optional[AnalysisResult] = None
    n_total: int = 0
    mean_total: float = 0.0
    median_total: float = 0.0
    sd_total: float = 0.0
    p95_total: float = 0.0
    p99_total: float = 0.0


AnalysisResult.model_rebuild()


# ── Request/Response DTOs ─────────────────────────────────
class RunAnalysisRequest(BaseModel):
    scenario_id: Optional[str] = None
    food_group_id: str
    x1_ids: List[str]
    x0_ids: List[str]
    sim_time: int = 5
    registered_by: str = ""


class DatasetRow(BaseModel):
    """Row shown in the DB management table."""
    id: str
    type: DatasetType
    type_label: str
    file_name: str
    source: str          # human-readable "project · phase · round"
    round_id: str
    row_count: int
    registered_at: str
    description: str
    is_orphan: bool


class ProjectCreate(BaseModel):
    project_code: str
    name_ko: str
    name_en: str = ""
    conducting_org: str = ""
    description: str = ""


class PhaseCreate(BaseModel):
    project_id: str
    phase_label: str
    year_start: Optional[int] = None
    year_end: Optional[int] = None
    status: str = "active"


class RoundCreate(BaseModel):
    phase_id: str
    round_no: int
    survey_year: Optional[int] = None
    status: str = "active"
    notes: str = ""


class FoodGroupCreate(BaseModel):
    name: str
    description: str = ""
    food_codes: List[str] = Field(default_factory=list)
    food_names: List[str] = Field(default_factory=list)
