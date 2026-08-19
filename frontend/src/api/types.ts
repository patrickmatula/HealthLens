export type ImportScope = 'Curated' | 'Full'

export interface ImportCurrentDto {
  hasData: boolean
  mode: 'Persistent' | 'Ephemeral'
  lastImportAtUtc: string | null
  lastScope: ImportScope | null
  rowsImported: number | null
}

export interface ImportStartedDto {
  jobId: number
}

export interface ImportStatusDto {
  id: number
  status: 'Running' | 'Completed' | 'Failed'
  currentStep: string
  progressPercent: number
  rowsImported: number
  errorMessage: string | null
}

export interface DailyActivityPointDto {
  date: string
  steps: number | null
  distanceMeters: number | null
  caloriesTotal: number | null
  activeMinutes: number | null
  sedentaryMinutes: number | null
}

export interface DashboardOverviewDto {
  from: string
  to: string
  daysWithData: number
  totalSteps: number
  totalDistanceMeters: number
  totalCalories: number
  avgStepsPerDay: number
  avgActiveMinutesPerDay: number
  days: DailyActivityPointDto[]
}

export type TimeframePreset = '1d' | '7d' | '30d' | '1y' | 'all' | 'custom'
