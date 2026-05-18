import React,{useState,useEffect,useRef,useCallback} from 'react'
import {Btn,Input,Select,Checkbox,Badge,Modal,Spinner} from '../components/UI'
import {DensityChart,QuantileChart} from '../components/Charts'
import {api} from '../api'

// ── 분석 이력 모달 (시나리오 기준, C# ScenarioHistoryDialog와 동일) ──────────
function HistoryModal({onClose,onSelect}){
  const [rows,setRows]=useState([])
  const [sel,setSel]=useState(null)
  const [search,setSearch]=useState('')

  useEffect(()=>{
    api.getScenarios().then(data=>{
      // 등록일 내림차순 정렬
      const sorted=[...data].sort((a,b)=>new Date(b.registered_at||0)-new Date(a.registered_at||0))
      setRows(sorted)
    }).catch(()=>{})
  },[])

  const filtered=rows.filter(r=>{
    if(!search)return true
    return (r.name||'').includes(search)||(r.registered_by||'').includes(search)
  })

  const fmtDate=(s)=>{
    if(!s)return '-'
    const d=new Date(s)
    const mm=String(d.getMonth()+1).padStart(2,'0')
    const dd=String(d.getDate()).padStart(2,'0')
    return `${mm}-${dd}`
  }
  const fmtDateTime=(s)=>{
    if(!s)return '-'
    const d=new Date(s)
    const mm=String(d.getMonth()+1).padStart(2,'0')
    const dd=String(d.getDate()).padStart(2,'0')
    const hh=String(d.getHours()).padStart(2,'0')
    const mi=String(d.getMinutes()).padStart(2,'0')
    return `${mm}-${dd} ${hh}:${mi}`
  }

  const handleRowClick=(id)=>setSel(id===sel?null:id)
  const handleDblClick=(sc)=>{onSelect(sc);onClose();}

  return <Modal title="분석 이력" subtitle="이전에 등록·실행한 시나리오를 다시 열거나 분석을 재실행할 수 있습니다" onClose={onClose} width={780}>
    <div style={{marginTop:10}}>
      <div style={{display:'flex',alignItems:'center',gap:10,marginBottom:8}}>
        <Input value={search} onChange={setSearch} placeholder="식품군명·등록자 검색" style={{flex:1}}/>
        <span style={{fontSize:12,color:'#64748B',whiteSpace:'nowrap'}}>{filtered.length} / {rows.length} 건</span>
      </div>
      <div style={{border:'1px solid #E2E8F0',borderRadius:6,overflow:'auto',maxHeight:340}}>
        <table>
          <thead><tr>
            <th>식품군</th>
            <th style={{textAlign:'right',width:64}}>식품수</th>
            <th style={{width:90}}>등록자</th>
            <th style={{width:80}}>등록일</th>
            <th style={{width:120}}>마지막 분석</th>
            <th style={{width:70}}>캐시</th>
          </tr></thead>
          <tbody>
            {filtered.map(sc=><tr key={sc.id}
              className={sel===sc.id?'selected':''}
              onClick={()=>handleRowClick(sc.id)}
              onDoubleClick={()=>handleDblClick(sc)}
              style={{cursor:'pointer'}}>
              <td><strong>{sc.name||'-'}</strong></td>
              <td style={{textAlign:'right'}}>{(sc.food_codes||[]).length}</td>
              <td>{sc.registered_by||'-'}</td>
              <td style={{color:'#94A3B8'}}>{fmtDate(sc.registered_at)}</td>
              <td style={{color:'#94A3B8'}}>{fmtDateTime(sc.last_analyzed_at)}</td>
              <td><Badge color="gray">캐시없음</Badge></td>
            </tr>)}
            {filtered.length===0&&<tr><td colSpan={6} style={{textAlign:'center',color:'#94A3B8',padding:'24px'}}>시나리오가 없습니다</td></tr>}
          </tbody>
        </table>
      </div>
      <div style={{display:'flex',justifyContent:'flex-end',gap:8,paddingTop:12}}>
        <Btn variant="secondary" onClick={onClose}>닫기</Btn>
        <Btn disabled={!sel} onClick={()=>{
          const sc=rows.find(x=>x.id===sel)
          if(sc)onSelect(sc)
          onClose()
        }}>▶ 열기 (분석)</Btn>
      </div>
    </div>
  </Modal>
}

