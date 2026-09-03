import type { KmSplitDto, PersonalRecordDto, WorkoutSampleDto } from '../api/types'

export type PacingStrategy = 'negative' | 'positive' | 'even'

export interface PacingAnalysis {
  strategy: PacingStrategy
  firstHalfAvgSecPerKm: number
  secondHalfAvgSecPerKm: number
  deltaPercent: number
}

/**
 * Negative splits (2nd half faster than the 1st) are the single most reliable pacing strategy in
 * distance running -- nearly every world record and the vast majority of personal bests are run this
 * way. Compares the average km-split pace of the first vs second half of the run; ±1% counts as "even"
 * since GPS/split noise alone can easily produce that much variation.
 */
export function analyzePacingStrategy(kmSplits: KmSplitDto[]): PacingAnalysis | null {
  if (kmSplits.length < 4) return null // too few splits for a meaningful half/half comparison

  const half = Math.floor(kmSplits.length / 2)
  const firstHalf = kmSplits.slice(0, half)
  const secondHalf = kmSplits.slice(kmSplits.length - half)

  const firstAvg = firstHalf.reduce((sum, s) => sum + s.durationSeconds, 0) / firstHalf.length
  const secondAvg = secondHalf.reduce((sum, s) => sum + s.durationSeconds, 0) / secondHalf.length
  const deltaPercent = ((secondAvg - firstAvg) / firstAvg) * 100
  const strategy: PacingStrategy = deltaPercent < -1 ? 'negative' : deltaPercent > 1 ? 'positive' : 'even'

  return { strategy, firstHalfAvgSecPerKm: firstAvg, secondHalfAvgSecPerKm: secondAvg, deltaPercent }
}

export interface DecouplingAnalysis {
  decouplingPercent: number
}

/**
 * Aerobic decoupling (Pa:HR / "efficiency factor" drift): compares speed-per-heartbeat in the first vs
 * second half of the run. A positive decoupling% means efficiency dropped in the second half (heart rate
 * climbed relative to pace) -- the standard marker of fading aerobic durability. <5% is considered
 * excellent, >10% suggests notable fatigue or an under-fueled/under-hydrated effort. Split by elapsed
 * time rather than distance -- simpler, and the metric is about how the same body responds to load over
 * the course of the effort, not about a specific distance boundary.
 */
export function analyzeDecoupling(samples: WorkoutSampleDto[]): DecouplingAnalysis | null {
  const valid = samples.filter((s) => s.heartRateBpm != null && s.heartRateBpm > 0 && s.speedKmh != null && s.speedKmh > 0)
  if (valid.length < 20) return null

  const half = Math.floor(valid.length / 2)
  const firstHalf = valid.slice(0, half)
  const secondHalf = valid.slice(valid.length - half)

  const efficiency = (points: typeof valid) => {
    const avgSpeed = points.reduce((sum, p) => sum + p.speedKmh!, 0) / points.length
    const avgHr = points.reduce((sum, p) => sum + p.heartRateBpm!, 0) / points.length
    return avgSpeed / avgHr
  }

  const firstEf = efficiency(firstHalf)
  const secondEf = efficiency(secondHalf)
  if (firstEf <= 0) return null

  return { decouplingPercent: ((firstEf - secondEf) / firstEf) * 100 }
}

// Minetti et al. (2002) energy cost of running on a gradient -- the same physiological model behind
// Strava's and Garmin's own Grade Adjusted Pace. C_r(0) = 3.6 J/(kg*m) is the flat-ground cost.
const FLAT_COST = 3.6

function minettiCost(gradeDecimal: number): number {
  const i = gradeDecimal
  return 155.4 * i ** 5 - 30.4 * i ** 4 - 43.3 * i ** 3 + 46.3 * i ** 2 + 19.5 * i + 3.6
}

function haversineMeters(lat1: number, lon1: number, lat2: number, lon2: number): number {
  const earthRadiusMeters = 6371000
  const dLat = ((lat2 - lat1) * Math.PI) / 180
  const dLon = ((lon2 - lon1) * Math.PI) / 180
  const a = Math.sin(dLat / 2) ** 2 + Math.cos((lat1 * Math.PI) / 180) * Math.cos((lat2 * Math.PI) / 180) * Math.sin(dLon / 2) ** 2
  return earthRadiusMeters * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a))
}

