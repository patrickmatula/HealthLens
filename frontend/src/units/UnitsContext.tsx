import { createContext, useContext, useEffect, useState, type PropsWithChildren } from 'react'
import { setFormatUnitSystem, type UnitSystem } from '../utils/format'

const STORAGE_KEY = 'ghl-units'

const UnitsCtx = createContext<{ unit: UnitSystem; setUnit: (u: UnitSystem) => void } | null>(null)

function readStored(): UnitSystem {
  return localStorage.getItem(STORAGE_KEY) === 'imperial' ? 'imperial' : 'metric'
}

export function UnitsProvider({ children }: PropsWithChildren) {
  const [unit, setUnitState] = useState<UnitSystem>(readStored)

  useEffect(() => {
    setFormatUnitSystem(unit)
  }, [unit])

  function setUnit(u: UnitSystem) {
    setUnitState(u)
    localStorage.setItem(STORAGE_KEY, u)
  }

  return <UnitsCtx.Provider value={{ unit, setUnit }}>{children}</UnitsCtx.Provider>
}

export function useUnits() {
  const ctx = useContext(UnitsCtx)
  if (!ctx) {
    throw new Error('useUnits must be used within a UnitsProvider')
  }
  return ctx
}
