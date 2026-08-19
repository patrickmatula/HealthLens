import type {
  DashboardOverviewDto,
  ImportCurrentDto,
  ImportScope,
  ImportStartedDto,
  ImportStatusDto,
  PersonalRecordDto,
  SleepSessionDetailDto,
  SleepSummaryDto,
  WorkoutDetailDto,
  WorkoutListItemDto,
} from './types'

async function get<T>(path: string): Promise<T> {
  const res = await fetch(path)
  if (!res.ok) {
    throw new Error(`${path} -> ${res.status}`)
  }
  return res.json() as Promise<T>
}

export const api = {
  importCurrent: () => get<ImportCurrentDto>('/api/import/current'),

  importStatus: (jobId: number) => get<ImportStatusDto>(`/api/import/${jobId}/status`),

  startImport: async (file: File, persistent: boolean, scope: ImportScope): Promise<ImportStartedDto> => {
    const form = new FormData()
    form.append('file', file)
    form.append('persistent', String(persistent))
    form.append('scope', scope)
    const res = await fetch('/api/import', { method: 'POST', body: form })
    if (!res.ok) {
      throw new Error(await res.text())
    }
    return res.json() as Promise<ImportStartedDto>
  },

  dashboardOverview: (params: { preset?: string; from?: string; to?: string }) => {
    const qs = new URLSearchParams()
    if (params.preset) qs.set('preset', params.preset)
    if (params.from) qs.set('from', params.from)
    if (params.to) qs.set('to', params.to)
    return get<DashboardOverviewDto>(`/api/dashboard/overview?${qs.toString()}`)
  },

  workouts: (params: { preset?: string; from?: string; to?: string; activity?: string }) => {
    const qs = new URLSearchParams()
    if (params.preset) qs.set('preset', params.preset)
    if (params.from) qs.set('from', params.from)
    if (params.to) qs.set('to', params.to)
    if (params.activity) qs.set('activity', params.activity)
    return get<WorkoutListItemDto[]>(`/api/workouts?${qs.toString()}`)
  },

  workoutDetail: (id: string) => get<WorkoutDetailDto>(`/api/workouts/${id}`),

  personalRecords: () => get<PersonalRecordDto[]>('/api/workouts/personal-records'),

  sleepSummary: (params: { preset?: string; from?: string; to?: string }) => {
    const qs = new URLSearchParams()
    if (params.preset) qs.set('preset', params.preset)
    if (params.from) qs.set('from', params.from)
    if (params.to) qs.set('to', params.to)
    return get<SleepSummaryDto>(`/api/sleep?${qs.toString()}`)
  },

  sleepDetail: (id: string) => get<SleepSessionDetailDto>(`/api/sleep/${id}`),
}
