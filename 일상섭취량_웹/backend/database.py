import sqlite3, os, json
from pathlib import Path

DB_PATH  = Path(__file__).parent / "data" / "app.db"
UPL_DIR  = Path(__file__).parent / "data" / "uploads"

def get_conn():
    DB_PATH.parent.mkdir(parents=True, exist_ok=True)
    UPL_DIR.mkdir(parents=True, exist_ok=True)
    conn = sqlite3.connect(str(DB_PATH))
    conn.row_factory = sqlite3.Row
    conn.execute("PRAGMA journal_mode=WAL")
    conn.execute("PRAGMA foreign_keys=ON")
    return conn

def init_db():
    with get_conn() as conn:
        conn.executescript("""
        CREATE TABLE IF NOT EXISTS survey_projects (
            id TEXT PRIMARY KEY,
            name_ko TEXT NOT NULL,
            project_code TEXT NOT NULL,
            name_en TEXT DEFAULT '',
            conducting_org TEXT DEFAULT '',
            commission_org TEXT DEFAULT '',
            survey_domain TEXT DEFAULT '',
            is_builtin INTEGER DEFAULT 0,
            created_at TEXT DEFAULT (datetime('now','localtime'))
        );
        CREATE TABLE IF NOT EXISTS survey_phases (
            id TEXT PRIMARY KEY,
            project_id TEXT NOT NULL REFERENCES survey_projects(id) ON DELETE CASCADE,
            phase_no INTEGER NOT NULL,
            phase_label TEXT NOT NULL,
            year_start INTEGER,
            year_end INTEGER,
            status TEXT DEFAULT 'active',
            sample_size INTEGER DEFAULT 0,
            notes TEXT DEFAULT '',
            is_builtin INTEGER DEFAULT 0,
            created_at TEXT DEFAULT (datetime('now','localtime'))
        );
        CREATE TABLE IF NOT EXISTS survey_rounds (
            id TEXT PRIMARY KEY,
            phase_id TEXT NOT NULL REFERENCES survey_phases(id) ON DELETE CASCADE,
            round_no INTEGER NOT NULL,
            display_label TEXT NOT NULL,
            status TEXT DEFAULT 'active',
            survey_year INTEGER,
            notes TEXT DEFAULT '',
            is_builtin INTEGER DEFAULT 0,
            created_at TEXT DEFAULT (datetime('now','localtime'))
        );
        CREATE TABLE IF NOT EXISTS datasets (
            id TEXT PRIMARY KEY,
            type TEXT NOT NULL CHECK(type IN ('X0','X1')),
            round_id TEXT DEFAULT '',
            filename TEXT NOT NULL,
            original_filename TEXT NOT NULL,
            description TEXT DEFAULT '',
            registered_by TEXT DEFAULT '',
            row_count INTEGER DEFAULT 0,
            registered_at TEXT DEFAULT (datetime('now','localtime'))
        );
        CREATE TABLE IF NOT EXISTS food_groups (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL UNIQUE,
            memo TEXT DEFAULT '',
            is_builtin INTEGER DEFAULT 0,
            created_at TEXT DEFAULT (datetime('now','localtime'))
        );
        CREATE TABLE IF NOT EXISTS food_group_codes (
            group_id TEXT NOT NULL REFERENCES food_groups(id) ON DELETE CASCADE,
            fcode TEXT NOT NULL,
            food_name TEXT DEFAULT '',
            PRIMARY KEY (group_id, fcode)
        );
        CREATE TABLE IF NOT EXISTS food_presets (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL UNIQUE,
            description TEXT DEFAULT '',
            food_codes TEXT DEFAULT '[]',
            food_names TEXT DEFAULT '[]',
            is_builtin INTEGER DEFAULT 0,
            created_at TEXT DEFAULT (datetime('now','localtime')),
            updated_at TEXT DEFAULT (datetime('now','localtime')),
            last_analyzed_at TEXT
        );
        CREATE TABLE IF NOT EXISTS scenarios (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL,
            food_group_id TEXT DEFAULT '',
            food_names TEXT DEFAULT '[]',
            food_codes TEXT DEFAULT '[]',
            x1_ids TEXT DEFAULT '[]',
            x0_ids TEXT DEFAULT '[]',
            sim_time INTEGER DEFAULT 5,
            registered_by TEXT DEFAULT '',
            registered_at TEXT DEFAULT (datetime('now','localtime')),
            last_analyzed_at TEXT
        );
        CREATE TABLE IF NOT EXISTS analysis_results (
            id TEXT PRIMARY KEY,
            scenario_id TEXT NOT NULL,
            rho_p REAL, rho_a REAL, papa REAL, zero_prevalence REAL,
            sigma_b2 REAL, sigma_w2 REAL, reliability REAL,
            log_transformed INTEGER DEFAULT 0,
            gamma_shape REAL, gamma_scale REAL,
            method_used TEXT, method_note TEXT,
            result_table TEXT DEFAULT '[]',
            person_intakes TEXT DEFAULT '[]',
            additional_result TEXT,
            created_at TEXT DEFAULT (datetime('now','localtime'))
        );
        """)
        _migrate_db(conn)
        _seed_defaults(conn)

