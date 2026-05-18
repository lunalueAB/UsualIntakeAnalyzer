import React from 'react'

export function DensityChart({personIntakes}){
  const W=520,H=220,pl=44,pr=16,pt=28,pb=36,w=W-pl-pr,h=H-pt-pb;
  function logNorm(x,mu,sig){
    if(x<=0)return 0;
    const z=(Math.log(x)-mu)/sig;
    return Math.exp(-z*z/2)/(x*sig*Math.sqrt(2*Math.PI));
  }
  // Estimate mu/sig from person intakes if available
  let curves;
  if(personIntakes&&personIntakes.length>0){
    const all=personIntakes.map(p=>p.intake).filter(v=>v>0);
    const male=personIntakes.filter(p=>p.sex===1).map(p=>p.intake).filter(v=>v>0);
    const female=personIntakes.filter(p=>p.sex===2).map(p=>p.intake).filter(v=>v>0);
    const raw=personIntakes.map(p=>p.raw_intk).filter(v=>v>0);
    const estMu=arr=>{const lv=arr.map(Math.log);return lv.reduce((a,b)=>a+b,0)/lv.length;};
    const estSig=arr=>{const mu=estMu(arr);const lv=arr.map(Math.log);return Math.sqrt(lv.map(v=>(v-mu)**2).reduce((a,b)=>a+b,0)/lv.length);};
    curves=[
      {name:'전체',mu:estMu(all),sig:Math.max(estSig(all),.1),color:'#2563EB',dash:''},
      {name:'남자',mu:estMu(male.length>3?male:all),sig:Math.max(estSig(male.length>3?male:all),.1),color:'#F97316',dash:''},
      {name:'여자',mu:estMu(female.length>3?female:all),sig:Math.max(estSig(female.length>3?female:all),.1),color:'#EC4899',dash:''},
      {name:'1일 실측치',mu:estMu(raw.length>3?raw:all),sig:Math.max(estSig(raw.length>3?raw:all),.15),color:'#94A3B8',dash:'5,4'},
    ];
  } else {
    curves=[
      {name:'전체',mu:4.8,sig:1.1,color:'#2563EB',dash:''},
      {name:'남자',mu:5.0,sig:1.05,color:'#F97316',dash:''},
      {name:'여자',mu:4.7,sig:1.15,color:'#EC4899',dash:''},
      {name:'1일 실측치',mu:5.1,sig:1.3,color:'#94A3B8',dash:'5,4'},
    ];
  }
  const N=120;
  const maxX=personIntakes&&personIntakes.length>0
    ? Math.min(Math.max(...personIntakes.map(p=>p.intake))*1.1,2000)
    : 900;
  let maxY=0;
  curves.forEach(c=>{for(let i=1;i<=N;i++){const y=logNorm(i/N*maxX,c.mu,c.sig);if(y>maxY)maxY=y;}});
  function path(c){
    const pts=[];
    for(let i=0;i<=N;i++){
      const x=i/N*maxX,y=logNorm(x,c.mu,c.sig);
      pts.push(`${i===0?'M':'L'}${(pl+(x/maxX)*w).toFixed(1)},${(pt+h-(y/maxY)*h).toFixed(1)}`);
    }
    return pts.join(' ');
  }
  return <svg viewBox={`0 0 ${W} ${H}`} style={{width:'100%',height:'auto'}}>
    <line x1={pl} y1={pt} x2={pl} y2={pt+h} stroke="#E2E8F0"/>
    <line x1={pl} y1={pt+h} x2={pl+w} y2={pt+h} stroke="#E2E8F0"/>
    {[.25,.5,.75,1].map(t=><line key={t} x1={pl} y1={pt+h*(1-t)} x2={pl+w} y2={pt+h*(1-t)} stroke="#F1F5F9"/>)}
    {curves.map(c=><path key={c.name} d={path(c)} fill="none" stroke={c.color} strokeWidth="2" strokeDasharray={c.dash}/>)}
    {[0,.25,.5,.75,1].map(t=><text key={t} x={pl+t*w} y={pt+h+14} textAnchor="middle" fontSize="10" fill="#94A3B8">{Math.round(t*maxX)}</text>)}
    {curves.map((c,i)=><g key={c.name} transform={`translate(${pl+i*120},${pt+4})`}>
      <line x1="0" y1="5" x2="16" y2="5" stroke={c.color} strokeWidth="2" strokeDasharray={c.dash}/>
      <text x="20" y="9" fontSize="10" fill="#475569">{c.name}</text>
    </g>)}
    <text x={pl+w/2} y={H-4} textAnchor="middle" fontSize="10" fill="#94A3B8">섭취량 (g/day)</text>
  </svg>;
}

