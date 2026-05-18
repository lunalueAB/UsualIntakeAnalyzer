/**
 * API client — thin wrapper around fetch.
 * All requests go to /api/* (proxied to FastAPI in dev, served directly in prod).
 */
const BASE = '/api'

async function req(method, path, body, isForm = false) {
  const opts = {
    method,
    headers: isForm ? {} : (body ? { 'Content-Type': 'application/json' } : {}),
    body: isForm ? body : (body ? JSON.stringify(body) : undefined),
  }
  const res = await fetch(`${BASE}${path}`, opts)
  if (!res.ok) {
    const text = await res.text().catch(() => '')
    let msg = text
    try { msg = JSON.parse(text)?.detail || text } catch {}
    throw new Error(msg || `HTTP ${res.status}`)
  }
  if (res.status === 204) return null
  const ct = res.headers.get('content-type') || ''
  return ct.includes('application/json') ? res.json() : res.text()
}

// ── Datasets ─────────────────────────────────────────────
export const api = {
  // Datasets
  listDatasets: () => req('GET', '/datasets'),
  uploadDataset: (form) => req('POST', '/datasets/upload', form, true),
  deleteDataset: (id) => req('DELETE', `/datasets/${id}`),
  previewDataset: (id) => req('GET', `/datasets/${id}/preview`),
  uploadCodebook: (form) => req('POST', '/datasets/codebook/upload', form, true),
  getCodebook: () => req('GET', '/datasets/codebook/info'),

  // Sources
  getTree: () => req('GET', '/sources/tree'),
  createProject: (b) => req('POST', '/sources/projects', b),
  updateProject: (id, b) => req('PUT', `/sources/projects/${id}`, b),
  deleteProject: (id) => req('DELETE', `/sources/projects/${id}`),
  createPhase: (b) => req('POST', '/sources/phases', b),
  updatePhase: (id, b) => req('PUT', `/sources/phases/${id}`, b),
  deletePhase: (id) => req('DELETE', `/sources/phases/${id}`),
  createRound: (b) => req('POST', '/sources/rounds', b),
  updateRound: (id, b) => req('PUT', `/sources/rounds/${id}`, b),
  deleteRound: (id) => req('DELETE', `/sources/rounds/${id}`),

  // Food Groups
  listGroups: () => req('GET', '/groups'),
  createGroup: (b) => req('POST', '/groups', b),
  updateGroup: (id, b) => req('PUT', `/groups/${id}`, b),
  deleteGroup: (id) => req('DELETE', `/groups/${id}`),

  // Analysis
  runAnalysis: (b) => req('POST', '/analysis/run', b),
  listHistory: () => req('GET', '/analysis/history'),
  getScenario: (id) => req('GET', `/analysis/history/${id}`),
}
