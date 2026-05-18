import React,{useState,useEffect,useCallback,useMemo} from 'react'
import {Btn,Input,Select,Checkbox,Badge,Modal} from '../components/UI'
import {api} from '../api'

// ── 유틸: source_label 파싱 ─────────────────────────────────────────────────
// source_label 형식: "사업명 · 기수 · 차수"  (·로 split)
function parseSourceLabel(label){
  if(!label||label==='(전역)'||label==='—')return{project:label||'',phase:'',round:''}
  const parts=label.split('·').map(s=>s.trim())
  if(parts.length>=3)return{project:parts[0],phase:parts[1],round:parts[2]}
  if(parts.length===2)return{project:parts[0],phase:'',round:parts[1]}
  return{project:parts[0],phase:'',round:''}
}

// ── 배지 색상 ────────────────────────────────────────────────────────────────
function kindBadge(type){
  if(type==='X1')return'blue'
  if(type==='X0')return'green'
  return'gray'
}
function kindLabel(type){
  if(type==='X1')return'1일 조사'
  if(type==='X0')return'2일 조사'
  return type
}
function fmtNum(n){return(n||0).toLocaleString()}
function fmtDt(s){
  if(!s)return''
  const d=new Date(s)
  if(isNaN(d))return s.slice(0,16).replace('T',' ')
  const pad=n=>String(n).padStart(2,'0')
  return`${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

// ── 재지정 모달 ─────────────────────────────────────────────────────────────
function ReassignModal({sources,dataset,onClose,onDone}){
  const [projId,setProjId]=useState('')
  const [phaseId,setPhaseId]=useState('')
  const [roundId,setRoundId]=useState('')
  const [loading,setLoading]=useState(false)
  const [error,setError]=useState('')
  const phases=sources.find(p=>p.id===projId)?.phases||[]
  const rounds=phases.find(p=>p.id===phaseId)?.rounds||[]
  const handleSubmit=async()=>{
    if(!roundId){setError('차수를 선택해 주세요.');return}
    setLoading(true);setError('')
    try{
      await api.updateDataset(dataset.id,{round_id:roundId})
      onDone()
    }catch(e){setError(e?.response?.data?.detail||'재지정 오류')}
    finally{setLoading(false)}
  }
  return <Modal title="자료원 재지정" subtitle={`선택 파일: ${dataset.original_filename}`} onClose={onClose} width={460}>
    <div style={{display:'flex',flexDirection:'column',gap:12,marginTop:14}}>
      {error&&<div style={{background:'#FEE2E2',color:'#B91C1C',borderRadius:4,padding:'8px 12px',fontSize:12}}>{error}</div>}
      {!dataset.is_orphan&&dataset.source_label&&
        <div style={{background:'#FEF9C3',border:'1px solid #FDE047',borderRadius:4,padding:'8px 12px',fontSize:12,color:'#92400E'}}>
          현재 자료원: <strong>{dataset.source_label}</strong><br/>그래도 변경하려면 아래에서 새 차수를 선택하세요.
        </div>}
      <div><div style={{fontSize:12,fontWeight:600,marginBottom:4}}>대분류 (사업)</div>
        <Select value={projId} onChange={v=>{setProjId(v);setPhaseId('');setRoundId('')}}>
          <option value="">사업 선택</option>
          {sources.map(p=><option key={p.id} value={p.id}>{p.name_ko}</option>)}
        </Select></div>
      <div><div style={{fontSize:12,fontWeight:600,marginBottom:4}}>기수</div>
        <Select value={phaseId} onChange={v=>{setPhaseId(v);setRoundId('')}}>
          <option value="">기수 선택</option>
          {phases.map(p=><option key={p.id} value={p.id}>{p.phase_label}</option>)}
        </Select></div>
      <div><div style={{fontSize:12,fontWeight:600,marginBottom:4}}>차수 *</div>
        <Select value={roundId} onChange={setRoundId}>
          <option value="">차수 선택</option>
          {rounds.map(r=><option key={r.id} value={r.id}>{r.display_label}</option>)}
        </Select></div>
      <div style={{display:'flex',justifyContent:'flex-end',gap:8,paddingTop:8,borderTop:'1px solid #E2E8F0'}}>
        <Btn variant="secondary" onClick={onClose}>취소</Btn>
        <Btn onClick={handleSubmit} disabled={loading||!roundId}>{loading?'처리 중...':'확인'}</Btn>
      </div>
    </div>
  </Modal>
}

// ── 업로드 모달 ─────────────────────────────────────────────────────────────
function UploadModal({sources,onClose,onDone}){
  const [kind,setKind]=useState('X1')
  const [file,setFile]=useState(null)
  const [projId,setProjId]=useState('')
  const [phaseId,setPhaseId]=useState('')
  const [roundId,setRoundId]=useState('')
  const [desc,setDesc]=useState('')
  const [by,setBy]=useState('')
  const [loading,setLoading]=useState(false)
  const [error,setError]=useState('')
  const phases=sources.find(p=>p.id===projId)?.phases||[]
  const rounds=phases.find(p=>p.id===phaseId)?.rounds||[]
  const fileRef=React.useRef()
  const needRound=true
  const handleSubmit=async()=>{
    if(!file){setError('파일을 선택해 주세요.');return}
    if(needRound&&!roundId){setError('차수를 선택해 주세요.');return}
    setLoading(true);setError('')
    try{
      const fd=new FormData()
      fd.append('file',file)
      fd.append('type',kind)
      if(needRound)fd.append('round_id',roundId)
      fd.append('description',desc)
      fd.append('registered_by',by)
      await api.uploadDataset(fd)
      onDone()
    }catch(e){setError(e?.response?.data?.detail||'업로드 오류')}
    finally{setLoading(false)}
  }
  return <Modal title="데이터 업로드" subtitle="종류와 자료원(대분류·기수·차수)을 선택한 뒤 파일을 등록" onClose={onClose} width={500}>
    <div style={{display:'flex',flexDirection:'column',gap:14,marginTop:16}}>
      {error&&<div style={{background:'#FEE2E2',color:'#B91C1C',borderRadius:4,padding:'8px 12px',fontSize:12}}>{error}</div>}
      <div>
        <div style={{fontSize:12,fontWeight:600,marginBottom:6}}>데이터 종류 *</div>
        <div style={{display:'flex',alignItems:'center',gap:10}}>
          <Select value={kind} onChange={v=>{setKind(v);setProjId('');setPhaseId('');setRoundId('')}} style={{flex:1}}>
            <option value="X1">1일 조사 데이터</option>
            <option value="X0">2일 조사 데이터</option>
          </Select>
        </div>
      </div>
      <div><div style={{fontSize:12,fontWeight:600,marginBottom:6}}>자료원 (차수) *</div>
          <div style={{display:'flex',flexDirection:'column',gap:6}}>
            <Select value={projId} onChange={v=>{setProjId(v);setPhaseId('');setRoundId('')}}>
              <option value="">사업 선택</option>
              {sources.map(p=><option key={p.id} value={p.id}>{p.name_ko}</option>)}
            </Select>
            <Select value={phaseId} onChange={v=>{setPhaseId(v);setRoundId('')}}>
              <option value="">기수 선택</option>
              {phases.map(p=><option key={p.id} value={p.id}>{p.phase_label}</option>)}
            </Select>
            <Select value={roundId} onChange={setRoundId}>
              <option value="">차수 선택</option>
              {rounds.map(r=><option key={r.id} value={r.id}>{r.display_label}</option>)}
            </Select>
          </div></div>
      <div><div style={{fontSize:12,fontWeight:600,marginBottom:6}}>파일 선택 *</div>
        <input type="file" ref={fileRef} accept=".csv,.xlsx" style={{display:'none'}}
          onChange={e=>setFile(e.target.files[0]||null)}/>
        <div onClick={()=>fileRef.current.click()} style={{border:'2px dashed #CBD5E1',
          borderRadius:6,padding:'20px',textAlign:'center',background:'#F8FAFC',cursor:'pointer'}}>
          {file?<><div style={{fontSize:20}}>📄</div><div style={{color:'#2563EB',fontWeight:600,marginTop:4}}>{file.name}</div></>
            :<><div style={{fontSize:20}}>📂</div><div style={{color:'#94A3B8',fontSize:12,marginTop:4}}>클릭하여 파일 선택</div>
               <div style={{color:'#CBD5E1',fontSize:11,marginTop:2}}>CSV, XLSX 지원</div></>}
        </div></div>
      <div><div style={{fontSize:12,fontWeight:600,marginBottom:6}}>설명</div>
        <Input value={desc} onChange={setDesc} placeholder="간단한 설명 (선택)"/></div>
      <div><div style={{fontSize:12,fontWeight:600,marginBottom:6}}>등록자</div>
        <Input value={by} onChange={setBy} placeholder="이름 입력 (선택)"/></div>
      <div style={{display:'flex',justifyContent:'flex-end',gap:8,paddingTop:8,borderTop:'1px solid #E2E8F0'}}>
        <Btn variant="secondary" onClick={onClose}>취소</Btn>
        <Btn onClick={handleSubmit} disabled={loading||!file||(needRound&&!roundId)}>{loading?'업로드 중...':'저장'}</Btn>
      </div>
    </div>
  </Modal>
}

// ── 자료원 관리 모달 ─────────────────────────────────────────────────────────
function SourceManageModal({sources,onClose,onDone}){
  const [open,setOpen]=useState({})
  const toggle=id=>setOpen(o=>({...o,[id]:!o[id]}))
  const [sel,setSel]=useState(null)
  const [form,setForm]=useState({})
  const [adding,setAdding]=useState(null)
  const [loading,setLoading]=useState(false)
  useEffect(()=>{
    const o={};
    sources.forEach(p=>{o[p.id]=true;p.phases?.forEach(ph=>{o[ph.id]=true})});
    setOpen(o)
  },[sources])
  const handleAdd=async()=>{
    setLoading(true)
    try{
      if(adding==='project')await api.addProject({name_ko:form.name,project_code:form.code||''})
      else if(adding==='phase')await api.addPhase({project_id:form.pid,phase_no:parseInt(form.no)||1,phase_label:form.label,year_start:form.ys?parseInt(form.ys):null,year_end:form.ye?parseInt(form.ye):null})
      else if(adding==='round')await api.addRound({phase_id:form.phid,round_no:parseInt(form.no)||1,display_label:form.label})
      setAdding(null);setForm({});onDone()
    }catch(e){alert(e?.response?.data?.detail||'오류')}
    finally{setLoading(false)}
  }
  const handleDelete=async()=>{
    if(!sel||!window.confirm('삭제하시겠습니까?'))return
    setLoading(true)
    try{
      if(sel.type==='project')await api.deleteProject(sel.id)
      else if(sel.type==='phase')await api.deletePhase(sel.id)
      else if(sel.type==='round')await api.deleteRound(sel.id)
      setSel(null);onDone()
    }catch(e){alert(e?.response?.data?.detail||'삭제 오류')}
    finally{setLoading(false)}
  }
  return <Modal title="자료원 관리" subtitle="대분류(사업) → 기수 → 차수 추가/편집/삭제" onClose={onClose} width={520}>
    <div style={{marginTop:14}}>
      <div style={{display:'flex',gap:6,marginBottom:10}}>
        <Btn variant="secondary" small onClick={()=>{setAdding('project');setForm({})}}>＋ 사업</Btn>
        <Btn variant="secondary" small onClick={()=>{setAdding('phase');setForm({})}}>＋ 기수</Btn>
        <Btn variant="secondary" small onClick={()=>{setAdding('round');setForm({})}}>＋ 차수</Btn>
      </div>
      {adding&&<div style={{background:'#F8FAFC',border:'1px solid #E2E8F0',borderRadius:6,padding:'12px',marginBottom:10}}>
        <div style={{fontWeight:600,marginBottom:8,fontSize:12}}>{adding==='project'?'사업 추가':adding==='phase'?'기수 추가':'차수 추가'}</div>
        {adding==='project'&&<div style={{display:'flex',gap:6}}>
          <Input value={form.name||''} onChange={v=>setForm(f=>({...f,name:v}))} placeholder="사업명" style={{flex:1}}/>
          <Input value={form.code||''} onChange={v=>setForm(f=>({...f,code:v}))} placeholder="코드" style={{width:100}}/>
        </div>}
        {adding==='phase'&&<div style={{display:'flex',flexDirection:'column',gap:6}}>
          <Select value={form.pid||''} onChange={v=>setForm(f=>({...f,pid:v}))}>
            <option value="">사업 선택</option>
            {sources.map(p=><option key={p.id} value={p.id}>{p.name_ko}</option>)}
          </Select>
          <div style={{display:'flex',gap:6}}>
            <Input value={form.label||''} onChange={v=>setForm(f=>({...f,label:v}))} placeholder="기수명 (예: 2023년)" style={{flex:1}}/>
            <Input value={form.no||''} onChange={v=>setForm(f=>({...f,no:v}))} placeholder="순서" style={{width:60}} type="number"/>
          </div>
        </div>}
        {adding==='round'&&<div style={{display:'flex',flexDirection:'column',gap:6}}>
          <Select value={form.phid||''} onChange={v=>setForm(f=>({...f,phid:v}))}>
            <option value="">기수 선택</option>
            {sources.flatMap(p=>p.phases||[]).map(ph=><option key={ph.id} value={ph.id}>{ph.phase_label}</option>)}
          </Select>
          <div style={{display:'flex',gap:6}}>
            <Input value={form.label||''} onChange={v=>setForm(f=>({...f,label:v}))} placeholder="차수명 (예: 1차)" style={{flex:1}}/>
            <Input value={form.no||''} onChange={v=>setForm(f=>({...f,no:v}))} placeholder="순서" style={{width:60}} type="number"/>
          </div>
        </div>}
        <div style={{display:'flex',gap:6,marginTop:8}}>
          <Btn small onClick={handleAdd} disabled={loading}>저장</Btn>
          <Btn variant="secondary" small onClick={()=>{setAdding(null);setForm({})}}>취소</Btn>
        </div>
      </div>}
      <div style={{border:'1px solid #CBD5E1',borderRadius:6,background:'#F8FAFC',minHeight:260,maxHeight:300,overflow:'auto',padding:'6px 0'}}>
        {sources.map(p=><div key={p.id}>
          <div onClick={()=>{toggle(p.id);setSel({type:'project',id:p.id,data:p})}}
            style={{padding:'6px 14px',cursor:'pointer',display:'flex',alignItems:'center',gap:6,
              fontWeight:600,background:sel?.id===p.id?'#DBEAFE':'transparent'}}>
            <span style={{fontSize:10}}>{open[p.id]?'▼':'▶'}</span>📁 {p.name_ko}
            <span style={{color:'#94A3B8',fontWeight:400,fontSize:11}}>({p.project_code})</span>
          </div>
          {open[p.id]&&(p.phases||[]).map(ph=><div key={ph.id}>
            <div onClick={()=>{toggle(ph.id);setSel({type:'phase',id:ph.id,data:ph})}}
              style={{padding:'5px 14px 5px 30px',cursor:'pointer',display:'flex',alignItems:'center',gap:6,
                fontSize:12,color:'#475569',background:sel?.id===ph.id?'#DBEAFE':'transparent'}}>
              <span style={{fontSize:10}}>{open[ph.id]?'▼':'▶'}</span>📂 {ph.phase_label}
            </div>
            {open[ph.id]&&(ph.rounds||[]).map(r=><div key={r.id}
              onClick={()=>setSel({type:'round',id:r.id,data:r})}
              style={{padding:'4px 14px 4px 50px',fontSize:12,color:'#475569',cursor:'pointer',
                background:sel?.id===r.id?'#DBEAFE':'transparent'}}>
              📄 {r.display_label}
            </div>)}
          </div>)}
        </div>)}
      </div>
      <div style={{display:'flex',justifyContent:'space-between',marginTop:12,paddingTop:12,borderTop:'1px solid #E2E8F0'}}>
        <div style={{display:'flex',gap:6}}>
          <Btn variant="danger" small disabled={!sel||loading} onClick={handleDelete}>삭제</Btn>
        </div>
        <Btn variant="secondary" onClick={onClose}>닫기</Btn>
      </div>
    </div>
  </Modal>
}

// ── 식품군 편집 모달 (코드 목록 편집) ────────────────────────────────────────
function FoodGroupEditModal({group,onClose,onDone}){
  const [rows,setRows]=useState([])
  const [loading,setLoading]=useState(false)
  const [fetchLoading,setFetchLoading]=useState(true)
  const [error,setError]=useState('')
  useEffect(()=>{
    setFetchLoading(true)
    api.getCodes(group.id)
      .then(data=>setRows(Array.isArray(data)?data.map(c=>({fcode:c.fcode||'',food_name:c.food_name||''})):[]))
      .catch(()=>setRows([]))
      .finally(()=>setFetchLoading(false))
  },[group.id])
  const addRow=()=>setRows(r=>[...r,{fcode:'',food_name:''}])
  const updateRow=(i,field,val)=>setRows(r=>r.map((row,idx)=>idx===i?{...row,[field]:val}:row))
  const removeRow=i=>setRows(r=>r.filter((_,idx)=>idx!==i))
  const handleSave=async()=>{
    setLoading(true);setError('')
    try{
      await api.setCodes(group.id,rows.filter(r=>r.fcode||r.food_name))
      onDone()
    }catch(e){setError(e?.response?.data?.detail||'저장 오류')}
    finally{setLoading(false)}
  }
  return <Modal title={`식품군 편집: ${group.name}`} subtitle="식품 코드(fcode)와 식품명 목록을 편집합니다" onClose={onClose} width={560}>
    <div style={{display:'flex',flexDirection:'column',gap:10,marginTop:14}}>
      {error&&<div style={{background:'#FEE2E2',color:'#B91C1C',borderRadius:4,padding:'8px 12px',fontSize:12}}>{error}</div>}
      {fetchLoading?<div style={{textAlign:'center',padding:24,color:'#94A3B8'}}>불러오는 중...</div>:
      <><div style={{maxHeight:320,overflow:'auto',border:'1px solid #E2E8F0',borderRadius:6}}>
          <table style={{width:'100%',borderCollapse:'collapse'}}>
            <thead><tr style={{background:'#F8FAFC'}}>
              <th style={{padding:'8px 10px',fontSize:11,fontWeight:600,color:'#64748B',textAlign:'left',borderBottom:'1px solid #E2E8F0'}}>fcode</th>
              <th style={{padding:'8px 10px',fontSize:11,fontWeight:600,color:'#64748B',textAlign:'left',borderBottom:'1px solid #E2E8F0'}}>food_name</th>
              <th style={{width:36,borderBottom:'1px solid #E2E8F0'}}></th>
            </tr></thead>
            <tbody>
              {rows.map((row,i)=><tr key={i} style={{borderBottom:'1px solid #F1F5F9'}}>
                <td style={{padding:'4px 8px'}}>
                  <input value={row.fcode} onChange={e=>updateRow(i,'fcode',e.target.value)}
                    style={{width:'100%',border:'1px solid #E2E8F0',borderRadius:4,padding:'4px 8px',fontSize:12}}
                    placeholder="코드"/>
                </td>
                <td style={{padding:'4px 8px'}}>
                  <input value={row.food_name} onChange={e=>updateRow(i,'food_name',e.target.value)}
                    style={{width:'100%',border:'1px solid #E2E8F0',borderRadius:4,padding:'4px 8px',fontSize:12}}
                    placeholder="식품명"/>
                </td>
                <td style={{padding:'4px 6px',textAlign:'center'}}>
                  <button onClick={()=>removeRow(i)} style={{background:'none',border:'none',cursor:'pointer',color:'#EF4444',fontSize:14}}>✕</button>
                </td>
              </tr>)}
              {rows.length===0&&<tr><td colSpan={3} style={{textAlign:'center',padding:20,color:'#94A3B8',fontSize:12}}>행이 없습니다. 아래 버튼으로 추가하세요.</td></tr>}
            </tbody>
          </table>
        </div>
        <div><Btn variant="secondary" small onClick={addRow}>＋ 행 추가</Btn></div></>}
      <div style={{display:'flex',justifyContent:'flex-end',gap:8,paddingTop:8,borderTop:'1px solid #E2E8F0'}}>
        <Btn variant="secondary" onClick={onClose}>취소</Btn>
        <Btn onClick={handleSave} disabled={loading||fetchLoading}>{loading?'저장 중...':'저장'}</Btn>
      </div>
    </div>
  </Modal>
}

// ── 식품군 DB 패널 ───────────────────────────────────────────────────────────
function DbGroupView(){
  const [groups,setGroups]=useState([])
  const [sel,setSel]=useState(null)
  const [showAdd,setShowAdd]=useState(false)
  const [form,setForm]=useState({name:'',memo:''})
  const [editGroup,setEditGroup]=useState(null)
  const [groupSearch,setGroupSearch]=useState('')
  const load=()=>api.getGroups().then(setGroups).catch(()=>{})
  useEffect(()=>{load()},[])
  const handleCreate=async()=>{
    if(!form.name.trim())return
    await api.createGroup(form);setShowAdd(false);setForm({name:'',memo:''});load()
  }
  const handleDelete=async(g)=>{
    if(g.is_builtin){alert('기본 제공 식품군은 삭제할 수 없습니다.');return}
    if(!window.confirm(`'${g.name}' 식품군을 삭제하시겠습니까?`))return
    await api.deleteGroup(g.id);setSel(null);load()
  }
  const selGroup=groups.find(g=>g.id===sel)
  const filteredGroups=useMemo(()=>{
    if(!groupSearch.trim())return groups
    const kw=groupSearch.trim().toLowerCase()
    return groups.filter(g=>g.name?.toLowerCase().includes(kw)||g.memo?.toLowerCase().includes(kw))
  },[groups,groupSearch])
  const openEdit=(g)=>{if(g)setEditGroup(g)}
  return <div style={{flex:1,display:'flex',overflow:'hidden'}}>
    <div style={{flex:1,padding:'20px',display:'flex',flexDirection:'column',overflow:'hidden'}}>
      <div style={{display:'flex',justifyContent:'space-between',alignItems:'center',marginBottom:12}}>
        <div>
          <div style={{fontSize:16,fontWeight:700}}>식품군 DB</div>
          <div style={{fontSize:11,color:'#94A3B8',marginTop:2}}>기본 식품군 + 분석 시 추가 등록된 식품군을 관리합니다</div>
        </div>
        <div style={{display:'flex',gap:8}}>
          <Btn onClick={()=>setShowAdd(true)}>＋ 추가</Btn>
          <Btn variant="secondary" disabled={!sel} onClick={()=>selGroup&&openEdit(selGroup)}>✎ 편집</Btn>
          <Btn variant="danger" disabled={!sel||selGroup?.is_builtin} onClick={()=>selGroup&&handleDelete(selGroup)}>🗑 삭제</Btn>
        </div>
      </div>
      {showAdd&&<div style={{background:'#F8FAFC',border:'1px solid #E2E8F0',borderRadius:6,padding:'12px',marginBottom:12}}>
        <div style={{fontWeight:600,fontSize:12,marginBottom:8}}>새 식품군 추가</div>
        <div style={{display:'flex',gap:8,marginBottom:8}}>
          <Input value={form.name} onChange={v=>setForm(f=>({...f,name:v}))} placeholder="식품군명 *" style={{flex:1}}/>
          <Input value={form.memo} onChange={v=>setForm(f=>({...f,memo:v}))} placeholder="메모" style={{flex:1}}/>
        </div>
        <div style={{display:'flex',gap:6}}>
          <Btn small onClick={handleCreate} disabled={!form.name.trim()}>저장</Btn>
          <Btn variant="secondary" small onClick={()=>{setShowAdd(false);setForm({name:'',memo:''})}}>취소</Btn>
        </div>
      </div>}
      <div style={{marginBottom:8}}>
        <Input value={groupSearch} onChange={setGroupSearch} placeholder="식품군명·설명 검색"/>
      </div>
      <div style={{flex:1,border:'1px solid #E2E8F0',borderRadius:6,overflow:'auto'}}>
        <table>
          <thead><tr>
            <th>식품군명</th>
            <th>설명</th>
            <th style={{textAlign:'right',width:70}}>식품 수</th>
            <th style={{textAlign:'right',width:70}}>코드 수</th>
            <th>포함 식품</th>
            <th style={{width:60}}>구분</th>
          </tr></thead>
          <tbody>
            {filteredGroups.map(g=><tr key={g.id}
              className={sel===g.id?'selected':''}
              onClick={()=>setSel(g.id===sel?null:g.id)}
              onDoubleClick={()=>openEdit(g)}
              style={{cursor:'pointer'}}>
              <td><strong>{g.name}</strong></td>
              <td style={{color:'#64748B',fontSize:11,maxWidth:160,overflow:'hidden',textOverflow:'ellipsis',whiteSpace:'nowrap'}}>
                {g.memo?(g.memo.length>20?g.memo.slice(0,20)+'…':g.memo):''}
              </td>
              <td style={{textAlign:'right'}}>{g.food_count}</td>
              <td style={{textAlign:'right'}}>{g.code_count}</td>
              <td style={{color:'#64748B',fontSize:11}}>{(g.foods||[]).slice(0,5).join(', ')}{(g.foods||[]).length>5&&' …'}</td>
              <td><Badge color={g.is_builtin?'blue':'gray'}>{g.is_builtin?'기본':'사용자'}</Badge></td>
            </tr>)}
          </tbody>
        </table>
      </div>
    </div>
    {selGroup&&<div style={{width:240,borderLeft:'1px solid #E2E8F0',padding:'16px',overflow:'auto',flexShrink:0}}>
      <div style={{fontWeight:700,marginBottom:10}}>{selGroup.name}</div>
      {selGroup.memo&&<div style={{fontSize:11,color:'#64748B',marginBottom:10}}>{selGroup.memo}</div>}
      <div style={{fontSize:11,fontWeight:600,color:'#64748B',marginBottom:6}}>포함 식품 ({selGroup.food_count}종)</div>
      <div style={{display:'flex',flexWrap:'wrap',gap:4}}>
        {(selGroup.foods||[]).map(f=><Badge key={f}>{f}</Badge>)}
      </div>
    </div>}
    {editGroup&&<FoodGroupEditModal group={editGroup} onClose={()=>setEditGroup(null)} onDone={()=>{setEditGroup(null);load()}}/>}
  </div>
}

// ── 메인 DB 탭 ───────────────────────────────────────────────────────────────
export default function DbManagementTab(){
  const [subTab,setSubTab]=useState('data')
  const [sources,setSources]=useState([])
  const [datasets,setDatasets]=useState([])
  const [projF,setProjF]=useState('')
  const [phaseF,setPhaseF]=useState('')
  const [roundF,setRoundF]=useState('')
  const [chkDay1,setChkDay1]=useState(true)
  const [chkDay2,setChkDay2]=useState(true)
  const [search,setSearch]=useState('')
  const [selId,setSelId]=useState(null)
  const [modal,setModal]=useState(null) // 'upload'|'source'|'reassign'
  const [loading,setLoading]=useState(false)

  const loadSources=()=>api.getSources().then(setSources).catch(()=>{})
  const loadDatasets=()=>{
    setLoading(true)
    api.getDatasets({search}).then(setDatasets).catch(()=>{}).finally(()=>setLoading(false))
  }
  useEffect(()=>{loadSources();loadDatasets()},[])
  useEffect(()=>{loadDatasets()},[search])

  const phases=(sources.find(p=>p.id===projF)?.phases)||[]
  const rounds=(phases.find(p=>p.id===phaseF)?.rounds)||[]

  const filtered=useMemo(()=>datasets.filter(d=>{
    if(!chkDay1&&d.type==='X1')return false
    if(!chkDay2&&d.type==='X0')return false
    if(roundF&&d.round_id!==roundF)return false
    else if(phaseF){
      const ph=phases.find(p=>p.id===phaseF)
      if(ph&&!ph.rounds.some(r=>r.id===d.round_id))return false
    }else if(projF){
      const pr=sources.find(p=>p.id===projF)
      if(pr){
        const allRounds=pr.phases.flatMap(ph=>ph.rounds).map(r=>r.id)
        if(!allRounds.includes(d.round_id))return false
      }
    }
    return true
  }),[datasets,chkDay1,chkDay2,projF,phaseF,roundF,phases,sources])

  const selDataset=filtered.find(d=>d.id===selId)||datasets.find(d=>d.id===selId)

  const handleDelete=async()=>{
    if(!selId||!window.confirm('선택한 데이터를 삭제하시겠습니까?'))return
    await api.deleteDataset(selId);setSelId(null);loadDatasets()
  }

  const day1c=filtered.filter(d=>d.type==='X1').length
  const day2c=filtered.filter(d=>d.type==='X0').length
  const orphanCnt=filtered.filter(d=>d.is_orphan||!d.source_label).length
  const summary=`전체 ${filtered.length}건  |  1일조사 ${day1c} · 2일조사 ${day2c}`+
    (orphanCnt>0?`  |  ⚠ 삭제된 자료원 ${orphanCnt}건`:'')

  return <div style={{flex:1,display:'flex',flexDirection:'column',overflow:'hidden',minHeight:0}}>
    <div style={{padding:'8px 20px',borderBottom:'1px solid #E2E8F0',background:'#fff',flexShrink:0}}>
      <div style={{display:'inline-flex',border:'1px solid #CBD5E1',borderRadius:5,overflow:'hidden'}}>
        {[['data','자료 (1일·2일 조사)'],['group','식품군 DB']].map(([v,l])=>
          <button key={v} onClick={()=>setSubTab(v)}
            style={{padding:'6px 16px',fontSize:12,fontWeight:subTab===v?700:400,
              background:subTab===v?'#2563EB':'#fff',color:subTab===v?'#fff':'#475569',border:'none',cursor:'pointer'}}>{l}</button>
        )}
      </div>
    </div>
    {subTab==='data'?<div style={{flex:1,display:'flex',overflow:'hidden'}}>
      {/* 좌측 필터 패널 */}
      <div style={{width:260,flexShrink:0,borderRight:'1px solid #E2E8F0',padding:'20px 16px',display:'flex',flexDirection:'column',overflow:'auto'}}>
        <div style={{fontWeight:700,fontSize:14,marginBottom:2}}>필터</div>
        <div style={{color:'#94A3B8',fontSize:11,marginBottom:16}}>자료원 + 종류로 좁힘</div>
        <div style={{display:'flex',flexDirection:'column',gap:12,flex:1}}>
          <div><div style={{fontSize:11,fontWeight:600,color:'#64748B',marginBottom:4}}>대분류 (사업)</div>
            <Select value={projF} onChange={v=>{setProjF(v);setPhaseF('');setRoundF('')}}>
              <option value="">전체</option>
              {sources.map(p=><option key={p.id} value={p.id}>{p.name_ko}</option>)}
            </Select></div>
          <div><div style={{fontSize:11,fontWeight:600,color:'#64748B',marginBottom:4}}>기수</div>
            <Select value={phaseF} onChange={v=>{setPhaseF(v);setRoundF('')}}>
              <option value="">전체</option>
              {phases.map(p=><option key={p.id} value={p.id}>{p.phase_label}</option>)}
            </Select></div>
          <div><div style={{fontSize:11,fontWeight:600,color:'#64748B',marginBottom:4}}>차수</div>
            <Select value={roundF} onChange={setRoundF}>
              <option value="">전체</option>
              {rounds.map(r=><option key={r.id} value={r.id}>{r.display_label}</option>)}
            </Select></div>
          <div><div style={{fontSize:11,fontWeight:600,color:'#64748B',marginBottom:8}}>데이터 종류</div>
            <div style={{display:'flex',flexDirection:'column',gap:8}}>
              <Checkbox checked={chkDay1} onChange={setChkDay1} label="1일 조사"/>
              <Checkbox checked={chkDay2} onChange={setChkDay2} label="2일 조사"/>
            </div></div>
        </div>
        <div style={{display:'flex',flexDirection:'column',gap:8,marginTop:16,paddingTop:16,borderTop:'1px solid #E2E8F0'}}>
          <Btn variant="secondary" onClick={()=>{setProjF('');setPhaseF('');setRoundF('');setChkDay1(true);setChkDay2(true)}}>↺ 필터 초기화</Btn>
          <Btn variant="secondary" onClick={()=>setModal('source')}>📁 자료원 관리...</Btn>
        </div>
      </div>
      {/* 우측 메인 패널 */}
      <div style={{flex:1,padding:'20px',display:'flex',flexDirection:'column',overflow:'hidden'}}>
        <div style={{display:'flex',justifyContent:'space-between',alignItems:'flex-start',marginBottom:12}}>
          <div>
            <div style={{fontSize:16,fontWeight:700}}>DB 조회 / 관리</div>
            <div style={{fontSize:11,color:'#94A3B8',marginTop:2}}>등록된 1일·2일 조사 데이터를 한 곳에서 관리</div>
          </div>
          <div style={{display:'flex',gap:8}}>
            <Btn onClick={()=>setModal('upload')}>📤 업로드</Btn>
            <Btn variant="secondary" disabled={!selId} onClick={()=>selId&&api.downloadDataset(selId)}>🔍 조회(다운로드)</Btn>
            <Btn variant="secondary" disabled={!selId} onClick={()=>selId&&setModal('reassign')}>↔ 재지정</Btn>
            <Btn variant="danger" disabled={!selId} onClick={handleDelete}>🗑 삭제</Btn>
          </div>
        </div>
        <Input value={search} onChange={setSearch} placeholder="파일명·설명·자료원 검색" style={{marginBottom:8}}/>
        <div style={{background:'#F8FAFC',border:'1px solid #E2E8F0',borderRadius:4,padding:'8px 14px',marginBottom:10,fontSize:12,color:'#475569'}}>{summary}</div>
        <div style={{flex:1,border:'1px solid #E2E8F0',borderRadius:6,overflow:'auto'}}>
          {loading?<div style={{padding:40,textAlign:'center',color:'#94A3B8'}}>불러오는 중...</div>:
          <table>
            <thead><tr>
              <th style={{width:130}}>종류</th>
              <th style={{width:130}}>대분류</th>
              <th style={{width:150}}>기수·차수</th>
              <th>파일명</th>
              <th style={{width:140}}>설명</th>
              <th style={{width:80,textAlign:'right'}}>행 수</th>
              <th style={{width:120}}>등록일시</th>
              <th style={{width:80}}>등록자</th>
            </tr></thead>
            <tbody>
              {filtered.map(d=>{
                const isOrphan=d.is_orphan||!d.source_label
                const parsed=parseSourceLabel(d.source_label)
                const phaseRound=parsed.phase&&parsed.round?`${parsed.phase} · ${parsed.round}`:parsed.round||parsed.phase||'-'
                const projectName=isOrphan?'⚠ 삭제된 자료원':parsed.project||'-'
                return <tr key={d.id}
                  className={selId===d.id?'selected':''}
                  onClick={()=>setSelId(d.id===selId?null:d.id)}
                  style={{cursor:'pointer',background:isOrphan&&selId!==d.id?'#fff8e1':''}}>
                  <td>
                    <div style={{display:'flex',alignItems:'center',gap:4}}>
                      <Badge color={kindBadge(d.type)}>{kindLabel(d.type)}</Badge>
                      {isOrphan&&<span title="자료원 없음">⚠</span>}
                    </div>
                  </td>
                  <td style={{fontSize:12,color:'#374151'}}>{projectName}</td>
                  <td style={{fontSize:12,color:'#475569'}}>{phaseRound}</td>
                  <td style={{fontFamily:'monospace',fontSize:12}}>{d.original_filename}</td>
                  <td style={{color:'#64748B',fontSize:11,maxWidth:140,overflow:'hidden',textOverflow:'ellipsis',whiteSpace:'nowrap'}}>{d.description||''}</td>
                  <td style={{textAlign:'right',fontSize:12}}>{fmtNum(d.row_count)}</td>
                  <td style={{color:'#94A3B8',fontSize:11}}>{fmtDt(d.registered_at)}</td>
                  <td style={{fontSize:12,color:'#475569'}}>{d.registered_by||''}</td>
                </tr>
              })}
              {filtered.length===0&&<tr><td colSpan={8} style={{textAlign:'center',color:'#94A3B8',padding:'32px'}}>등록된 데이터가 없습니다</td></tr>}
            </tbody>
          </table>}
        </div>
      </div>
    </div>:<DbGroupView/>}
    {modal==='upload'&&<UploadModal sources={sources} onClose={()=>setModal(null)} onDone={()=>{setModal(null);loadDatasets()}}/>}
    {modal==='source'&&<SourceManageModal sources={sources} onClose={()=>setModal(null)} onDone={()=>{loadSources();loadDatasets()}}/>}
    {modal==='reassign'&&selDataset&&<ReassignModal sources={sources} dataset={selDataset} onClose={()=>setModal(null)} onDone={()=>{setModal(null);loadDatasets()}}/>}
  </div>
}
