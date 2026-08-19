export type UnitSystem = 'metric' | 'imperial'

// Set by UnitsProvider (frontend/src/units/UnitsContext.tsx) whenever the user's unit preference
// changes, so every formatter below can stay unit-aware without threading a parameter through every
// call site across the app.
let currentUnitSystem: UnitSystem = 'metric'
export function setFormatUnitSystem(unit: UnitSystem) {
  currentUnitSystem = unit
}

const MILES_PER_KM = 0.621371

export function formatPace(secPerKm: number | null | undefined): string {
  if (secPerKm == null || !Number.isFinite(secPerKm) || secPerKm <= 0) return '–'
  if (currentUnitSystem === 'imperial') {
    const secPerMile = secPerKm / MILES_PER_KM
    const min = Math.floor(secPerMile / 60)
    const sec = Math.round(secPerMile % 60)
    return `${min}:${sec.toString().padStart(2, '0')}/mi`
  }
  const min = Math.floor(secPerKm / 60)
  const sec = Math.round(secPerKm % 60)
  return `${min}:${sec.toString().padStart(2, '0')}/km`
}

export function formatMinutes(minutes: number | null | undefined): string {
  if (minutes == null || !Number.isFinite(minutes)) return '–'
  const h = Math.floor(minutes / 60)
  const m = Math.round(minutes % 60)
  if (h > 0) return `${h}h ${m}min`
  return `${m}min`
}

export function formatDuration(totalSeconds: number | null | undefined): string {
  if (totalSeconds == null || !Number.isFinite(totalSeconds) || totalSeconds <= 0) return '–'
  const h = Math.floor(totalSeconds / 3600)
  const m = Math.floor((totalSeconds % 3600) / 60)
  const s = Math.round(totalSeconds % 60)
  if (h > 0) return `${h}:${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`
  return `${m}:${s.toString().padStart(2, '0')}`
}

export function formatDistanceKm(meters: number | null | undefined): string {
  if (meters == null) return '–'
  if (currentUnitSystem === 'imperial') {
    const miles = (meters / 1000) * MILES_PER_KM
    return `${miles.toLocaleString('de-AT', { maximumFractionDigits: 2, minimumFractionDigits: 2 })} mi`
  }
  return `${(meters / 1000).toLocaleString('de-AT', { maximumFractionDigits: 2, minimumFractionDigits: 2 })} km`
}

export function formatDateTime(iso: string): string {
  return new Date(iso + (iso.endsWith('Z') ? '' : 'Z')).toLocaleString('de-AT', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

export function formatDate(iso: string): string {
  return new Date(iso + (iso.endsWith('Z') ? '' : 'Z')).toLocaleDateString('de-AT', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  })
}

const RECORD_LABELS: Record<string, string> = {
  FASTEST_MILE_NAME: 'Schnellste Meile',
  FASTEST_KILOMETRE_NAME: 'Schnellster 1 km',
  FASTEST_2_KILOMETRES_NAME: 'Schnellste 2 km',
  FASTEST_5K_NAME: 'Schnellste 5 km',
  FASTEST_10K_NAME: 'Schnellste 10 km',
  FASTEST_2_MILES_NAME: 'Schnellste 2 Meilen',
  FARTHEST_RUN_NAME: 'Weitester Lauf',
}

export function recordLabel(name: string): string {
  return RECORD_LABELS[name] ?? name.replace(/_NAME$/, '').replace(/_/g, ' ')
}

export type WorkoutCategory = 'Lauf' | 'Spaziergang' | 'Rad' | 'Kraft' | 'Sonstiges'

/**
 * Fitbit's own activityName is inconsistent (generic "Workout"/"Structured Workout"/"Sport" entries are
 * common), so name keywords alone under-classify. Fall back to pace for those generic names: sustained
 * paces faster than ~8:00/km are essentially never walking, matching typical running-vs-walking pace
 * thresholds used by fitness apps.
 */
export function categorizeWorkout(w: { activityName: string; avgPaceSecPerKm: number | null }): WorkoutCategory {
  const name = w.activityName.toLowerCase()
  if (/(run|laufen|hike|wander)/.test(name)) return 'Lauf'
  if (/(walk|geh)/.test(name)) return 'Spaziergang'
  if (/(bike|cycl|rad)/.test(name)) return 'Rad'
  if (/(strength|hiit|aerobic|kraft|gym)/.test(name)) return 'Kraft'
  if (w.avgPaceSecPerKm != null) {
    return w.avgPaceSecPerKm < 480 ? 'Lauf' : 'Spaziergang'
  }
  return 'Sonstiges'
}

export function formatRecordValue(record: { recordType: string; recordValue: number }): string {
  if (record.recordType === 'PERSONAL_RECORD_TYPE_SHORTEST_TIME_FOR_DISTANCE') {
    return formatDuration(record.recordValue / 1000)
  }
  if (record.recordType === 'PERSONAL_RECORD_TYPE_LONGEST_DISTANCE') {
    return formatDistanceKm(record.recordValue / 1000)
  }
  return String(record.recordValue)
}
