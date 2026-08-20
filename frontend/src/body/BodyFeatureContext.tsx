import { createContext, useContext, useState, type PropsWithChildren } from 'react'
import { BODY_MEASUREMENT_TYPES, type BodyMeasurementTypeKey } from '../api/types'

const ENABLED_KEY = 'ghl-body-enabled'
const TRACKED_KEY = 'ghl-body-tracked-types'

// A sensible default subset rather than all nine — the entry form stays approachable, and everything
// else is one tap away in settings if someone wants the full set.
const DEFAULT_TRACKED: BodyMeasurementTypeKey[] = ['WeightKg', 'BodyFatPercent', 'WaistCm']

interface BodyFeatureState {
  enabled: boolean
  setEnabled: (v: boolean) => void
  trackedTypes: Set<BodyMeasurementTypeKey>
  setTypeTracked: (type: BodyMeasurementTypeKey, tracked: boolean) => void
}

const BodyFeatureCtx = createContext<BodyFeatureState | null>(null)

function readEnabled(): boolean {
  return localStorage.getItem(ENABLED_KEY) === '1'
}

function readTracked(): Set<BodyMeasurementTypeKey> {
  const raw = localStorage.getItem(TRACKED_KEY)
  if (!raw) return new Set(DEFAULT_TRACKED)
  try {
    const parsed = JSON.parse(raw) as string[]
    const valid = parsed.filter((t): t is BodyMeasurementTypeKey => (BODY_MEASUREMENT_TYPES as readonly string[]).includes(t))
    return new Set(valid)
  } catch {
    return new Set(DEFAULT_TRACKED)
  }
}

export function BodyFeatureProvider({ children }: PropsWithChildren) {
  const [enabled, setEnabledState] = useState<boolean>(readEnabled)
  const [trackedTypes, setTrackedTypes] = useState<Set<BodyMeasurementTypeKey>>(readTracked)

  function setEnabled(v: boolean) {
    setEnabledState(v)
    localStorage.setItem(ENABLED_KEY, v ? '1' : '0')
  }

  function setTypeTracked(type: BodyMeasurementTypeKey, tracked: boolean) {
    setTrackedTypes((prev) => {
      const next = new Set(prev)
      if (tracked) next.add(type)
      else next.delete(type)
      localStorage.setItem(TRACKED_KEY, JSON.stringify([...next]))
      return next
    })
  }

  return (
    <BodyFeatureCtx.Provider value={{ enabled, setEnabled, trackedTypes, setTypeTracked }}>{children}</BodyFeatureCtx.Provider>
  )
}

export function useBodyFeature() {
  const ctx = useContext(BodyFeatureCtx)
  if (!ctx) {
    throw new Error('useBodyFeature must be used within a BodyFeatureProvider')
  }
  return ctx
}
