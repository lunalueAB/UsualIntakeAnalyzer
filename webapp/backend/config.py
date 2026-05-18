"""App-wide configuration and paths."""
import os
from pathlib import Path

# WPF app data directory (same location the C# app uses)
APPDATA = Path(os.environ.get("APPDATA", Path.home() / "AppData" / "Roaming"))
DATA_DIR = APPDATA / "UsualIntakeAnalyzer"

# JSON metadata files
DATASETS_FILE   = DATA_DIR / "datasets.json"
CODEBOOK_FILE   = DATA_DIR / "codebook.json"
PROJECTS_FILE   = DATA_DIR / "survey_projects.json"
PHASES_FILE     = DATA_DIR / "survey_phases.json"
ROUNDS_FILE     = DATA_DIR / "survey_rounds.json"
ACTIVE_SRC_FILE = DATA_DIR / "active_source.json"
GROUPS_FILE     = DATA_DIR / "food_groups.json"
GROUPS_VER_FILE = DATA_DIR / "food_groups_version.txt"
SCENARIOS_FILE  = DATA_DIR / "scenarios.json"

# Raw data files are stored under DATA_DIR/data/
RAW_DATA_DIR    = DATA_DIR / "data"

# Built-in frontend static files (populated by build step)
FRONTEND_DIST   = Path(__file__).parent.parent / "frontend" / "dist"

# Server
HOST = "127.0.0.1"
PORT = 7788

def ensure_dirs():
    DATA_DIR.mkdir(parents=True, exist_ok=True)
    RAW_DATA_DIR.mkdir(parents=True, exist_ok=True)