// ── 식품군 추가 미니 모달 ─────────────────────────────────────────────────
function AddGroupModal({onClose,onCreated}){
  const [name,setName]=useState('')
  const [loading,setLoading]=useState(false)
  const [err,setErr]=useState('')

  const handleSave=async()=>{
    if(!name.trim()){setErr('식품군명을 입력하세요');return}
    setLoading(true);setErr('')
    try{
      await api.createGroup({name:name.trim(),memo:''})
      onCreated()
      onClose()
    }catch(e){setErr(e?.response?.data?.detail||'저장 실패')}
    finally{setLoading(false)}
  }

  return <Modal title="식품군 추가" onClose={onClose} width={360}>
    <div style={{padding:'8px 0',display:'flex',flexDirection:'column',gap:10}}>
      {err&&<div style={{background:'#FEE2E2',color:'#B91C1C',borderRadius:4,padding:'6px 10px',fontSize:12}}>{err}</div>}
      <div>
        <div style={{fontSize:12,fontWeight:600,marginBottom:4}}>식품군명 *</div>
        <Input value={name} onChange={setName} placeholder="예: 채소류" onKeyDown={e=>e.key==='Enter'&&handleSave()}/>
      </div>
      <div style={{display:'flex',justifyContent:'flex-end',gap:8}}>
        <Btn variant="secondary" onClick={onClose} disabled={loading}>취소</Btn>
        <Btn onClick={handleSave} disabled={loading||!name.trim()}>{loading?'저장 중...':'저장'}</Btn>
      </div>
    </div>
  </Modal>
}

