export function formatPace(secPerKm: number | null | undefined): string {
  if (secPerKm == null || !Number.isFinite(secPerKm) || secPerKm <= 0) return '–'
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

export function formatRecordValue(record: { recordType: string; recordValue: number }): string {
  if (record.recordType === 'PERSONAL_RECORD_TYPE_SHORTEST_TIME_FOR_DISTANCE') {
    return formatDuration(record.recordValue / 1000)
  }
  if (record.recordType === 'PERSONAL_RECORD_TYPE_LONGEST_DISTANCE') {
    return formatDistanceKm(record.recordValue / 1000)
  }
  return String(record.recordValue)
}
