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
  workoutsInRange: number
  avgSleepScore: number | null
  avgRestingHeartRate: number | null
  insights: string[]
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
  shoeId: number | null
  shoeName: string | null
}

export interface ShoeDto {
  id: number
  name: string
  brand: string | null
  isRetired: boolean
  createdAtUtc: string
  workoutCount: number
  totalDistanceMeters: number
}

export const BODY_MEASUREMENT_TYPES = [
  'WeightKg',
  'BodyFatPercent',
  'WaistCm',
  'HipCm',
  'ChestCm',
  'NeckCm',
  'BicepCm',
  'ThighCm',
  'CalfCm',
] as const

export type BodyMeasurementTypeKey = (typeof BODY_MEASUREMENT_TYPES)[number]

// Bicep/thigh/calf come in pairs; everything else is always 'None'.
export const BODY_PAIRED_TYPES: readonly BodyMeasurementTypeKey[] = ['BicepCm', 'ThighCm', 'CalfCm']
export type BodySideKey = 'None' | 'Left' | 'Right'

export interface BodyProfileDto {
  heightCm: number | null
  sex: 'male' | 'female' | null
}

export interface BodyMeasurementPointDto {
  date: string
  type: BodyMeasurementTypeKey
  side: BodySideKey
  value: number
}

export interface BodyOverviewDto {
  profile: BodyProfileDto
  measurements: BodyMeasurementPointDto[]
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

export interface RestingHeartRatePointDto {
  date: string
  bpm: number
}

export interface AzmDayDto {
  date: string
  fatBurnMinutes: number
  cardioMinutes: number
  peakMinutes: number
}

export interface HrvDayDto {
  date: string
  rmssdMs: number
  nonRemHrBpm: number
  entropy: number
}

export interface RespiratoryRatePointDto {
  date: string
  breathsPerMinute: number
}

export interface HeartOverviewDto {
  from: string
  to: string
  avgRestingHeartRate: number | null
  avgHrv: number | null
  restingHeartRate: RestingHeartRatePointDto[]
  activeZoneMinutes: AzmDayDto[]
  hrv: HrvDayDto[]
  respiratoryRate: RespiratoryRatePointDto[]
}

export interface StressScorePointDto {
  date: string
  score: number
}

export interface ReadinessPointDto {
  date: string
  score: number
  level: string
}

export interface SpO2PointDto {
  date: string
  averagePercent: number
  lowerBoundPercent: number
  upperBoundPercent: number
}

export interface TemperaturePointDto {
  date: string
  nightlyCelsius: number
  baselineCelsius: number | null
  deltaFromBaseline: number | null
}

export interface RecoveryOverviewDto {
  from: string
  to: string
  stressScore: StressScorePointDto[]
  readiness: ReadinessPointDto[]
  spO2: SpO2PointDto[]
  temperature: TemperaturePointDto[]
}

export interface CorrelationPointDto {
  date: string
  sleepScore: number
  restingHeartRate: number | null
  stressScore: number | null
  readinessScore: number | null
}

export interface KmSplitDto {
  km: number
  durationSeconds: number
  avgHeartRate: number | null
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
  kmSplits: KmSplitDto[]
  samples: WorkoutSampleDto[]
  personalRecords: PersonalRecordDto[]
}

export interface GoogleHealthStatusDto {
  configured: boolean
  connected: boolean
  lastSyncUtc: string | null
  lastSyncSummary: string | null
  lastError: string | null
  redirectUri: string
}

export interface GoogleHealthSyncResultDto {
  activityDaysSynced: number
  restingHeartRateDaysSynced: number
  weightEntriesSynced: number
  warnings: string[]
}