// Minimum horizontal distance per grade segment. Computing grade point-to-point between consecutive
// ~1-second GPS fixes was tried first and produced absurd results (e.g. an 8:15/km run "adjusting" to
// 5:16/km on a course with only 53m of total elevation gain over 15km): consumer GPS position noise and
// barometric altitude noise are both large relative to the few meters covered in one second, so
// point-to-point grade is dominated by sensor noise, not the real slope. Aggregating into ~30m chunks
// before computing elevation-change/distance gives the noise room to average out while still resolving
// real hills (a 30m segment is still short compared to any meaningful climb or descent).
const MIN_SEGMENT_METERS = 30

/**
 * Grade Adjusted Pace: maps actual pace onto the "flat-ground-equivalent effort" pace, so a hilly run can
 * be fairly compared to a flat one. A segment with no altitude data on either end is treated as flat
 * rather than dropped, so a GPS track with sparse altitude fixes still produces a reasonable (if slightly
 * conservative) result instead of silently under-counting distance. Grade is clamped to ±45%, the range
 * Minetti's model was validated over.
 */
export function computeGradeAdjustedPace(samples: WorkoutSampleDto[]): number | null {
  const ordered = samples
    .filter((s) => s.latitude != null && s.longitude != null)
    .slice()
    .sort((a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime())
  if (ordered.length < 2) return null

  let totalFlatEquivalentMeters = 0
  let totalSeconds = 0
  let segmentStart = ordered[0]
  let segmentDistance = 0

  for (let i = 1; i < ordered.length; i++) {
    const prev = ordered[i - 1]
    const cur = ordered[i]
    segmentDistance += haversineMeters(prev.latitude!, prev.longitude!, cur.latitude!, cur.longitude!)

    const isLast = i === ordered.length - 1
    if (segmentDistance < MIN_SEGMENT_METERS && !isLast) continue
    if (segmentDistance <= 0) {
      segmentStart = cur
      continue
    }

    if (segmentStart.altitudeMeters != null && cur.altitudeMeters != null) {
      const elevationDelta = cur.altitudeMeters - segmentStart.altitudeMeters
      const grade = Math.max(-0.45, Math.min(0.45, elevationDelta / segmentDistance))
      totalFlatEquivalentMeters += segmentDistance * (minettiCost(grade) / FLAT_COST)
    } else {
      totalFlatEquivalentMeters += segmentDistance
    }

    totalSeconds += (new Date(cur.timestamp).getTime() - new Date(segmentStart.timestamp).getTime()) / 1000
    segmentStart = cur
    segmentDistance = 0
  }

  if (totalFlatEquivalentMeters < 100 || totalSeconds <= 0) return null
  return (totalSeconds / totalFlatEquivalentMeters) * 1000
}

export const RACE_PREDICTION_DISTANCES = [
  { meters: 1000, key: '1k' },
  { meters: 1609.344, key: 'mile' },
  { meters: 5000, key: '5k' },
  { meters: 10000, key: '10k' },
  { meters: 21097.5, key: 'half' },
  { meters: 42195, key: 'marathon' },
] as const

export interface RacePredictionResult {
  anchorMeters: number
  anchorSeconds: number
  predictions: { meters: number; key: string; seconds: number }[]
}

/**
 * Riegel formula (Riegel, 1977): T2 = T1 * (D2/D1)^1.06 -- about 80% accurate overall, and most reliable
 * extrapolating between similar distances (within ~2x); it degrades noticeably over a wide distance ratio
 * like 5K -> marathon, which the UI discloses rather than hides. Anchors on the longest available
 * distance-based PR (5K+) rather than the shortest, since a middle/long-distance time predicts other
 * distances more reliably than extrapolating far out from something like a 1K best.
 */
export function predictRaceTimes(bestRecords: PersonalRecordDto[]): RacePredictionResult | null {
  const distanceRecords = bestRecords.filter(
    (r) => r.recordType === 'PERSONAL_RECORD_TYPE_SHORTEST_TIME_FOR_DISTANCE' && r.extentValueMeters != null && r.extentValueMeters >= 3000,
  )
  if (distanceRecords.length === 0) return null

  const anchor = distanceRecords.reduce((best, r) => (r.extentValueMeters! > best.extentValueMeters! ? r : best))
  const anchorMeters = anchor.extentValueMeters!
  const anchorSeconds = anchor.recordValue / 1000

  const predictions = RACE_PREDICTION_DISTANCES.filter((d) => Math.abs(d.meters - anchorMeters) > 50).map((d) => ({
    meters: d.meters,
    key: d.key,
    seconds: anchorSeconds * (d.meters / anchorMeters) ** 1.06,
  }))

  return { anchorMeters, anchorSeconds, predictions }
}
