import type { RangeZone } from '../components/ReferenceRangeGauge'

type Language = 'de' | 'en'
export type BodySex = 'male' | 'female'

// ---------------------------------------------------------------------------
// BMI — World Health Organization classification (Untergewicht/Normal/
// Übergewicht/Adipositas). BMI itself is unitless (kg/m²), but the gauge
// charts the person's actual tracked metric — weight — so the four BMI cutoffs
// are converted to weight-in-kg for their specific height and rendered exactly
// like the cadence/GCT gauges (a weight domain + weight-valued zones).
// Source: WHO — https://www.who.int/europe/news-room/fact-sheets/item/a-healthy-lifestyle---who-recommendations
// ---------------------------------------------------------------------------
export const BMI_SOURCE = 'World Health Organization'
export const BMI_SOURCE_URL = 'https://www.who.int/europe/news-room/fact-sheets/item/a-healthy-lifestyle---who-recommendations'

const BMI_CUTOFFS = [18.5, 25, 30] as const

function weightAtBmi(bmi: number, heightCm: number): number {
  const heightM = heightCm / 100
  return bmi * heightM * heightM
}

export function getBmiDomain(heightCm: number): [number, number] {
  return [Math.max(30, weightAtBmi(15, heightCm)), weightAtBmi(38, heightCm)]
}

const BMI_LABELS: Record<Language, [string, string, string, string]> = {
  de: ['Untergewicht', 'Normalgewicht', 'Übergewicht', 'Adipositas'],
  en: ['Underweight', 'Normal weight', 'Overweight', 'Obesity'],
}

export function getBmiZones(heightCm: number, language: Language): RangeZone[] {
  const [under, normal, over, obese] = BMI_LABELS[language]
  const [min] = getBmiDomain(heightCm)
  const [c1, c2, c3] = BMI_CUTOFFS.map((bmi) => weightAtBmi(bmi, heightCm))
  const [, max] = getBmiDomain(heightCm)
  return [
    { label: under, from: min, to: c1, color: '#fb8c00' },
    { label: normal, from: c1, to: c2, color: '#43a047' },
    { label: over, from: c2, to: c3, color: '#fb8c00' },
    { label: obese, from: c3, to: max, color: '#e53935' },
  ]
}

export function computeBmi(weightKg: number, heightCm: number): number {
  const heightM = heightCm / 100
  return weightKg / (heightM * heightM)
}

export function bmiAssessment(weightKg: number, heightCm: number, language: Language): string {
  const bmi = computeBmi(weightKg, heightCm)
  if (language === 'en') {
    if (bmi < 18.5) return `BMI ${bmi.toFixed(1)} — underweight per WHO classification.`
    if (bmi < 25) return `BMI ${bmi.toFixed(1)} — normal weight per WHO classification.`
    if (bmi < 30) return `BMI ${bmi.toFixed(1)} — overweight per WHO classification.`
    return `BMI ${bmi.toFixed(1)} — obesity per WHO classification.`
  }
  if (bmi < 18.5) return `BMI ${bmi.toFixed(1)} — Untergewicht laut WHO-Klassifikation.`
  if (bmi < 25) return `BMI ${bmi.toFixed(1)} — Normalgewicht laut WHO-Klassifikation.`
  if (bmi < 30) return `BMI ${bmi.toFixed(1)} — Übergewicht laut WHO-Klassifikation.`
  return `BMI ${bmi.toFixed(1)} — Adipositas laut WHO-Klassifikation.`
}

// ---------------------------------------------------------------------------
// Waist circumference — WHO risk-of-metabolic-disease thresholds, sex-specific.
// Source: WHO — https://www.who.int/news-room/fact-sheets/detail/obesity-and-overweight
// ---------------------------------------------------------------------------
export const WAIST_SOURCE = 'World Health Organization'
export const WAIST_SOURCE_URL = 'https://www.who.int/news-room/fact-sheets/detail/obesity-and-overweight'

