import { createContext, useContext, useState, type PropsWithChildren } from 'react'

const STORAGE_KEY = 'ghl-weather-enabled'

interface WeatherFeatureState {
  enabled: boolean
  setEnabled: (v: boolean) => void
}

const WeatherFeatureCtx = createContext<WeatherFeatureState | null>(null)

function readStored(): boolean {
  return localStorage.getItem(STORAGE_KEY) === '1'
}

// Off by default: enabling this sends a GPS-tracked workout's start coordinate and date to Open-Meteo's
// third-party weather API (see backend/HealthLens.Api/Services/OpenMeteoWeatherService.cs) -- the one
// exception to this app's "sends nothing anywhere except what you explicitly connect" stance, so it
// requires an explicit opt-in rather than being on by default like most other optional features.
export function WeatherFeatureProvider({ children }: PropsWithChildren) {
  const [enabled, setEnabledState] = useState<boolean>(readStored)

  function setEnabled(v: boolean) {
    setEnabledState(v)
    localStorage.setItem(STORAGE_KEY, v ? '1' : '0')
  }

  return <WeatherFeatureCtx.Provider value={{ enabled, setEnabled }}>{children}</WeatherFeatureCtx.Provider>
}

export function useWeatherFeature() {
  const ctx = useContext(WeatherFeatureCtx)
  if (!ctx) {
    throw new Error('useWeatherFeature must be used within a WeatherFeatureProvider')
  }
  return ctx
}
