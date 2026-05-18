export function Btn({children,variant='primary',onClick,disabled,style,small,type='button'}){
  const base={border:'none',borderRadius:4,cursor:disabled?'not-allowed':'pointer',
    fontFamily:'inherit',fontWeight:600,fontSize:small?11:13,
    padding:small?'4px 10px':'7px 16px',transition:'opacity .15s',
    display:'inline-flex',alignItems:'center',gap:4,opacity:disabled?.45:1,
    whiteSpace:'nowrap'};
  const v={
    primary: {background:'#2563EB',color:'#fff'},
    secondary:{background:'#fff',color:'#1E293B',border:'1px solid #CBD5E1'},
    ghost:   {background:'transparent',color:'#475569'},
    danger:  {background:'#DC2626',color:'#fff'},
    success: {background:'#16A34A',color:'#fff'},
  };
  return <button type={type} style={{...base,...v[variant],...style}}
    onClick={!disabled?onClick:undefined}>{children}</button>;
}

export function Input({value,onChange,placeholder,style,type='text'}){
  return <input type={type} value={value||''} onChange={e=>onChange(e.target.value)}
    placeholder={placeholder} style={{border:'1px solid #CBD5E1',borderRadius:4,
      padding:'7px 10px',width:'100%',background:'#fff',color:'#1E293B',...style}}/>;
}

export function Select({value,onChange,children,style}){
  return <select value={value||''} onChange={e=>onChange(e.target.value)}
    style={{border:'1px solid #CBD5E1',borderRadius:4,padding:'6px 10px',width:'100%',
      background:'#fff',color:'#1E293B',cursor:'pointer',...style}}>
    {children}
  </select>;
}

export function Checkbox({checked,onChange,label,disabled}){
  return <label style={{display:'flex',alignItems:'flex-start',gap:6,
    cursor:disabled?'default':'pointer',color:disabled?'#94A3B8':'#1E293B',fontSize:12}}>
    <input type="checkbox" checked={!!checked} onChange={e=>onChange(e.target.checked)} disabled={disabled}
      style={{width:14,height:14,marginTop:2,cursor:'pointer',accentColor:'#2563EB',flexShrink:0}}/>
    <span>{label}</span>
  </label>;
}

export function Badge({children,color='gray'}){
  const map={blue:{bg:'#DBEAFE',text:'#1D4ED8'},green:{bg:'#DCFCE7',text:'#15803D'},
    amber:{bg:'#FEF3C7',text:'#92400E'},red:{bg:'#FEE2E2',text:'#B91C1C'},
    gray:{bg:'#F1F5F9',text:'#475569'},purple:{bg:'#EDE9FE',text:'#6D28D9'}};
  const s=map[color]||map.gray;
  return <span style={{background:s.bg,color:s.text,borderRadius:4,padding:'2px 7px',
    fontSize:10,fontWeight:700,whiteSpace:'nowrap',display:'inline-block'}}>{children}</span>;
}

export function Modal({title,subtitle,onClose,children,width=520}){
  return <div style={{position:'fixed',inset:0,background:'rgba(0,0,0,.45)',zIndex:200,
    display:'flex',alignItems:'center',justifyContent:'center'}}
    onClick={e=>{if(e.target===e.currentTarget)onClose();}}>
    <div style={{background:'#fff',borderRadius:8,width,maxWidth:'95vw',maxHeight:'90vh',
      display:'flex',flexDirection:'column',boxShadow:'0 20px 60px rgba(0,0,0,.25)'}}>
      <div style={{padding:'18px 20px 14px',borderBottom:'1px solid #E2E8F0',flexShrink:0}}>
        <div style={{display:'flex',justifyContent:'space-between',alignItems:'flex-start'}}>
          <div>
            <div style={{fontSize:16,fontWeight:700}}>{title}</div>
            {subtitle&&<div style={{fontSize:11,color:'#94A3B8',marginTop:2}}>{subtitle}</div>}
          </div>
          <button onClick={onClose} style={{background:'none',border:'none',cursor:'pointer',
            fontSize:18,color:'#94A3B8',padding:'0 4px',lineHeight:1}}>✕</button>
        </div>
      </div>
      <div style={{flex:1,overflow:'auto',padding:'0 20px 20px'}}>{children}</div>
    </div>
  </div>;
}

export function Spinner({text='분석 중...'}){
  return <div style={{display:'flex',flexDirection:'column',alignItems:'center',gap:12,padding:40}}>
    <div style={{width:36,height:36,border:'3px solid #DBEAFE',borderTopColor:'#2563EB',
      borderRadius:'50%',animation:'spin 0.8s linear infinite'}}/>
    <div style={{color:'#475569',fontSize:12}}>{text}</div>
    <style>{`@keyframes spin{to{transform:rotate(360deg)}}`}</style>
  </div>;
}