// ── 사이드 패널 ───────────────────────────────────────────────────────────
function SidePanel({onClose,onRun}){
  const [groups,setGroups]=useState([])
  const [datasets,setDatasets]=useState([])
  const [selGroup,setSelGroup]=useState(null)
  const [day1Checked,setDay1Checked]=useState({})
  const [day2Checked,setDay2Checked]=useState({})
  const [simTime,setSimTime]=useState('5')
  const [byWho,setByWho]=useState('')
  const [groupSearch,setGroupSearch]=useState('')
  const [loading,setLoading]=useState(false)
  const [error,setError]=useState('')
  const [progressStep,setProgressStep]=useState('')
  const [showAddGroup,setShowAddGroup]=useState(false)
  const progressRef=useRef(null)

  const loadGroups=useCallback(()=>{
    api.getGroups().then(setGroups).catch(()=>{})
  },[])

  useEffect(()=>{
    loadGroups()
    api.getDatasets({}).then(setDatasets).catch(()=>{})
  },[])

  const day1Ds=datasets.filter(d=>d.type==='X1')
  const day2Ds=datasets.filter(d=>d.type==='X0')
  const filteredGroups=groups.filter(g=>!groupSearch||g.name.includes(groupSearch))
  const canRun=selGroup&&Object.values(day1Checked).some(Boolean)&&Object.values(day2Checked).some(Boolean)&&byWho.trim()

  const checkAll=(ds,setter,val)=>setter(Object.fromEntries(ds.map(d=>[d.id,val])))

  // 선택된 식품군의 codes
  const selectedGroupObj=groups.find(g=>g.id===selGroup)
  const selectedCodes=selectedGroupObj?.codes||[]

  // 분석 진행 단계 순환
  const startProgressCycle=()=>{
    const steps=['데이터 로드 중...','분석 엔진 실행 중...','결과 저장 중...']
    let i=0
    setProgressStep(steps[0])
    const timer=setInterval(()=>{
      i=(i+1)%steps.length
      setProgressStep(steps[i])
    },2000)
    progressRef.current=timer
  }
  const stopProgressCycle=()=>{
    if(progressRef.current){clearInterval(progressRef.current);progressRef.current=null}
    setProgressStep('')
  }

  const handleRun=async()=>{
    const g=groups.find(g=>g.id===selGroup)
    if(!g||!canRun)return
    setLoading(true);setError('')
    startProgressCycle()
    try{
      const sc=await api.createScenario({
        name:g.name, food_group_id:g.id,
        food_names:g.foods||[], food_codes:g.codes||[],
        x1_ids:Object.entries(day1Checked).filter(([,v])=>v).map(([k])=>k),
        x0_ids:Object.entries(day2Checked).filter(([,v])=>v).map(([k])=>k),
        sim_time:parseInt(simTime), registered_by:byWho,
      })
      const result=await api.runAnalysis({scenario_id:sc.id})
      stopProgressCycle()
      onRun(result,g.name,sc)
    }catch(e){
      stopProgressCycle()
      setError(e?.response?.data?.detail||'분석 오류가 발생했습니다.')
    }
    finally{setLoading(false)}
  }

  return <>
    <div style={{position:'absolute',top:0,right:0,bottom:0,width:440,
      background:'#fff',borderLeft:'1px solid #E2E8F0',display:'flex',flexDirection:'column',
      zIndex:50,boxShadow:'-4px 0 24px rgba(0,0,0,.12)'}}>
      <div style={{background:'#DBEAFE',borderBottom:'1px solid #BFDBFE',padding:'14px 18px',flexShrink:0}}>
        <div style={{display:'flex',justifyContent:'space-between',alignItems:'flex-start'}}>
          <div><div style={{fontSize:15,fontWeight:700}}>시나리오 선택</div>
            <div style={{fontSize:11,color:'#94A3B8',marginTop:2}}>식품군 선택 → 자료 매칭 → 분석 조건 입력</div></div>
          <button onClick={onClose} style={{background:'none',border:'none',cursor:'pointer',fontSize:18,color:'#94A3B8'}}>✕</button>
        </div>
      </div>
      <div style={{flex:1,overflow:'auto',padding:'16px 18px',display:'flex',flexDirection:'column',gap:16}}>
        {error&&<div style={{background:'#FEE2E2',color:'#B91C1C',borderRadius:4,padding:'8px 12px',fontSize:12}}>{error}</div>}

        {/* ① 식품군 선택 */}
        <div>
          <div style={{fontSize:12,fontWeight:700,marginBottom:4}}>① 식품군 선택 *</div>
          <div style={{fontSize:10,color:'#94A3B8',marginBottom:6}}>2일 조사 데이터에 식품 코드가 포함된 식품군만 표시</div>
          <div style={{display:'flex',gap:6,marginBottom:6}}>
            <Input value={groupSearch} onChange={setGroupSearch} placeholder="식품군명 검색" style={{flex:1}}/>
            <Btn variant="secondary" small onClick={()=>setShowAddGroup(true)}>＋ 식품군 추가</Btn>
          </div>
          <div style={{border:'1px solid #E2E8F0',borderRadius:6,overflow:'auto',maxHeight:160}}>
            <table>
              <thead><tr><th>식품군</th><th style={{width:56,textAlign:'right'}}>식품</th><th style={{width:56,textAlign:'right'}}>코드</th></tr></thead>
              <tbody>
                {filteredGroups.map(g=><tr key={g.id} className={selGroup===g.id?'selected':''}
                  onClick={()=>setSelGroup(g.id===selGroup?null:g.id)} style={{cursor:'pointer'}}>
                  <td>{g.name}</td><td style={{textAlign:'right'}}>{g.food_count}</td><td style={{textAlign:'right'}}>{g.code_count}</td>
                </tr>)}
              </tbody>
            </table>
          </div>
          {/* 선택된 식품군 코드 안내 박스 */}
          {selGroup&&selectedCodes.length>0&&<div style={{marginTop:6,padding:'6px 10px',background:'#EFF6FF',border:'1px solid #BFDBFE',borderRadius:4,fontSize:11,color:'#1D4ED8'}}>
            선택 식품군 코드: {selectedCodes.slice(0,8).join(', ')}{selectedCodes.length>8?` 외 ${selectedCodes.length-8}개`:''}
          </div>}
          {/* 포함 식품 chip */}
          {selGroup&&<div style={{marginTop:6,padding:'8px 10px',background:'#F1F5F9',borderRadius:4}}>
            <div style={{fontSize:11,fontWeight:600,marginBottom:4,color:'#64748B'}}>포함 식품 ({(selectedGroupObj?.foods||[]).length}개)</div>
            {(selectedGroupObj?.foods||[]).length===0
              ?<div style={{fontSize:11,color:'#94A3B8'}}>식품군을 선택하면 포함된 식품명이 여기에 표시됩니다.</div>
              :<div style={{display:'flex',flexWrap:'wrap',gap:3}}>
                {(selectedGroupObj?.foods||[]).map(f=><Badge key={f}>{f}</Badge>)}
              </div>}
          </div>}
        </div>

        {/* ② 1일 조사 데이터 */}
        <div>
          <div style={{display:'flex',justifyContent:'space-between',alignItems:'center',marginBottom:2}}>
            <div style={{fontSize:12,fontWeight:700}}>② 1일 조사 데이터 *</div>
            <div style={{display:'flex',gap:4}}>
              <Btn variant="ghost" small onClick={()=>checkAll(day1Ds,setDay1Checked,true)}>전체선택</Btn>
              <span style={{color:'#CBD5E1',alignSelf:'center'}}>|</span>
              <Btn variant="ghost" small onClick={()=>checkAll(day1Ds,setDay1Checked,false)}>해제</Btn>
            </div>
          </div>
          <div style={{fontSize:10,color:'#94A3B8',marginBottom:4}}>식품군 선택 시 해당 코드를 포함한 항목이 활성화됩니다.</div>
          <div style={{border:'1px solid #E2E8F0',borderRadius:6,padding:'8px 10px',display:'flex',flexDirection:'column',gap:6,maxHeight:140,overflow:'auto'}}>
            {day1Ds.length===0?<div style={{color:'#94A3B8',fontSize:12,padding:'4px 0'}}>등록된 1일 조사 데이터가 없습니다</div>:
            day1Ds.map(d=><Checkbox key={d.id} checked={!!day1Checked[d.id]} onChange={v=>setDay1Checked(c=>({...c,[d.id]:v}))}
              label={<span><span style={{fontWeight:600}}>{d.original_filename}</span><br/><span style={{color:'#94A3B8',fontSize:10}}>{d.source_label||d.round_id||'-'}</span></span>}/>)}
          </div>
        </div>

        {/* ③ 2일 조사 데이터 */}
        <div>
          <div style={{display:'flex',justifyContent:'space-between',alignItems:'center',marginBottom:2}}>
            <div style={{fontSize:12,fontWeight:700}}>③ 2일 조사 데이터 *</div>
            <div style={{display:'flex',gap:4}}>
              <Btn variant="ghost" small onClick={()=>checkAll(day2Ds,setDay2Checked,true)}>전체선택</Btn>
              <span style={{color:'#CBD5E1',alignSelf:'center'}}>|</span>
              <Btn variant="ghost" small onClick={()=>checkAll(day2Ds,setDay2Checked,false)}>해제</Btn>
            </div>
          </div>
          <div style={{fontSize:10,color:'#94A3B8',marginBottom:4}}>식품군 선택 시 해당 코드를 포함한 항목이 활성화됩니다.</div>
          <div style={{border:'1px solid #E2E8F0',borderRadius:6,padding:'8px 10px',display:'flex',flexDirection:'column',gap:6,maxHeight:140,overflow:'auto'}}>
            {day2Ds.length===0?<div style={{color:'#94A3B8',fontSize:12,padding:'4px 0'}}>등록된 2일 조사 데이터가 없습니다</div>:
            day2Ds.map(d=><Checkbox key={d.id} checked={!!day2Checked[d.id]} onChange={v=>setDay2Checked(c=>({...c,[d.id]:v}))}
              label={<span><span style={{fontWeight:600}}>{d.original_filename}</span><br/><span style={{color:'#94A3B8',fontSize:10}}>{d.source_label||d.round_id||'-'}</span></span>}/>)}
          </div>
        </div>

        {/* ④ 시뮬 횟수 */}
        <div>
          <div style={{fontSize:12,fontWeight:700,marginBottom:6}}>④ 시뮬레이션 반복 횟수 *</div>
          <Select value={simTime} onChange={setSimTime}>
            {['1','3','5','10','20','50'].map(v=><option key={v} value={v}>{v}회</option>)}
          </Select>
        </div>

        {/* ⑤ 분석자 */}
        <div>
          <div style={{fontSize:12,fontWeight:700,marginBottom:6}}>⑤ 분석자 *</div>
          <Input value={byWho} onChange={setByWho} placeholder="이름 입력"/>
        </div>
      </div>

      {/* 하단 액션 */}
      <div style={{background:'#DBEAFE',borderTop:'1px solid #BFDBFE',padding:'12px 18px',flexShrink:0}}>
        {loading&&<div style={{marginBottom:6,fontSize:11,color:'#2563EB'}}>
          <span style={{marginRight:6}}>⏳</span>{progressStep||'분석 실행 중...'}
        </div>}
        <div style={{display:'flex',justifyContent:'flex-end',gap:8}}>
          <Btn variant="secondary" onClick={onClose} disabled={loading}>취소</Btn>
          <Btn disabled={!canRun||loading} onClick={handleRun}>{loading?'분석 중...':'분석 실행'}</Btn>
        </div>
      </div>
    </div>

    {/* 식품군 추가 모달 */}
    {showAddGroup&&<AddGroupModal onClose={()=>setShowAddGroup(false)} onCreated={loadGroups}/>}
  </>
}

