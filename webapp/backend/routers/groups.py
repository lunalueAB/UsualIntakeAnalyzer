import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent.parent))

from datetime import datetime
from typing import List
from fastapi import APIRouter, HTTPException
from fastapi.responses import Response

from models import FoodGroup, FoodGroupCreate
import storage

router = APIRouter()

@router.get("", response_model=List[FoodGroup])
def list_groups():
    return storage.load_groups()

@router.post("", response_model=FoodGroup)
def create_group(body: FoodGroupCreate):
    g = FoodGroup(**body.model_dump())
    storage.add_group(g)
    return g

@router.put("/{gid}", response_model=FoodGroup)
def update_group(gid: str, body: FoodGroup):
    body.id = gid
    body.updated_at = datetime.now()
    ok = storage.update_group(body)
    if not ok:
        raise HTTPException(404)
    return body

@router.delete("/{gid}", status_code=204)
def delete_group(gid: str):
    ok = storage.remove_group(gid)
    if not ok:
        raise HTTPException(404)
    return Response(status_code=204)
