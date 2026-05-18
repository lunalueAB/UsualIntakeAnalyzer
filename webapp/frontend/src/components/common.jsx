import React from 'react'

/* ── Global CSS (injected once) ──────────────────────── */
const CSS = `
*{box-sizing:border-box;margin:0;padding:0}
body{font-family:'Segoe UI',system-ui,sans-serif;font-size:13px;color:#1E293B;background:#F8FAFC;overflow:hidden}
::-webkit-scrollbar{width:6px;height:6px}
::-webkit-scrollbar-track{background:transparent}
::-webkit-scrollbar-thumb{background:#CBD5E1;border-radius:3px}
button{font-family:inherit;cursor:pointer;border:none;outline:none}
input,select,textarea{font-family:inherit;font-size:13px;outline:none}
table{border-collapse:collapse;width:100%}
th,td{padding:7px 10px;text-align:left;border-bottom:1px solid #E2E8F0;font-size:12px;white-space:nowrap}
th{background:#F8FAFC;font-weight:600;color:#475569;font-size:11px;position:sticky;top:0;z-index:1}
tbody tr:hover td{background:#F1F5F9}
tbody tr.sel td{background:#DBEAFE !important}
tbody tr.warn td{background:#FEF3C7;color:#92400E}
`
if (!document.getElementById('app-css')) {
  const s = document.createElement('style')
  s.id = 'app-css'
  s.textContent = CSS
  document.head.appendChild(s)
}

/* ── Button ──────────────────────────────────────────── */
export function Btn({ children, variant = 'primary', onClick, disabled, style, small }) {
  const base = {
    border: 'none', borderRadius: 4, cursor: disabled ? 'not-allowed' : 'pointer',
    fontFamily: 'inherit', fontWeight: 600, fontSize: small ? 11 : 13,
    padding: small ? '4px 10px' : '7px 16px', display: 'inline-flex',
    alignItems: 'center', gap: 4, opacity: disabled ? .5 : 1, transition: 'all .15s',
  }
  const v = {
    primary: { background: '#2563EB', color: '#fff' },
    secondary: { background: '#fff', color: '#1E293B', border: '1px solid #CBD5E1' },
    ghost: { background: 'transparent', color: '#475569' },
    danger: { background: '#DC2626', color: '#fff' },
    success: { background: '#16A34A', color: '#fff' },
  }
  return (
    <button style={{ ...base, ...v[variant], ...style }}
      onClick={!disabled ? onClick : undefined}>{children}</button>
  )
}

/* ── Input ───────────────────────────────────────────── */
export function Input({ value, onChange, placeholder, style, type = 'text' }) {
  return (
    <input type={type} value={value} onChange={e => onChange(e.target.value)}
      placeholder={placeholder}
      style={{ border: '1px solid #CBD5E1', borderRadius: 4, padding: '7px 10px', width: '100%', background: '#fff', color: '#1E293B', ...style }} />
  )
}

/* ── Select ──────────────────────────────────────────── */
export function Sel({ value, onChange, children, style }) {
  return (
    <select value={value} onChange={e => onChange(e.target.value)}
      style={{ border: '1px solid #CBD5E1', borderRadius: 4, padding: '6px 10px', width: '100%', background: '#fff', color: '#1E293B', cursor: 'pointer', ...style }}>
      {children}
    </select>
  )
}

/* ── Checkbox ────────────────────────────────────────── */
export function Checkbox({ checked, onChange, label, disabled }) {
  return (
    <label style={{ display: 'flex', alignItems: 'flex-start', gap: 6, cursor: disabled ? 'default' : 'pointer', color: disabled ? '#94A3B8' : '#1E293B', fontSize: 12 }}>
      <input type="checkbox" checked={checked} onChange={e => onChange(e.target.checked)} disabled={disabled}
        style={{ width: 14, height: 14, marginTop: 1, cursor: 'pointer', accentColor: '#2563EB', flexShrink: 0 }} />
      {label}
    </label>
  )
}

