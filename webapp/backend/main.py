"""FastAPI entry point — serves API + built React frontend."""
import sys
import threading
import time
import webbrowser
from pathlib import Path

import uvicorn
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles

# Add backend dir to path
sys.path.insert(0, str(Path(__file__).parent))

from config import HOST, PORT, FRONTEND_DIST, ensure_dirs
from routers import datasets, sources, groups, analysis

app = FastAPI(title="일상섭취량 분석 프로그램", version="2.5.0")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(datasets.router, prefix="/api/datasets", tags=["datasets"])
app.include_router(sources.router,  prefix="/api/sources",  tags=["sources"])
app.include_router(groups.router,   prefix="/api/groups",   tags=["groups"])
app.include_router(analysis.router, prefix="/api/analysis", tags=["analysis"])

# Serve built React frontend
if FRONTEND_DIST.exists():
    app.mount("/assets", StaticFiles(directory=str(FRONTEND_DIST / "assets")), name="assets")

    @app.get("/{full_path:path}", include_in_schema=False)
    async def spa(full_path: str):
        return FileResponse(str(FRONTEND_DIST / "index.html"))
else:
    @app.get("/", include_in_schema=False)
    async def root():
        return {
            "status": "API is running",
            "message": "프론트엔드를 빌드하세요: cd frontend && npm install && npm run build",
            "docs": f"http://{HOST}:{PORT}/docs",
        }


def _open_browser():
    time.sleep(1.5)
    webbrowser.open(f"http://{HOST}:{PORT}")


if __name__ == "__main__":
    ensure_dirs()
    if "--no-browser" not in sys.argv:
        threading.Thread(target=_open_browser, daemon=True).start()
    uvicorn.run(app, host=HOST, port=PORT, log_level="warning")
