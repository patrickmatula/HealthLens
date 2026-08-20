import { createContext, useContext, useState, type PropsWithChildren } from 'react'

const STORAGE_KEY = 'ghl-shoes-enabled'

interface ShoesFeatureState {
  enabled: boolean
  setEnabled: (v: boolean) => void
}

const ShoesFeatureCtx = createContext<ShoesFeatureState | null>(null)

function readStored(): boolean {
  return localStorage.getItem(STORAGE_KEY) === '1'
}

export function ShoesFeatureProvider({ children }: PropsWithChildren) {
  const [enabled, setEnabledState] = useState<boolean>(readStored)

  function setEnabled(v: boolean) {
    setEnabledState(v)
    localStorage.setItem(STORAGE_KEY, v ? '1' : '0')
  }

  return <ShoesFeatureCtx.Provider value={{ enabled, setEnabled }}>{children}</ShoesFeatureCtx.Provider>
}

export function useShoesFeature() {
  const ctx = useContext(ShoesFeatureCtx)
  if (!ctx) {
    throw new Error('useShoesFeature must be used within a ShoesFeatureProvider')
  }
  return ctx
}