def _migrate_db(conn):
    # ALTER TABLE ADD COLUMN - 이미 있으면 무시
    migrations = [
        "ALTER TABLE survey_projects ADD COLUMN name_en TEXT DEFAULT ''",
        "ALTER TABLE survey_projects ADD COLUMN conducting_org TEXT DEFAULT ''",
        "ALTER TABLE survey_projects ADD COLUMN commission_org TEXT DEFAULT ''",
        "ALTER TABLE survey_projects ADD COLUMN survey_domain TEXT DEFAULT ''",
        "ALTER TABLE survey_projects ADD COLUMN is_builtin INTEGER DEFAULT 0",
        "ALTER TABLE survey_phases ADD COLUMN sample_size INTEGER DEFAULT 0",
        "ALTER TABLE survey_phases ADD COLUMN notes TEXT DEFAULT ''",
        "ALTER TABLE survey_phases ADD COLUMN is_builtin INTEGER DEFAULT 0",
        "ALTER TABLE survey_rounds ADD COLUMN survey_year INTEGER",
        "ALTER TABLE survey_rounds ADD COLUMN notes TEXT DEFAULT ''",
        "ALTER TABLE survey_rounds ADD COLUMN is_builtin INTEGER DEFAULT 0",
    ]
    for sql in migrations:
        try:
            conn.execute(sql)
        except Exception:
            pass
    # datasets type CHECK: X0/X1만 허용 (CB/PR 제거) — 구버전 DB 마이그레이션
    try:
        test_id = '__type_check_test__'
        conn.execute("INSERT INTO datasets(id,type,filename,original_filename) VALUES(?,?,?,?)",
                     (test_id,'CB','__test__','__test__'))
        conn.execute("DELETE FROM datasets WHERE id=?", (test_id,))
        # CB 삽입 성공 → 구버전 CHECK → X0/X1만 허용하도록 재생성
        conn.executescript("""
        CREATE TABLE IF NOT EXISTS datasets_new (
            id TEXT PRIMARY KEY,
            type TEXT NOT NULL CHECK(type IN ('X0','X1')),
            round_id TEXT DEFAULT '',
            filename TEXT NOT NULL,
            original_filename TEXT NOT NULL,
            description TEXT DEFAULT '',
            registered_by TEXT DEFAULT '',
            row_count INTEGER DEFAULT 0,
            registered_at TEXT DEFAULT (datetime('now','localtime'))
        );
        INSERT INTO datasets_new
            SELECT id,
                CASE WHEN type IN ('X0','X1') THEN type ELSE 'X1' END,
                round_id, filename, original_filename,
                description, registered_by, row_count, registered_at
            FROM datasets;
        DROP TABLE datasets;
        ALTER TABLE datasets_new RENAME TO datasets;
        """)
    except Exception:
        pass  # CB 삽입 실패 → 이미 X0/X1만 허용 중, 정상

