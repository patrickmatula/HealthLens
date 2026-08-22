import type { RangeZone } from '../components/ReferenceRangeGauge'

type Language = 'de' | 'en'

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

// Shared 5-step "how good is this" scale used by the running-dynamics gauges below.
const SCALE_LABELS: Record<Language, [string, string, string, string, string]> = {
  de: ['Niedrig', 'Unterdurchschnittlich', 'Durchschnittlich', 'Gut', 'Sehr gut'],
  en: ['Low', 'Below average', 'Average', 'Good', 'Very good'],
}

// Source: American Heart Association — "normal" resting heart rate for most adults is 60-100 bpm;
// well-conditioned athletes can be as low as 40 bpm; a persistently elevated resting HR above 100 bpm
// warrants medical evaluation. https://www.heart.org/en/health-topics/high-blood-pressure/the-facts-about-high-blood-pressure/all-about-heart-rate-pulse
export const RESTING_HR_DOMAIN: [number, number] = [40, 110]

const RESTING_HR_LABELS: Record<Language, [string, string, string]> = {
  de: ['Sportlich', 'Normal', 'Erhöht'],
  en: ['Athletic', 'Normal', 'Elevated'],
}

export function getRestingHrZones(language: Language): RangeZone[] {
  const [athletic, normal, elevated] = RESTING_HR_LABELS[language]
  return [
    { label: athletic, from: 40, to: 60, color: '#1e88e5' },
    { label: normal, from: 60, to: 100, color: '#43a047' },
    { label: elevated, from: 100, to: 110, color: '#e53935' },
  ]
}

export const RESTING_HR_SOURCE = 'American Heart Association'
export const RESTING_HR_SOURCE_URL =
  'https://www.heart.org/en/health-topics/high-blood-pressure/the-facts-about-high-blood-pressure/all-about-heart-rate-pulse'

