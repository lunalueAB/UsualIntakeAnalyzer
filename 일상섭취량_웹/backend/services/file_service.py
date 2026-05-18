"""CSV / XLSX 파일 파싱 서비스"""
import pandas as pd
import io
from pathlib import Path
from database import UPL_DIR
from typing import List

# 컬럼 매핑: 원시 컬럼명 → 내부 필드명
COL_MAP = {
    'id':           ['id','ID','응답자ID','hhid','respondent_id'],
    'sex':          ['sex','성별','SEX','gender'],
    'age':          ['age','나이','연령','AGE'],
    'age_g':        ['age_g','ageg','연령군코드','age_group'],
    'age_g_desc':   ['age_g_desc','agegdesc','연령군','age_group_desc'],
    'day':          ['day','조사일','DAY','survey_day'],
    'region':       ['region','지역','REGION'],
    'wt_ntr':       ['wt_ntr','wtntr','영양가중치','nutrition_weight','weight'],
    'fcode':        ['fcode','식품코드','FCODE','food_code'],
    'nf_intk':      ['nf_intk','nfintk','섭취량','intake','NfIntk'],
    'ffq':          ['ffq','FFQ'],
    'ho_incm':      ['ho_incm','hoincm','소득분위','income'],
    'town_t':       ['town_t','townt','읍면동','town'],
    'edu':          ['edu','EDU','교육수준','education'],
    'genertn_type': ['genertn_type','세대유형','generation_type'],
    'region_type':  ['region_type','지역유형','region_type'],
}

def _resolve_columns(df: pd.DataFrame) -> dict:
    """실제 컬럼명을 내부 필드명으로 매핑"""
    col_lower = {c.lower().strip(): c for c in df.columns}
    mapping = {}
    for field, candidates in COL_MAP.items():
        for cand in candidates:
            if cand.lower() in col_lower:
                mapping[field] = col_lower[cand.lower()]
                break
    return mapping


def parse_file(filepath: str) -> List[dict]:
    """CSV or XLSX → list of dicts (normalized field names)"""
    p = Path(filepath)
    if p.suffix.lower() == '.xlsx':
        df = pd.read_excel(p, dtype=str)
    else:
        try:
            df = pd.read_csv(p, dtype=str, encoding='utf-8-sig')
        except UnicodeDecodeError:
            df = pd.read_csv(p, dtype=str, encoding='cp949')

    df.columns = [c.strip() for c in df.columns]
    mapping = _resolve_columns(df)

    records = []
    for _, row in df.iterrows():
        rec = {
            'id':           str(row[mapping['id']]).strip()   if 'id'    in mapping else '',
            'sex':          _int(row, mapping, 'sex', 1),
            'age':          _int(row, mapping, 'age', 0),
            'age_g':        _int(row, mapping, 'age_g', 1),
            'age_g_desc':   str(row[mapping['age_g_desc']]).strip() if 'age_g_desc' in mapping else '',
            'day':          _int(row, mapping, 'day', 1),
            'region':       _int(row, mapping, 'region', 0),
            'wt_ntr':       _float(row, mapping, 'wt_ntr', 1.0),
            'fcode':        str(row[mapping['fcode']]).strip()  if 'fcode' in mapping else '',
            'nf_intk':      _float(row, mapping, 'nf_intk', 0.0),
            'ffq':          _float(row, mapping, 'ffq', 0.0),
            'ho_incm':      _int(row, mapping, 'ho_incm', 1),
            'town_t':       str(row[mapping['town_t']]).strip() if 'town_t' in mapping else '',
            'edu':          str(row[mapping['edu']]).strip()    if 'edu'   in mapping else '',
            'genertn_type': str(row[mapping['genertn_type']]).strip() if 'genertn_type' in mapping else '',
            'region_type':  str(row[mapping['region_type']]).strip()  if 'region_type'  in mapping else '',
        }
        records.append(rec)
    return records


def save_upload(file_bytes: bytes, original_name: str) -> tuple[str, int]:
    """파일 저장 → (저장된 파일경로, 행수)"""
    import uuid
    from pathlib import Path
    suffix = Path(original_name).suffix.lower()
    fname  = f"{uuid.uuid4().hex}{suffix}"
    dest   = UPL_DIR / fname
    dest.write_bytes(file_bytes)
    records = parse_file(str(dest))
    return fname, len(records)


def _int(row, mapping, field, default):
    try: return int(float(str(row[mapping[field]])))
    except: return default

def _float(row, mapping, field, default):
    try: return float(str(row[mapping[field]]))
    except: return default
