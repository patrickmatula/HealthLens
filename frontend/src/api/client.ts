import type {
  BodyMeasurementTypeKey,
  BodyOverviewDto,
  BodyProfileDto,
  BodySideKey,
  CorrelationPointDto,
  DashboardOverviewDto,
  GoogleHealthStatusDto,
  GoogleHealthSyncResultDto,
  ImportCurrentDto,
  ImportScope,
  ImportStartedDto,
  ImportStatusDto,
  HeartOverviewDto,
  PersonalRecordDto,
  RecoveryOverviewDto,
  ShoeDto,
  SleepSessionDetailDto,
  SleepSummaryDto,
  WorkoutDetailDto,
  WorkoutListItemDto,
} from './types'

// HealthLens has no login by design (self-hosted, meant for your own trusted network only — see
// README). That still leaves state-changing requests open to being blindly triggered by a malicious
// page a browser on that network happens to visit (a classic CSRF pattern, just without a session to
// steal). Since fetch/XHR can set arbitrary headers but a plain cross-site <form> submit cannot, this
// header on every mutating request forces the browser into a CORS preflight; with no CORS policy
// granting cross-origin access in production, that preflight fails and the browser blocks the actual
// request before it ever reaches the server -- so this needs no verification at all beyond "does the
// server require it," see the matching check in Program.cs.
const ANTI_CSRF_HEADER = 'X-HealthLens-Client'

async function get<T>(path: string): Promise<T> {
  const res = await fetch(path)
  if (!res.ok) {
    throw new Error(`${path} -> ${res.status}`)
  }
  return res.json() as Promise<T>
}

async function send<T>(method: 'POST' | 'PUT' | 'DELETE', path: string, body?: unknown): Promise<T> {
  const res = await fetch(path, {
    method,
    headers: {
      [ANTI_CSRF_HEADER]: '1',
      ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
    },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })
  if (!res.ok) {
    throw new Error(await res.text())
  }
  return res.status === 204 ? (undefined as T) : ((await res.json()) as T)
}

export const api = {
  importCurrent: () => get<ImportCurrentDto>('/api/import/current'),

  importStatus: (jobId: number) => get<ImportStatusDto>(`/api/import/${jobId}/status`),

  startImport: async (file: File, persistent: boolean, scope: ImportScope): Promise<ImportStartedDto> => {
    const form = new FormData()
    form.append('file', file)
    form.append('persistent', String(persistent))
    form.append('scope', scope)
    const res = await fetch('/api/import', { method: 'POST', headers: { [ANTI_CSRF_HEADER]: '1' }, body: form })
    if (!res.ok) {
      throw new Error(await res.text())
    }
    return res.json() as Promise<ImportStartedDto>
  },

  dashboardOverview: (params: { preset?: string; from?: string; to?: string; lang?: string }) => {
    const qs = new URLSearchParams()
    if (params.preset) qs.set('preset', params.preset)
    if (params.from) qs.set('from', params.from)
    if (params.to) qs.set('to', params.to)
    if (params.lang) qs.set('lang', params.lang)
    return get<DashboardOverviewDto>(`/api/dashboard/overview?${qs.toString()}`)
  },

  workouts: (params: { preset?: string; from?: string; to?: string; activity?: string; shoeId?: number }) => {
    const qs = new URLSearchParams()
    if (params.preset) qs.set('preset', params.preset)
    if (params.from) qs.set('from', params.from)
    if (params.to) qs.set('to', params.to)
    if (params.activity) qs.set('activity', params.activity)
    if (params.shoeId != null) qs.set('shoeId', String(params.shoeId))
    return get<WorkoutListItemDto[]>(`/api/workouts?${qs.toString()}`)
  },

  workoutDetail: (id: string) => get<WorkoutDetailDto>(`/api/workouts/${id}`),

  personalRecords: () => get<PersonalRecordDto[]>('/api/workouts/personal-records'),

  bestPersonalRecords: () => get<PersonalRecordDto[]>('/api/workouts/personal-records/best'),

  sleepSummary: (params: { preset?: string; from?: string; to?: string }) => {
    const qs = new URLSearchParams()
    if (params.preset) qs.set('preset', params.preset)
    if (params.from) qs.set('from', params.from)
    if (params.to) qs.set('to', params.to)
    return get<SleepSummaryDto>(`/api/sleep?${qs.toString()}`)
  },

  sleepDetail: (id: string) => get<SleepSessionDetailDto>(`/api/sleep/${id}`),

  heartOverview: (params: { preset?: string; from?: string; to?: string }) => {
    const qs = new URLSearchParams()
    if (params.preset) qs.set('preset', params.preset)
    if (params.from) qs.set('from', params.from)
    if (params.to) qs.set('to', params.to)
    return get<HeartOverviewDto>(`/api/heart/overview?${qs.toString()}`)
  },

  recoveryOverview: (params: { preset?: string; from?: string; to?: string }) => {
    const qs = new URLSearchParams()
    if (params.preset) qs.set('preset', params.preset)
    if (params.from) qs.set('from', params.from)
    if (params.to) qs.set('to', params.to)
    return get<RecoveryOverviewDto>(`/api/recovery/overview?${qs.toString()}`)
  },

  correlation: () => get<CorrelationPointDto[]>('/api/recovery/correlation'),

  shoes: () => get<ShoeDto[]>('/api/shoes'),

  createShoe: (name: string, brand: string | null) => send<ShoeDto>('POST', '/api/shoes', { name, brand }),

  updateShoe: (id: number, name: string, brand: string | null, isRetired: boolean) =>
    send<ShoeDto>('PUT', `/api/shoes/${id}`, { name, brand, isRetired }),

  deleteShoe: (id: number) => send<void>('DELETE', `/api/shoes/${id}`),

  assignShoe: (shoeId: number | null, workoutIds: string[]) => send<void>('POST', '/api/shoes/assign', { shoeId, workoutIds }),

  bodyProfile: () => get<BodyProfileDto>('/api/body/profile'),

  updateBodyProfile: (heightCm: number | null, sex: 'male' | 'female' | null) =>
    send<BodyProfileDto>('PUT', '/api/body/profile', { heightCm, sex }),

  bodyOverview: (params: { preset?: string; from?: string; to?: string }) => {
    const qs = new URLSearchParams()
    if (params.preset) qs.set('preset', params.preset)
    if (params.from) qs.set('from', params.from)
    if (params.to) qs.set('to', params.to)
    return get<BodyOverviewDto>(`/api/body/overview?${qs.toString()}`)
  },

  submitBodyEntry: (date: string, values: { type: BodyMeasurementTypeKey; side: BodySideKey; value: number }[]) =>
    send<void>('POST', '/api/body/entry', { date, values }),

  deleteBodyEntry: (date: string) => send<void>('DELETE', `/api/body/entry/${date}`),

  googleHealthStatus: () => get<GoogleHealthStatusDto>('/api/googlehealth/status'),

  saveGoogleHealthConfig: (clientId: string, clientSecret: string) =>
    send<GoogleHealthStatusDto>('POST', '/api/googlehealth/config', { clientId, clientSecret }),

  disconnectGoogleHealth: () => send<void>('DELETE', '/api/googlehealth/config'),

  googleHealthAuthorizeUrl: () => get<{ url: string }>('/api/googlehealth/authorize'),

  syncGoogleHealth: () => send<GoogleHealthSyncResultDto>('POST', '/api/googlehealth/sync'),
}
