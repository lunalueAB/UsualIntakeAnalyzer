"""CSV / XLSX parsing utilities."""
from __future__ import annotations
import io
from pathlib import Path
from typing import List, Optional

import pandas as pd

from models import SurveyRecord, FoodCodeEntry


# Column name aliases (handles slight variations in header naming)
_ID_COLS    = ["id", "ID", "Id", "respondent_id"]
_SEX_COLS   = ["sex", "Sex", "SEX"]
_AGE_COLS   = ["age", "Age", "AGE"]
_AGEG_COLS  = ["ageg", "AgeG", "age_g", "AGEG", "age_group"]
_AGEDSC_COLS= ["agegdesc", "AgeGDesc", "age_g_desc", "ageGDesc"]
_DAY_COLS   = ["day", "Day", "DAY"]
_REGION_COLS= ["region", "Region", "REGION"]
_WTNTR_COLS = ["wtntr", "WtNtr", "wt_ntr", "weight", "WTNTR"]
_FCODE_COLS = ["fcode", "FCode", "f_code", "food_code", "FCODE"]
_INTK_COLS  = ["nfintk", "NfIntk", "nf_intk", "intake", "NFINTK"]
_FFQ_COLS   = ["ffq", "Ffq", "FFQ"]
_HOINCM_COLS= ["hoincm", "HoIncm", "ho_incm", "income", "HOINCM"]

_AGE_G_MAP = {
    1: "1-8세", 2: "9-18세", 3: "19-29세", 4: "30-49세",
    5: "50-64세", 6: "65-74세", 7: "75세 이상", 8: "전체"
}


def _first(df: pd.DataFrame, candidates: list[str]) -> Optional[str]:
    for c in candidates:
        if c in df.columns:
            return c
    return None


def _get(df: pd.DataFrame, candidates: list[str], default=None) -> pd.Series:
    col = _first(df, candidates)
    if col:
        return df[col]
    if default is not None:
        return pd.Series([default] * len(df))
    raise KeyError(f"None of {candidates} found in columns: {list(df.columns)}")


def read_raw_file(path: Path) -> pd.DataFrame:
    """Read CSV or XLSX into a DataFrame (auto-detect format)."""
    suffix = path.suffix.lower()
    if suffix == ".csv":
        for enc in ("utf-8-sig", "cp949", "utf-8"):
            try:
                return pd.read_csv(path, encoding=enc, dtype=str)
            except UnicodeDecodeError:
                continue
        return pd.read_csv(path, encoding="latin1", dtype=str)
    elif suffix in (".xlsx", ".xls"):
        return pd.read_excel(path, dtype=str)
    else:
        raise ValueError(f"Unsupported file type: {suffix}")


def parse_survey_records(path: Path) -> List[SurveyRecord]:
    """Parse x0 or x1 survey data file into SurveyRecord list."""
    df = read_raw_file(path)
    df.columns = [c.strip() for c in df.columns]

    records: List[SurveyRecord] = []
    for _, row in df.iterrows():
        def g(candidates, default=""):
            col = _first(df, candidates)
            return str(row[col]).strip() if col else str(default)

        def gf(candidates, default=0.0):
            col = _first(df, candidates)
            try:
                return float(str(row[col]).strip()) if col else default
            except (ValueError, TypeError):
                return default

        def gi(candidates, default=0):
            col = _first(df, candidates)
            try:
                return int(float(str(row[col]).strip())) if col else default
            except (ValueError, TypeError):
                return default

        age_g = gi(_AGEG_COLS, 1)
        age_g_desc = g(_AGEDSC_COLS) or _AGE_G_MAP.get(age_g, "")
        ho_incm = gi(_HOINCM_COLS, 1)
        ho_incm = max(1, min(4, ho_incm)) if ho_incm else 1

        records.append(SurveyRecord(
            id=g(_ID_COLS),
            sex=gi(_SEX_COLS, 1),
            age=gi(_AGE_COLS, 0),
            age_g=age_g,
            age_g_desc=age_g_desc,
            day=gi(_DAY_COLS, 1),
            region=gi(_REGION_COLS, 0),
            wt_ntr=gf(_WTNTR_COLS, 1.0),
            f_code=g(_FCODE_COLS),
            nf_intk=gf(_INTK_COLS, 0.0),
            ffq=gf(_FFQ_COLS, 0.0),
            ho_incm=ho_incm,
        ))
    return records


def parse_codebook(path: Path) -> List[FoodCodeEntry]:
    """Parse codebook XLSX into FoodCodeEntry list."""
    df = read_raw_file(path)
    df.columns = [c.strip() for c in df.columns]

    # Column detection
    _CODE_C    = _first(df, ["code", "Code", "CODE", "1차코드", "식품코드"])
    _CNAME_C   = _first(df, ["CodeName", "code_name", "코드명"])
    _FGROUP_C  = _first(df, ["FoodGroup", "food_group", "식품군", "FOODGROUP"])
    _FNAME_C   = _first(df, ["FoodName", "food_name", "식품명", "FOODNAME"])
    _MCODE_C   = _first(df, ["MimsCode", "mims_code", "MIMS코드"])
    _MNAME_C   = _first(df, ["MimsName", "mims_name", "MIMS명"])
    _NO_C      = _first(df, ["No", "no", "번호"])
    _SUB1_C    = _first(df, ["SubCat1", "sub_cat1", "소분류1"])
    _SUB2_C    = _first(df, ["SubCat2", "sub_cat2", "소분류2"])

    entries: List[FoodCodeEntry] = []
    for _, row in df.iterrows():
        def g(col):
            return str(row[col]).strip() if col and col in df.columns else ""

        code = g(_CODE_C)
        if not code or code in ("nan", "None"):
            continue
        entries.append(FoodCodeEntry(
            no=g(_NO_C),
            code=code,
            code_name=g(_CNAME_C),
            food_group=g(_FGROUP_C),
            mims_code=g(_MCODE_C),
            mims_name=g(_MNAME_C),
            food_name=g(_FNAME_C),
            sub_cat1=g(_SUB1_C),
            sub_cat2=g(_SUB2_C),
        ))
    return entries


def count_rows(path: Path) -> int:
    """Quick row count without full parse."""
    try:
        df = read_raw_file(path)
        return len(df)
    except Exception:
        return 0


def read_bytes(path: Path) -> bytes:
    return path.read_bytes()
