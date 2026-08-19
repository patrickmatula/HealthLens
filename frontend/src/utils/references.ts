import type { RangeZone } from '../components/ReferenceRangeGauge'

// ---------------------------------------------------------------------------
// Fitness profile — edit this to recalibrate every reference gauge in the app
// at once. The zone bands below are calibrated for a "trained" recreational
// athlete (the Garmin Running Dynamics population, roughly). Switch to
// 'average' to shift bands toward a general, less-trained population, or
// 'athletic' to shift toward a more competitive one. This is a code-level
// setting on purpose (per user request) rather than a UI toggle, since it
// reflects a factual assumption about the person using this app, not a
// display preference.
// ---------------------------------------------------------------------------
export type FitnessProfile = 'average' | 'trained' | 'athletic'
export const FITNESS_PROFILE: FitnessProfile = 'trained'

// Cadence/GCT/vertical-oscillation zones all shift by this many percent of
// their own domain span per profile step, keeping "better" at the same end.
const PROFILE_SHIFT_PERCENT: Record<FitnessProfile, number> = {
  average: -8,
  trained: 0,
  athletic: 8,
}

function shiftZones(zones: RangeZone[], domain: [number, number]): RangeZone[] {
  const shiftPercent = PROFILE_SHIFT_PERCENT[FITNESS_PROFILE]
  if (shiftPercent === 0) return zones
  const span = domain[1] - domain[0]
  const delta = (shiftPercent / 100) * span
  return zones.map((z) => ({ ...z, from: z.from + delta, to: z.to + delta }))
}

// Source: American Heart Association — "normal" resting heart rate for most adults is 60-100 bpm;
// well-conditioned athletes can be as low as 40 bpm; a persistently elevated resting HR above 100 bpm
// warrants medical evaluation. https://www.heart.org/en/health-topics/high-blood-pressure/the-facts-about-high-blood-pressure/all-about-heart-rate-pulse
export const RESTING_HR_DOMAIN: [number, number] = [40, 110]
export const RESTING_HR_ZONES: RangeZone[] = [
  { label: 'Sportlich', from: 40, to: 60, color: '#26a69a' },
  { label: 'Normal', from: 60, to: 100, color: '#43a047' },
  { label: 'Erhöht', from: 100, to: 110, color: '#e53935' },
]
export const RESTING_HR_SOURCE = 'American Heart Association'
export const RESTING_HR_SOURCE_URL =
  'https://www.heart.org/en/health-topics/high-blood-pressure/the-facts-about-high-blood-pressure/all-about-heart-rate-pulse'

export function restingHrAssessment(bpm: number): string {
  if (bpm < 60) {
    return 'Sportlich niedrig — bei gut trainierten Personen in der Regel unbedenklich und oft Zeichen guter Ausdauerfitness.'
  }
  if (bpm <= 100) {
    return 'Im Normalbereich für Erwachsene laut American Heart Association.'
  }
  return 'Erhöht — bei dauerhaft über 100 bpm liegendem Ruhepuls empfiehlt die American Heart Association eine ärztliche Abklärung.'
}

// Source: Garmin Running Dynamics reference bands, derived from percentile analysis across their
// running population (wrist/waist-pod bands); cadence floor per common running-injury research
// (<170 spm associated with higher injury risk). https://www8.garmin.com/manuals/webhelp/GUID-676967A0-1B23-4384-9BC9-76F3D643F1C8/EN-US/GUID-62A09512-518A-424A-8491-FE2B80CD2091.html
export const CADENCE_DOMAIN: [number, number] = [140, 200]
export const CADENCE_ZONES: RangeZone[] = shiftZones(
  [
    { label: 'Niedrig', from: 140, to: 153, color: '#e53935' },
    { label: 'Unterdurchschnittlich', from: 153, to: 164, color: '#fb8c00' },
    { label: 'Durchschnittlich', from: 164, to: 174, color: '#43a047' },
    { label: 'Gut', from: 174, to: 184, color: '#1e88e5' },
    { label: 'Sehr gut', from: 184, to: 200, color: '#8e24aa' },
  ],
  CADENCE_DOMAIN,
)

export const GCT_DOMAIN: [number, number] = [180, 340]
export const GCT_ZONES: RangeZone[] = shiftZones(
  [
    { label: 'Sehr gut', from: 180, to: 218, color: '#8e24aa' },
    { label: 'Gut', from: 218, to: 249, color: '#1e88e5' },
    { label: 'Durchschnittlich', from: 249, to: 278, color: '#43a047' },
    { label: 'Unterdurchschnittlich', from: 278, to: 308, color: '#fb8c00' },
    { label: 'Niedrig', from: 308, to: 340, color: '#e53935' },
  ],
  GCT_DOMAIN,
)

export const VERTICAL_OSC_DOMAIN: [number, number] = [4, 15]
export const VERTICAL_OSC_ZONES: RangeZone[] = shiftZones(
  [
    { label: 'Sehr gut', from: 4, to: 6.8, color: '#8e24aa' },
    { label: 'Gut', from: 6.8, to: 8.9, color: '#1e88e5' },
    { label: 'Durchschnittlich', from: 8.9, to: 10.9, color: '#43a047' },
    { label: 'Unterdurchschnittlich', from: 10.9, to: 13, color: '#fb8c00' },
    { label: 'Niedrig', from: 13, to: 15, color: '#e53935' },
  ],
  VERTICAL_OSC_DOMAIN,
)

export const RUNNING_DYNAMICS_SOURCE = 'Garmin Running Dynamics'
export const RUNNING_DYNAMICS_SOURCE_URL =
  'https://www8.garmin.com/manuals/webhelp/GUID-676967A0-1B23-4384-9BC9-76F3D643F1C8/EN-US/GUID-62A09512-518A-424A-8491-FE2B80CD2091.html'
