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

export interface WorkoutListItemDto {
  id: string
  startUtc: string
  endUtc: string
  durationSeconds: number
  activityName: string
  distanceMeters: number | null
  calories: number | null
  avgHeartRate: number | null
  avgPaceSecPerKm: number | null
  hasGps: boolean
  isLegacy: boolean
}

export interface WorkoutSplitDto {
  splitIndex: number
  type: string
  timestamp: string
  elapsedMs: number
  distanceMeters: number
  calories: number
  steps: number
  avgHeartRate: number | null
  elevationGainMeters: number
  avgSpeedKmh: number | null
}

export interface WorkoutSampleDto {
  timestamp: string
  latitude: number | null
  longitude: number | null
  altitudeMeters: number | null
  heartRateBpm: number | null
  paceSecPerKm: number | null
  cadenceSpm: number | null
  speedKmh: number | null
  strideLengthCm: number | null
  verticalOscillationCm: number | null
  verticalRatioPercent: number | null
  groundContactTimeMs: number | null
}

export interface PersonalRecordDto {
  id: number
  workoutId: string | null
  nameLocalizationId: string
  state: string
  achieveTimeUtc: string
  recordValue: number
  recordType: string
  extentValueMeters: number | null
  workoutActivityName: string | null
}

export interface SleepStageDto {
  stageType: string
  startUtc: string
  endUtc: string
}

export interface SleepScoreDto {
  overallScore: number
  durationScore: number | null
  compositionScore: number | null
  revitalizationScore: number | null
  deepSleepMinutes: number
  remSleepPercent: number
  restingHeartRate: number | null
  restlessnessNormalized: number | null
}

export interface SleepSessionListItemDto {
  id: string
  startUtc: string
  endUtc: string
  sleepType: string
  minutesAsleep: number
  minutesAwake: number
  timeInBedMinutes: number
  overallScore: number | null
  isLegacy: boolean
}

export interface SleepSessionDetailDto {
  id: string
  startUtc: string
  endUtc: string
  sleepType: string
  dataSource: string | null
  minutesAsleep: number
  minutesAwake: number
  minutesToFallAsleep: number
  minutesAfterWakeup: number
  timeInBedMinutes: number
  efficiencyPercent: number | null
  isLegacy: boolean
  stages: SleepStageDto[]
  score: SleepScoreDto | null
}

export interface SleepSummaryDto {
  from: string
  to: string
  nights: number
  avgMinutesAsleep: number
  avgTimeInBedMinutes: number
  avgEfficiencyPercent: number
  avgOverallScore: number | null
  sessions: SleepSessionListItemDto[]
}

export interface WorkoutDetailDto extends WorkoutListItemDto {
  logType: string
  source: string | null
  steps: number | null
  peakHeartRate: number | null
  avgSpeedKmh: number | null
  peakSpeedKmh: number | null
  elevationGainMeters: number | null
  cardioLoad: number | null
  cadenceAvgSpm: number | null
  groundContactTimeMs: number | null
  verticalOscillationMm: number | null
  verticalRatioPercent: number | null
  ratePerceivedExertion: number | null
  splits: WorkoutSplitDto[]
  samples: WorkoutSampleDto[]
  personalRecords: PersonalRecordDto[]
}
