import axios from 'axios'

const http = axios.create({ baseURL: '/api', timeout: 300000 })

export const api = {
  // Sources
  getSources:      ()          => http.get('/sources/projects').then(r=>r.data),
  addProject:      (b)         => http.post('/sources/projects', b).then(r=>r.data),
  updateProject:   (id,b)      => http.put(`/sources/projects/${id}`,b).then(r=>r.data),
  deleteProject:   (id)        => http.delete(`/sources/projects/${id}`).then(r=>r.data),
  addPhase:        (b)         => http.post('/sources/phases',b).then(r=>r.data),
  updatePhase:     (id,b)      => http.put(`/sources/phases/${id}`,b).then(r=>r.data),
  deletePhase:     (id)        => http.delete(`/sources/phases/${id}`).then(r=>r.data),
  addRound:        (b)         => http.post('/sources/rounds',b).then(r=>r.data),
  updateRound:     (id,b)      => http.put(`/sources/rounds/${id}`,b).then(r=>r.data),
  deleteRound:     (id)        => http.delete(`/sources/rounds/${id}`).then(r=>r.data),

  // Datasets
  getDatasets:     (p)         => http.get('/datasets', {params:p}).then(r=>r.data),
  uploadDataset:   (fd)        => http.post('/datasets', fd, {headers:{'Content-Type':'multipart/form-data'}}).then(r=>r.data),
  updateDataset:   (id,b)      => http.put(`/datasets/${id}`,b).then(r=>r.data),
  deleteDataset:   (id)        => http.delete(`/datasets/${id}`).then(r=>r.data),
  downloadDataset: (id)        => { window.open(`/api/datasets/${id}/download`,'_blank') },

  // Groups
  getGroups:       ()          => http.get('/groups').then(r=>r.data),
  createGroup:     (b)         => http.post('/groups',b).then(r=>r.data),
  updateGroup:     (id,b)      => http.put(`/groups/${id}`,b).then(r=>r.data),
  deleteGroup:     (id)        => http.delete(`/groups/${id}`).then(r=>r.data),
  getCodes:        (id)        => http.get(`/groups/${id}/codes`).then(r=>r.data),
  setCodes:        (id,codes)  => http.put(`/groups/${id}/codes`,codes).then(r=>r.data),

  // Scenarios
  getScenarios:    ()          => http.get('/scenarios').then(r=>r.data),
  createScenario:  (b)         => http.post('/scenarios',b).then(r=>r.data),
  deleteScenario:  (id)        => http.delete(`/scenarios/${id}`).then(r=>r.data),
  getScenarioResult:(id)       => http.get(`/scenarios/${id}/result`).then(r=>r.data),


  // Presets
  getPresets:      ()          => http.get('/presets').then(r=>r.data),
  createPreset:    (b)         => http.post('/presets',b).then(r=>r.data),
  updatePreset:    (id,b)      => http.put(`/presets/${id}`,b).then(r=>r.data),
  deletePreset:    (id)        => http.delete(`/presets/${id}`).then(r=>r.data),

  // Analysis
  runAnalysis:     (b)         => http.post('/analysis/run',b).then(r=>r.data),
  getHistory:      ()          => http.get('/analysis/history').then(r=>r.data),
}