def _seed_defaults(conn):
    if conn.execute("SELECT COUNT(*) FROM survey_projects").fetchone()[0] > 0:
        return
    import uuid
    p_id = str(uuid.uuid4())

    # 7기 (2016~2018)
    ph7_id  = str(uuid.uuid4())
    r7_1_id = str(uuid.uuid4())
    r7_2_id = str(uuid.uuid4())
    r7_3_id = str(uuid.uuid4())

    # 8기 (2019~2021)
    ph8_id  = str(uuid.uuid4())
    r8_1_id = str(uuid.uuid4())
    r8_2_id = str(uuid.uuid4())
    r8_3_id = str(uuid.uuid4())

    # 9기 (2022~현재)
    ph9_id  = str(uuid.uuid4())
    r9_1_id = str(uuid.uuid4())
    r9_2_id = str(uuid.uuid4())

    conn.execute(
        "INSERT INTO survey_projects(id,name_ko,project_code,name_en,conducting_org,commission_org,survey_domain,is_builtin,created_at) "
        "VALUES (?,?,?,?,?,?,?,?,datetime('now','localtime'))",
        (p_id, '국민건강영양조사', 'KNHANES',
         'Korea National Health and Nutrition Examination Survey',
         '질병관리청', '보건복지부', '영양', 1))

    # 7기
    conn.execute(
        "INSERT INTO survey_phases(id,project_id,phase_no,phase_label,year_start,year_end,status,is_builtin,created_at) "
        "VALUES (?,?,7,'7기',2016,2018,'active',1,datetime('now','localtime'))",
        (ph7_id, p_id))
    conn.execute(
        "INSERT INTO survey_rounds(id,phase_id,round_no,display_label,status,survey_year,is_builtin,created_at) "
        "VALUES (?,?,1,'1차(2016)','active',2016,1,datetime('now','localtime'))",
        (r7_1_id, ph7_id))
    conn.execute(
        "INSERT INTO survey_rounds(id,phase_id,round_no,display_label,status,survey_year,is_builtin,created_at) "
        "VALUES (?,?,2,'2차(2017)','active',2017,1,datetime('now','localtime'))",
        (r7_2_id, ph7_id))
    conn.execute(
        "INSERT INTO survey_rounds(id,phase_id,round_no,display_label,status,survey_year,is_builtin,created_at) "
        "VALUES (?,?,3,'3차(2018)','active',2018,1,datetime('now','localtime'))",
        (r7_3_id, ph7_id))

    # 8기
    conn.execute(
        "INSERT INTO survey_phases(id,project_id,phase_no,phase_label,year_start,year_end,status,is_builtin,created_at) "
        "VALUES (?,?,8,'8기',2019,2021,'active',1,datetime('now','localtime'))",
        (ph8_id, p_id))
    conn.execute(
        "INSERT INTO survey_rounds(id,phase_id,round_no,display_label,status,survey_year,is_builtin,created_at) "
        "VALUES (?,?,1,'1차(2019)','active',2019,1,datetime('now','localtime'))",
        (r8_1_id, ph8_id))
    conn.execute(
        "INSERT INTO survey_rounds(id,phase_id,round_no,display_label,status,survey_year,is_builtin,created_at) "
        "VALUES (?,?,2,'2차(2020)','active',2020,1,datetime('now','localtime'))",
        (r8_2_id, ph8_id))
    conn.execute(
        "INSERT INTO survey_rounds(id,phase_id,round_no,display_label,status,survey_year,is_builtin,created_at) "
        "VALUES (?,?,3,'3차(2021)','active',2021,1,datetime('now','localtime'))",
        (r8_3_id, ph8_id))

    # 9기 (2022~현재) - 기존 2022, 2023 시드 유지
    conn.execute(
        "INSERT INTO survey_phases(id,project_id,phase_no,phase_label,year_start,year_end,status,is_builtin,created_at) "
        "VALUES (?,?,9,'9기',2022,NULL,'active',1,datetime('now','localtime'))",
        (ph9_id, p_id))
    conn.execute(
        "INSERT INTO survey_rounds(id,phase_id,round_no,display_label,status,survey_year,is_builtin,created_at) "
        "VALUES (?,?,1,'1차(2022)','active',2022,1,datetime('now','localtime'))",
        (r9_1_id, ph9_id))
    conn.execute(
        "INSERT INTO survey_rounds(id,phase_id,round_no,display_label,status,survey_year,is_builtin,created_at) "
        "VALUES (?,?,2,'2차(2023)','active',2023,1,datetime('now','localtime'))",
        (r9_2_id, ph9_id))

    groups = [
        ('채소류', [('V001','배추김치'),('V002','시금치'),('V003','콩나물'),('V004','상추'),('V005','무'),('V006','당근'),('V007','양파')]),
        ('과일류', [('F001','사과'),('F002','배'),('F003','귤'),('F004','바나나'),('F005','포도')]),
        ('곡류',   [('G001','백미밥'),('G002','현미밥'),('G003','식빵'),('G004','라면'),('G005','국수')]),
        ('육류',   [('M001','소고기'),('M002','돼지고기'),('M003','닭고기'),('M004','오리고기')]),
        ('어패류', [('S001','고등어'),('S002','갈치'),('S003','명태'),('S004','새우'),('S005','오징어')]),
        ('두류',   [('B001','두부'),('B002','순두부'),('B003','된장'),('B004','두유')]),
    ]
    for gname, codes in groups:
        gid = str(uuid.uuid4())
        conn.execute("INSERT INTO food_groups VALUES (?,?,''  ,1,datetime('now','localtime'))",(gid,gname))
        for fcode, fname in codes:
            conn.execute("INSERT INTO food_group_codes VALUES (?,?,?)",(gid,fcode,fname))