/* ── Badge ───────────────────────────────────────────── */
export function Badge({ children, color = 'blue' }) {
  const map = {
    blue: { bg: '#DBEAFE', text: '#1D4ED8' }, green: { bg: '#DCFCE7', text: '#15803D' },
    amber: { bg: '#FEF3C7', text: '#92400E' }, red: { bg: '#FEE2E2', text: '#B91C1C' },
    gray: { bg: '#F1F5F9', text: '#475569' }, purple: { bg: '#EDE9FE', text: '#6D28D9' },
  }
  const s = map[color] || map.gray
  return (
    <span style={{ background: s.bg, color: s.text, borderRadius: 4, padding: '2px 7px', fontSize: 10, fontWeight: 700, whiteSpace: 'nowrap' }}>
      {children}
    </span>
  )
}

/* ── Modal ───────────────────────────────────────────── */
export function Modal({ title, subtitle, onClose, children, width = 520 }) {
  return (
    <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,.45)', zIndex: 200, display: 'flex', alignItems: 'center', justifyContent: 'center' }}
      onClick={e => e.target === e.currentTarget && onClose()}>
      <div style={{ background: '#fff', borderRadius: 8, width, maxWidth: '95vw', maxHeight: '90vh', display: 'flex', flexDirection: 'column', boxShadow: '0 20px 60px rgba(0,0,0,.25)' }}>
        <div style={{ padding: '18px 20px 14px', borderBottom: '1px solid #E2E8F0', flexShrink: 0 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
            <div>
              <div style={{ fontSize: 16, fontWeight: 700 }}>{title}</div>
              {subtitle && <div style={{ fontSize: 11, color: '#94A3B8', marginTop: 2 }}>{subtitle}</div>}
            </div>
            <button onClick={onClose} style={{ background: 'none', border: 'none', cursor: 'pointer', fontSize: 18, color: '#94A3B8', padding: '0 4px', lineHeight: 1 }}>✕</button>
          </div>
        </div>
        <div style={{ flex: 1, overflow: 'auto', padding: '0 20px 20px' }}>{children}</div>
      </div>
    </div>
  )
}

/* ── Toast ───────────────────────────────────────────── */
export function Toast({ message, type = 'info', onClose }) {
  const colors = { info: '#2563EB', success: '#16A34A', error: '#DC2626', warning: '#D97706' }
  React.useEffect(() => {
    const t = setTimeout(onClose, 3500)
    return () => clearTimeout(t)
  }, [onClose])
  return (
    <div style={{ position: 'fixed', bottom: 24, right: 24, zIndex: 300, background: '#1E293B', color: '#fff', padding: '10px 18px', borderRadius: 6, fontSize: 13, display: 'flex', alignItems: 'center', gap: 10, boxShadow: '0 4px 20px rgba(0,0,0,.3)', maxWidth: 400 }}>
      <div style={{ width: 4, height: '100%', position: 'absolute', left: 0, top: 0, bottom: 0, background: colors[type], borderRadius: '6px 0 0 6px' }} />
      <span style={{ marginLeft: 8 }}>{message}</span>
      <button onClick={onClose} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#94A3B8', fontSize: 16, marginLeft: 8 }}>✕</button>
    </div>
  )
}

/* ── Loading ─────────────────────────────────────────── */
export function Spinner({ size = 20 }) {
  return (
    <div style={{ width: size, height: size, border: `3px solid #E2E8F0`, borderTopColor: '#2563EB', borderRadius: '50%', animation: 'spin 0.7s linear infinite', display: 'inline-block' }} />
  )
}

/* ── Card ────────────────────────────────────────────── */
export function Card({ children, padding = '16px', style }) {
  return (
    <div style={{ border: '1px solid #E2E8F0', borderRadius: 8, background: '#fff', padding, ...style }}>
      {children}
    </div>
  )
}

/* ── Section header ──────────────────────────────────── */
export function SectionHeader({ title, subtitle }) {
  return (
    <div style={{ marginBottom: 12 }}>
      <div style={{ fontSize: 16, fontWeight: 700 }}>{title}</div>
      {subtitle && <div style={{ fontSize: 11, color: '#94A3B8', marginTop: 2 }}>{subtitle}</div>}
    </div>
  )
}

/* ── useApi hook ─────────────────────────────────────── */
export function useApi(apiFn, deps = []) {
  const [data, setData] = React.useState(null)
  const [loading, setLoading] = React.useState(true)
  const [error, setError] = React.useState(null)

  const load = React.useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const result = await apiFn()
      setData(result)
    } catch (e) {
      setError(e.message)
    } finally {
      setLoading(false)
    }
  }, deps)

  React.useEffect(() => { load() }, [load])
  return { data, loading, error, reload: load }
}
