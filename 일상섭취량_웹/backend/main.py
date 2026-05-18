from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from database import init_db
from routers import datasets, sources, groups, scenarios, analysis, presets

app = FastAPI(title="일상섭취량 분석 API", version="2.4.1")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:3000", "http://127.0.0.1:3000"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(datasets.router,  prefix="/datasets",  tags=["datasets"])
app.include_router(sources.router,   prefix="/sources",   tags=["sources"])
app.include_router(groups.router,    prefix="/groups",    tags=["groups"])
app.include_router(scenarios.router, prefix="/scenarios", tags=["scenarios"])
app.include_router(analysis.router,  prefix="/analysis",  tags=["analysis"])
app.include_router(presets.router,   prefix="/presets",   tags=["presets"])

@app.on_event("startup")
def startup():
    init_db()

@app.get("/")
def root():
    return {"status": "ok", "app": "일상섭취량 분석 API", "version": "2.4.1"}