export function QuantileChart({resultTable}){
  const W=520,H=300,pl=52,pr=16,pt=20,pb=50,w=W-pl-pr,h=H-pt-pb;
  const ageGroups=['19-29세','30-49세','50-64세','65세 이상'];
  const maleRows =ageGroups.map(ag=>(resultTable||[]).find(r=>r.sex==='남자'&&r.age_g_desc===ag));
  const femaleRows=ageGroups.map(ag=>(resultTable||[]).find(r=>r.sex==='여자'&&r.age_g_desc===ag));
  const hasData=maleRows.some(Boolean)||femaleRows.some(Boolean);
  const getP95=r=>r?r.p95th||r.p95||0:0;
  const allVals=[...maleRows,...femaleRows].map(getP95);
  const maxV=hasData?Math.max(...allVals)*1.15:600;
  const barW=22,gap=6,spacing=w/ageGroups.length;
  const labels=['19-29','30-49','50-64','65+'];
  return <svg viewBox={`0 0 ${W} ${H}`} style={{width:'100%',height:'auto'}}>
    <line x1={pl} y1={pt} x2={pl} y2={pt+h} stroke="#E2E8F0"/>
    <line x1={pl} y1={pt+h} x2={pl+w} y2={pt+h} stroke="#E2E8F0"/>
    {[.25,.5,.75,1].map(t=><g key={t}>
      <line x1={pl} y1={pt+h*(1-t)} x2={pl+w} y2={pt+h*(1-t)} stroke="#F1F5F9"/>
      <text x={pl-4} y={pt+h*(1-t)+4} textAnchor="end" fontSize="10" fill="#94A3B8">{Math.round(t*maxV)}</text>
    </g>)}
    {ageGroups.map((ag,i)=>{
      const cx=pl+spacing*(i+.5);
      const mv=getP95(maleRows[i]),fv=getP95(femaleRows[i]);
      const mh=(mv/maxV)*h,fh=(fv/maxV)*h;
      return <g key={ag}>
        <rect x={cx-(barW*2+gap)/2} y={pt+h-mh} width={barW} height={mh} fill="#3B82F6" opacity=".8" rx="2"/>
        <rect x={cx-(barW*2+gap)/2+barW+gap} y={pt+h-fh} width={barW} height={fh} fill="#F472B6" opacity=".8" rx="2"/>
        <text x={cx} y={pt+h+14} textAnchor="middle" fontSize="11" fill="#475569">{labels[i]}</text>
        {mv>0&&<text x={cx-(barW*2+gap)/2+barW/2} y={pt+h-mh-4} textAnchor="middle" fontSize="9" fill="#2563EB">{mv.toFixed(0)}</text>}
        {fv>0&&<text x={cx-(barW*2+gap)/2+barW+gap+barW/2} y={pt+h-fh-4} textAnchor="middle" fontSize="9" fill="#DB2777">{fv.toFixed(0)}</text>}
      </g>;
    })}
    <g transform={`translate(${pl+20},${H-12})`}>
      <rect width="12" height="10" fill="#3B82F6" opacity=".8" rx="2"/>
      <text x="16" y="9" fontSize="11" fill="#475569">남자 P95</text>
      <rect x="70" width="12" height="10" fill="#F472B6" opacity=".8" rx="2"/>
      <text x="86" y="9" fontSize="11" fill="#475569">여자 P95</text>
    </g>
  </svg>;
}