// ── 결과 대시보드 ─────────────────────────────────────────────────────────
function ResultDashboard({result,scenarioName,scenario}){
  const overall=result.result_table?.find(r=>r.sex==='ALL')||{}

  const exportCsv=()=>{
    // 16개 컬럼 (C# 순서)
    const header='성별,연령군,N,평균,SD,최솟값,P1백분위,P5백분위,P25백분위,중앙값,P75백분위,P90백분위,P95백분위,P97.5백분위,P99백분위,최댓값'
    const rows=(result.result_table||[]).map(row=>[
      row.sex,
      row.age_g_desc,
      row.n,
      row.average,
      row.sd,
      row.min_val,
      row.p1st,
      row.p5th,
      row.p25th,
      row.median,
      row.p75th,
      row.p90th,
      row.p95th,
      row.p975th,
      row.p99th,
      row.max_val,
    ].map(v=>v??'').join(',')).join('\n')
    const blob=new Blob(['﻿'+header+'\n'+rows],{type:'text/csv;charset=utf-8'})
    const url=URL.createObjectURL(blob)
    const a=document.createElement('a');a.href=url;a.download=`${scenarioName}_결과.csv`;a.click()
    URL.revokeObjectURL(url)
  }

  const r=result
  const add=r.additional_result

  // scenario 메타 정보
  const foodCount=(scenario?.food_codes||[]).length
  const simTime=scenario?.sim_time
  const registeredBy=scenario?.registered_by

  return <div style={{flex:1,overflow:'auto',padding:'20px'}}>
    <div style={{display:'flex',justifyContent:'flex-end',marginBottom:10}}>
      <Btn variant="secondary" small onClick={exportCsv}>💾 결과 내보내기 (CSV)</Btn>
    </div>

    {/* ① 메타 카드 */}
    <div style={{border:'1px solid #E2E8F0',borderRadius:8,background:'#fff',padding:'16px 20px',marginBottom:14}}>
      <div style={{display:'flex',gap:28,alignItems:'center',flexWrap:'wrap'}}>
        <div style={{flex:1,minWidth:160}}>
          <div style={{fontSize:11,fontWeight:600,color:'#94A3B8'}}>식품군</div>
          <div style={{fontSize:20,fontWeight:700}}>{scenarioName}</div>
          <div style={{fontSize:11,color:'#94A3B8',marginTop:2}}>{r.method_note?.split('\n')[0]}</div>
        </div>
        {[['분석 대상 (N)',(overall.n||0).toLocaleString(),'#2563EB'],
          ['rhoP',(r.rho_p||0).toFixed(3),'#16A34A'],
          ['rhoA',(r.rho_a||0).toFixed(3),'#16A34A'],
          ['papa (1일만)',`${(r.papa||0).toFixed(1)}%`,'#D97706']].map(([l,v,c])=>
          <div key={l} style={{textAlign:'right'}}>
            <div style={{fontSize:10,fontWeight:600,color:'#94A3B8'}}>{l}</div>
            <div style={{fontSize:20,fontWeight:700,color:c,marginTop:2}}>{v}</div>
          </div>)}
        <Badge color={r.method_used==='NCI'?'blue':r.method_used==='ISU'?'purple':'gray'} style={{marginLeft:'auto'}}>{r.method_used}</Badge>
      </div>
      {/* 부가 정보 행 */}
      {(foodCount||simTime||registeredBy)&&<div style={{marginTop:8,paddingTop:8,borderTop:'1px solid #F1F5F9',fontSize:12,color:'#888'}}>
        {foodCount?`식품 ${foodCount}개`:''}
        {foodCount&&simTime?' · ':''}
        {simTime?`시뮬 ${simTime}회`:''}
        {(foodCount||simTime)&&registeredBy?' · ':''}
        {registeredBy?`분석자: ${registeredBy}`:''}
      </div>}
    </div>

    {/* ② 요약 통계 카드 */}
    <div style={{display:'grid',gridTemplateColumns:'repeat(5,1fr)',gap:10,marginBottom:14}}>
      {[['평균 (mean)',overall.average,'#1E293B'],['중앙값 (P50)',overall.median,'#1E293B'],
        ['표준편차 (SD)',overall.sd,'#1E293B'],['95th 백분위',overall.p95th,'#D97706'],
        ['99th 백분위',overall.p99th,'#DC2626']].map(([l,v,c])=>
        <div key={l} style={{border:'1px solid #E2E8F0',borderRadius:8,background:'#fff',padding:'12px 14px'}}>
          <div style={{fontSize:10,fontWeight:600,color:'#94A3B8'}}>{l}</div>
          <div style={{fontSize:20,fontWeight:700,color:c,marginTop:4}}>{v!=null?v.toFixed(1):'-'}</div>
        </div>)}
    </div>

    {/* ③ 차트 */}
    <div style={{display:'grid',gridTemplateColumns:'1fr 1fr',gap:14,marginBottom:14}}>
      <div style={{border:'1px solid #E2E8F0',borderRadius:8,background:'#fff',padding:'14px'}}>
        <div style={{fontWeight:600,marginBottom:10,fontSize:13}}>일상섭취량 분포 (전체·남자·여자·1일 실측치)</div>
        <DensityChart personIntakes={r.person_intakes||[]}/>
      </div>
      <div style={{border:'1px solid #E2E8F0',borderRadius:8,background:'#fff',padding:'14px'}}>
        <div style={{fontWeight:600,marginBottom:10,fontSize:13}}>성별·연령군 P95 분위수 도표</div>
        <QuantileChart resultTable={r.result_table||[]}/>
      </div>
    </div>

    {/* ④ 결과 테이블 (C#과 동일 컬럼: 성별·연령군·N·평균·SD·최솟값·P95·P97.5·P99·최댓값) */}
    <div style={{border:'1px solid #E2E8F0',borderRadius:8,background:'#fff',overflow:'hidden',marginBottom:14}}>
      <div style={{padding:'10px 14px',borderBottom:'1px solid #E2E8F0',fontWeight:600,fontSize:13}}>
        성별·연령군 통계 결과 (NCI)
      </div>
      <div style={{overflow:'auto',maxHeight:300}}>
        <table>
          <thead><tr>
            <th>성별</th><th>연령군</th><th style={{textAlign:'right'}}>N</th>
            <th style={{textAlign:'right'}}>평균</th><th style={{textAlign:'right'}}>SD</th>
            <th style={{textAlign:'right'}}>최솟값</th>
            <th style={{textAlign:'right'}}>P95</th>
            <th style={{textAlign:'right'}}>P97.5</th>
            <th style={{textAlign:'right'}}>P99</th>
            <th style={{textAlign:'right'}}>최댓값</th>
          </tr></thead>
          <tbody>
            {(r.result_table||[]).map((row,i)=><tr key={i} style={row.sex==='ALL'?{fontWeight:600}:{}}>
              <td>{row.sex==='ALL'?'전체':row.sex}</td>
              <td>{row.age_g_desc==='ALL'?'전체':row.age_g_desc}</td>
              <td style={{textAlign:'right'}}>{(row.n||0).toLocaleString()}</td>
              <td style={{textAlign:'right'}}>{(row.average||0).toFixed(2)}</td>
              <td style={{textAlign:'right'}}>{(row.sd||0).toFixed(2)}</td>
              <td style={{textAlign:'right'}}>{(row.min_val||0).toFixed(2)}</td>
              <td style={{textAlign:'right',color:'#D97706'}}>{(row.p95th||0).toFixed(2)}</td>
              <td style={{textAlign:'right'}}>{(row.p975th||0).toFixed(2)}</td>
              <td style={{textAlign:'right',color:'#DC2626'}}>{(row.p99th||0).toFixed(2)}</td>
              <td style={{textAlign:'right'}}>{(row.max_val||0).toFixed(2)}</td>
            </tr>)}
          </tbody>
        </table>
      </div>
    </div>

    {/* ⑤ 보완 분석 결과 테이블 (C#과 동일 컬럼) */}
    {add&&<div style={{border:'1px solid #E2E8F0',borderRadius:8,background:'#fff',overflow:'hidden'}}>
      <div style={{padding:'10px 14px',borderBottom:'1px solid #E2E8F0',display:'flex',alignItems:'center',gap:8}}>
        <span style={{fontWeight:600,fontSize:13}}>보완 분석 결과</span>
        <Badge color={add.method_used==='ISU'?'purple':'gray'}>{add.method_used}</Badge>
        <span style={{fontSize:11,color:'#64748B'}}>{add.method_note?.split('\n')[0]}</span>
      </div>
      <div style={{overflow:'auto',maxHeight:240}}>
        <table>
          <thead><tr>
            <th>성별</th><th>연령군</th><th style={{textAlign:'right'}}>N</th>
            <th style={{textAlign:'right'}}>평균</th><th style={{textAlign:'right'}}>SD</th>
            <th style={{textAlign:'right'}}>최솟값</th>
            <th style={{textAlign:'right'}}>P25</th>
            <th style={{textAlign:'right'}}>중앙값</th>
            <th style={{textAlign:'right'}}>P75</th>
            <th style={{textAlign:'right'}}>P90</th>
            <th style={{textAlign:'right'}}>P95</th>
            <th style={{textAlign:'right'}}>P97.5</th>
            <th style={{textAlign:'right'}}>P99</th>
            <th style={{textAlign:'right'}}>최댓값</th>
          </tr></thead>
          <tbody>
            {(add.result_table||[]).map((row,i)=><tr key={i} style={row.sex==='ALL'?{fontWeight:600}:{}}>
              <td>{row.sex==='ALL'?'전체':row.sex}</td>
              <td>{row.age_g_desc==='ALL'?'전체':row.age_g_desc}</td>
              <td style={{textAlign:'right'}}>{(row.n||0).toLocaleString()}</td>
              <td style={{textAlign:'right'}}>{(row.average||0).toFixed(2)}</td>
              <td style={{textAlign:'right'}}>{(row.sd||0).toFixed(2)}</td>
              <td style={{textAlign:'right'}}>{(row.min_val||0).toFixed(2)}</td>
              <td style={{textAlign:'right'}}>{(row.p25th||0).toFixed(2)}</td>
              <td style={{textAlign:'right'}}>{(row.median||0).toFixed(2)}</td>
              <td style={{textAlign:'right'}}>{(row.p75th||0).toFixed(2)}</td>
              <td style={{textAlign:'right'}}>{(row.p90th||0).toFixed(2)}</td>
              <td style={{textAlign:'right',color:'#D97706'}}>{(row.p95th||0).toFixed(2)}</td>
              <td style={{textAlign:'right'}}>{(row.p975th||0).toFixed(2)}</td>
              <td style={{textAlign:'right',color:'#DC2626'}}>{(row.p99th||0).toFixed(2)}</td>
              <td style={{textAlign:'right'}}>{(row.max_val||0).toFixed(2)}</td>
            </tr>)}
          </tbody>
        </table>
      </div>
    </div>}
  </div>
}