const WAIST_THRESHOLDS: Record<BodySex, [number, number]> = {
  male: [94, 102],
  female: [80, 88],
}

const WAIST_LABELS: Record<Language, [string, string, string]> = {
  de: ['Niedrig', 'Erhöht', 'Hoch'],
  en: ['Low', 'Elevated', 'High'],
}

export function getWaistDomain(sex: BodySex): [number, number] {
  return sex === 'male' ? [60, 130] : [55, 120]
}

export function getWaistZones(sex: BodySex, language: Language): RangeZone[] {
  const [low, elevated, high] = WAIST_LABELS[language]
  const [min, max] = getWaistDomain(sex)
  const [c1, c2] = WAIST_THRESHOLDS[sex]
  return [
    { label: low, from: min, to: c1, color: '#43a047' },
    { label: elevated, from: c1, to: c2, color: '#fb8c00' },
    { label: high, from: c2, to: max, color: '#e53935' },
  ]
}

// ---------------------------------------------------------------------------
// Waist-to-hip ratio — same WHO risk data as waist circumference, expressed as
// a ratio; the "increased risk" cutoff is WHO's published one (0.90 men /
// 0.85 women), with a borderline band added below it for a more legible gauge.
// ---------------------------------------------------------------------------
export const WAIST_HIP_SOURCE = WAIST_SOURCE
export const WAIST_HIP_SOURCE_URL = WAIST_SOURCE_URL
export const WAIST_HIP_DOMAIN: [number, number] = [0.6, 1.1]

const WAIST_HIP_THRESHOLDS: Record<BodySex, [number, number]> = {
  male: [0.85, 0.9],
  female: [0.75, 0.85],
}

export function getWaistHipZones(sex: BodySex, language: Language): RangeZone[] {
  const [low, elevated, high] = WAIST_LABELS[language]
  const [min, max] = WAIST_HIP_DOMAIN
  const [c1, c2] = WAIST_HIP_THRESHOLDS[sex]
  return [
    { label: low, from: min, to: c1, color: '#43a047' },
    { label: elevated, from: c1, to: c2, color: '#fb8c00' },
    { label: high, from: c2, to: max, color: '#e53935' },
  ]
}

// ---------------------------------------------------------------------------
// Body fat percentage — American Council on Exercise categories, sex-specific.
// Source: ACE — https://www.acefitness.org/resources/everyone/blog/112/what-are-the-guidelines-for-percentage-of-body-fat-loss/
// ---------------------------------------------------------------------------
export const BODY_FAT_SOURCE = 'American Council on Exercise'
export const BODY_FAT_SOURCE_URL = 'https://www.acefitness.org/resources/everyone/blog/112/what-are-the-guidelines-for-percentage-of-body-fat-loss/'
export const BODY_FAT_DOMAIN: [number, number] = [2, 40]

const BODY_FAT_THRESHOLDS: Record<BodySex, [number, number, number, number]> = {
  male: [5, 13, 17, 24],
  female: [13, 20, 24, 31],
}

const BODY_FAT_LABELS: Record<Language, [string, string, string, string, string]> = {
  de: ['Essentiell', 'Athletisch', 'Fitness', 'Durchschnittlich', 'Erhöht'],
  en: ['Essential fat', 'Athletes', 'Fitness', 'Average', 'Obese'],
}

export function getBodyFatZones(sex: BodySex, language: Language): RangeZone[] {
  const [essential, athletic, fitness, average, obese] = BODY_FAT_LABELS[language]
  const [min, max] = BODY_FAT_DOMAIN
  const [c1, c2, c3, c4] = BODY_FAT_THRESHOLDS[sex]
  return [
    { label: essential, from: min, to: c1, color: '#8e24aa' },
    { label: athletic, from: c1, to: c2, color: '#1e88e5' },
    { label: fitness, from: c2, to: c3, color: '#43a047' },
    { label: average, from: c3, to: c4, color: '#fb8c00' },
    { label: obese, from: c4, to: max, color: '#e53935' },
  ]
}