export function restingHrAssessment(bpm: number, language: Language): string {
  if (language === 'en') {
    if (bpm < 60) {
      return 'Athletically low — usually unremarkable for well-trained individuals and often a sign of good endurance fitness.'
    }
    if (bpm <= 100) {
      return 'Within the normal range for adults per the American Heart Association.'
    }
    return 'Elevated — the American Heart Association recommends medical evaluation for a resting heart rate persistently above 100 bpm.'
  }
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
export function getCadenceZones(language: Language): RangeZone[] {
  const [low, belowAvg, avg, good, veryGood] = SCALE_LABELS[language]
  return shiftZones(
    [
      { label: low, from: 140, to: 153, color: '#e53935' },
      { label: belowAvg, from: 153, to: 164, color: '#fb8c00' },
      { label: avg, from: 164, to: 174, color: '#43a047' },
      { label: good, from: 174, to: 184, color: '#1e88e5' },
      { label: veryGood, from: 184, to: 200, color: '#8e24aa' },
    ],
    CADENCE_DOMAIN,
  )
}

export const GCT_DOMAIN: [number, number] = [180, 340]
export function getGctZones(language: Language): RangeZone[] {
  const [low, belowAvg, avg, good, veryGood] = SCALE_LABELS[language]
  return shiftZones(
    [
      { label: veryGood, from: 180, to: 218, color: '#8e24aa' },
      { label: good, from: 218, to: 249, color: '#1e88e5' },
      { label: avg, from: 249, to: 278, color: '#43a047' },
      { label: belowAvg, from: 278, to: 308, color: '#fb8c00' },
      { label: low, from: 308, to: 340, color: '#e53935' },
    ],
    GCT_DOMAIN,
  )
}

export const VERTICAL_OSC_DOMAIN: [number, number] = [4, 15]
export function getVerticalOscZones(language: Language): RangeZone[] {
  const [low, belowAvg, avg, good, veryGood] = SCALE_LABELS[language]
  return shiftZones(
    [
      { label: veryGood, from: 4, to: 6.8, color: '#8e24aa' },
      { label: good, from: 6.8, to: 8.9, color: '#1e88e5' },
      { label: avg, from: 8.9, to: 10.9, color: '#43a047' },
      { label: belowAvg, from: 10.9, to: 13, color: '#fb8c00' },
      { label: low, from: 13, to: 15, color: '#e53935' },
    ],
    VERTICAL_OSC_DOMAIN,
  )
}

export const RUNNING_DYNAMICS_SOURCE = 'Garmin Running Dynamics'
export const RUNNING_DYNAMICS_SOURCE_URL =
  'https://www8.garmin.com/manuals/webhelp/GUID-676967A0-1B23-4384-9BC9-76F3D643F1C8/EN-US/GUID-62A09512-518A-424A-8491-FE2B80CD2091.html'

// Source: Shaffer F, Ginsberg JP. "An Overview of Heart Rate Variability Metrics and Norms." Front
// Public Health. 2017 — pooled healthy-adult RMSSD averages ~42ms (5-minute seated recordings), with
// wide individual spread (roughly 15-100ms across studies/ages). RMSSD is unusually individual (age,
// sex, fitness, and measurement conditions all shift it substantially), so unlike resting heart rate
// there is no single clinically agreed "normal" cutoff — these bands are a rough population-level
// orientation only. Trend relative to your own baseline matters far more than the absolute number.
export const HRV_DOMAIN: [number, number] = [10, 100]
const HRV_LABELS: Record<Language, [string, string, string]> = {
  de: ['Niedrig', 'Typisch', 'Hoch'],
  en: ['Low', 'Typical', 'High'],
}
export function getHrvZones(language: Language): RangeZone[] {
  const [low, typical, high] = HRV_LABELS[language]
  return [
    { label: low, from: 10, to: 25, color: '#e53935' },
    { label: typical, from: 25, to: 60, color: '#43a047' },
    { label: high, from: 60, to: 100, color: '#1e88e5' },
  ]
}
export const HRV_SOURCE = 'Shaffer & Ginsberg 2017, Frontiers in Public Health'
export const HRV_SOURCE_URL = 'https://www.ncbi.nlm.nih.gov/pmc/articles/PMC5624990/'
export function hrvAssessment(rmssdMs: number, language: Language): string {
  if (language === 'en') {
    return rmssdMs < 25
      ? 'On the lower end of commonly reported healthy-adult ranges. HRV is highly individual and affected by age, fitness, sleep, and stress — a single low night matters far less than a sustained downward trend against your own baseline.'
      : 'Within or above commonly reported healthy-adult ranges. As with any HRV reading, your own trend over time is more informative than comparing to population averages.'
  }
  return rmssdMs < 25
    ? 'Am unteren Ende häufig berichteter Werte für gesunde Erwachsene. HRV ist stark individuell und hängt von Alter, Fitness, Schlaf und Stress ab — eine einzelne niedrige Nacht zählt deutlich weniger als ein anhaltender Abwärtstrend gegenüber der eigenen Baseline.'
    : 'Im oder über dem häufig berichteten Bereich für gesunde Erwachsene. Wie bei jedem HRV-Wert ist der eigene Verlauf über die Zeit aussagekräftiger als der Vergleich mit Populationsdurchschnitten.'
}

// Source: American Lung Association — normal resting adult respiratory rate is 12-20 breaths/min;
// under 12 or over 25 breaths/min at rest warrants medical attention.
// https://www.lung.org/blog/respiratory-rate-vital-signs
export const RESPIRATORY_RATE_DOMAIN: [number, number] = [8, 28]
const RESPIRATORY_RATE_LABELS: Record<Language, [string, string, string]> = {
  de: ['Niedrig', 'Normal', 'Erhöht'],
  en: ['Low', 'Normal', 'Elevated'],
}
export function getRespiratoryRateZones(language: Language): RangeZone[] {
  const [low, normal, elevated] = RESPIRATORY_RATE_LABELS[language]
  return [
    { label: low, from: 8, to: 12, color: '#fb8c00' },
    { label: normal, from: 12, to: 20, color: '#43a047' },
    { label: elevated, from: 20, to: 28, color: '#e53935' },
  ]
}
export const RESPIRATORY_RATE_SOURCE = 'American Lung Association'
export const RESPIRATORY_RATE_SOURCE_URL = 'https://www.lung.org/blog/respiratory-rate-vital-signs'

// Source: Google Health / Fitbit Help Center — Daily Readiness Score is officially banded Low (1-29),
// Moderate (30-64), High (65-100). https://support.google.com/fitbit/answer/14236710
export const READINESS_DOMAIN: [number, number] = [0, 100]
const READINESS_LABELS: Record<Language, [string, string, string]> = {
  de: ['Niedrig', 'Moderat', 'Hoch'],
  en: ['Low', 'Moderate', 'High'],
}
export function getReadinessZones(language: Language): RangeZone[] {
  const [low, moderate, high] = READINESS_LABELS[language]
  return [
    { label: low, from: 0, to: 30, color: '#e53935' },
    { label: moderate, from: 30, to: 65, color: '#fb8c00' },
    { label: high, from: 65, to: 100, color: '#43a047' },
  ]
}
export const READINESS_SOURCE = 'Google Health Help Center'
export const READINESS_SOURCE_URL = 'https://support.google.com/fitbit/answer/14236710'

// Fitbit/Google don't publish official numeric cutoffs for the Stress Management Score (unlike
// Readiness) -- their own guidance is qualitative: a higher score means fewer physical signs of stress
// and better capacity to take on new demands. These bands are an even, unweighted three-way split of
// the 0-100 scale for visual orientation only, not an official threshold.
// https://support.google.com/fitbit/answer/14237928
export const STRESS_SCORE_DOMAIN: [number, number] = [0, 100]
const STRESS_SCORE_LABELS: Record<Language, [string, string, string]> = {
  de: ['Erhöhte Stresszeichen', 'Typisch', 'Wenig Stresszeichen'],
  en: ['Elevated stress signs', 'Typical', 'Few stress signs'],
}
export function getStressScoreZones(language: Language): RangeZone[] {
  const [elevated, typical, few] = STRESS_SCORE_LABELS[language]
  return [
    { label: elevated, from: 0, to: 33, color: '#e53935' },
    { label: typical, from: 33, to: 67, color: '#fb8c00' },
    { label: few, from: 67, to: 100, color: '#43a047' },
  ]
}
export const STRESS_SCORE_SOURCE = 'Google Health Help Center'
export const STRESS_SCORE_SOURCE_URL = 'https://support.google.com/fitbit/answer/14237928'

// Source: Mayo Clinic — normal pulse-oximeter SpO2 for a healthy person is 95-100%; below 90% is
// considered low and warrants medical attention (hypoxemia). Does not apply to people with chronic
// lung conditions, who should follow their own doctor's target range.
// https://www.mayoclinic.org/symptoms/hypoxemia/basics/definition/sym-20050930
export const SPO2_DOMAIN: [number, number] = [85, 100]
const SPO2_LABELS: Record<Language, [string, string, string]> = {
  de: ['Niedrig', 'Grenzwertig', 'Normal'],
  en: ['Low', 'Borderline', 'Normal'],
}
export function getSpo2Zones(language: Language): RangeZone[] {
  const [low, borderline, normal] = SPO2_LABELS[language]
  return [
    { label: low, from: 85, to: 90, color: '#e53935' },
    { label: borderline, from: 90, to: 95, color: '#fb8c00' },
    { label: normal, from: 95, to: 100, color: '#43a047' },
  ]
}
export const SPO2_SOURCE = 'Mayo Clinic'
export const SPO2_SOURCE_URL = 'https://www.mayoclinic.org/symptoms/hypoxemia/basics/definition/sym-20050930'

// Source: CDC physical activity guidelines — at least 150 min/week moderate-intensity OR 75 min/week
// vigorous-intensity aerobic activity (or an equivalent combination). Fitbit's Active Zone Minutes
// implements this directly: 1 minute in the Fat Burn/Cardio zones = 1 AZM, 1 minute in the Peak zone =
// 2 AZM, with a default weekly goal of 150 AZM (~22/day) matching the CDC minimum.
// https://www.cdc.gov/physical-activity-basics/guidelines/adults.html
export const AZM_WEEKLY_TARGET = 150
export const AZM_SOURCE = 'CDC'
export const AZM_SOURCE_URL = 'https://www.cdc.gov/physical-activity-basics/guidelines/adults.html'

// Fitbit compares nightly skin temperature to your own rolling baseline rather than an absolute
// reading, and explicitly notes population norms aren't meaningful here since baseline skin
// temperature varies enormously between individuals. Day-to-day swings within roughly ±0.5°C are
// common and usually unremarkable; larger or multi-night sustained deviations (particularly upward)
// are commonly associated with illness, alcohol, poor sleep, or (for women) menstrual-cycle phase.
// https://help.fitbit.com/articles/en_US/Help_article/2458.htm
export const TEMPERATURE_SOURCE = 'Fitbit Help Center'
export const TEMPERATURE_SOURCE_URL = 'https://help.fitbit.com/articles/en_US/Help_article/2458.htm'