// ── 메인 분석 탭 ──────────────────────────────────────────────────────────
export default function AnalysisTab(){
  const [sideOpen,setSideOpen]=useState(false)
  const [result,setResult]=useState(null)
  const [scenarioName,setScenarioName]=useState('')
  const [currentScenario,setCurrentScenario]=useState(null)
  const [showHistory,setShowHistory]=useState(false)

  const handleRun=(res,name,sc)=>{
    setResult(res)
    setScenarioName(name)
    setCurrentScenario(sc||null)
    setSideOpen(false)
  }

  const handleHistorySelect=async(sc)=>{
    try{
      const res=await api.getScenarioResult(sc.id)
      setResult(res)
      setScenarioName(sc.name||'')
      setCurrentScenario(sc)
    }catch(e){alert('결과를 불러오는 중 오류가 발생했습니다.')}
  }

  return <div style={{flex:1,display:'flex',flexDirection:'column',overflow:'hidden',position:'relative',minHeight:0}}>
    {/* 메인 액션 툴바 */}
    <div style={{padding:'10px 20px',borderBottom:'1px solid #E2E8F0',background:'#fff',
      display:'flex',alignItems:'center',justifyContent:'space-between',flexShrink:0}}>
      <div style={{display:'flex',alignItems:'center',gap:8}}>
        <Btn onClick={()=>setSideOpen(true)}>＋ 시나리오 분석</Btn>
        <div style={{width:1,height:24,background:'#E2E8F0',margin:'0 4px'}}/>
        <Btn variant="ghost" onClick={()=>setShowHistory(true)}>↻ 분석 이력</Btn>
      </div>
      {result&&<div style={{background:'#DBEAFE',border:'1px solid #BFDBFE',borderRadius:4,
        padding:'5px 12px',fontSize:11,color:'#2563EB',fontWeight:600}}>
        현재 시나리오 · {scenarioName}
      </div>}
    </div>

    {/* 결과 메타 툴바 */}
    {result&&<div style={{padding:'6px 20px',borderBottom:'1px solid #E2E8F0',background:'#F8FAFC',fontSize:11,color:'#64748B',flexShrink:0,overflowX:'auto',whiteSpace:'nowrap'}}>
      N={(result.result_table?.find(r=>r.sex==='ALL')?.n||0).toLocaleString()} · rhoP={result.rho_p?.toFixed(3)} · rhoA={result.rho_a?.toFixed(3)} · papa={result.papa?.toFixed(1)}% · 방법: {result.method_used}
      {result.additional_result?` + ${result.additional_result.method_used} 보완 적용`:''}
    </div>}

    {/* 본문 */}
    {!result
      ?<div style={{flex:1,display:'flex',flexDirection:'column',alignItems:'center',justifyContent:'center',gap:12}}>
          <div style={{fontSize:48}}>📊</div>
          <div style={{fontSize:15,fontWeight:700}}>선택된 시나리오가 없습니다</div>
          <div style={{fontSize:12,color:'#94A3B8',textAlign:'center',maxWidth:480,lineHeight:1.7}}>
            상단에서 <strong>＋ 시나리오 분석</strong>을 눌러 새 분석 조건을 구성하거나,<br/>
            <strong>↻ 분석 이력</strong>에서 기존 결과를 선택하세요.
          </div>
        </div>
      :<ResultDashboard result={result} scenarioName={scenarioName} scenario={currentScenario}/>
    }

    {sideOpen&&<SidePanel onClose={()=>setSideOpen(false)} onRun={handleRun}/>}
    {showHistory&&<HistoryModal onClose={()=>setShowHistory(false)} onSelect={handleHistorySelect}/>}
  </div>
}
