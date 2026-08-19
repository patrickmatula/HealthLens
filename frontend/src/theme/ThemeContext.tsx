import { createContext, useContext, useEffect, useState, type PropsWithChildren } from 'react'
import { COLOR_THEMES, type ColorThemeKey } from './themes.generated'

export type ThemeMode = 'light' | 'dark' | 'system'

const MODE_STORAGE_KEY = 'ghl-theme'
const COLOR_STORAGE_KEY = 'ghl-color-theme'
const DEFAULT_COLOR_THEME: ColorThemeKey = COLOR_THEMES[0].key

const ThemeCtx = createContext<{
  mode: ThemeMode
  setMode: (m: ThemeMode) => void
  colorTheme: ColorThemeKey
  setColorTheme: (t: ColorThemeKey) => void
} | null>(null)

function readStoredMode(): ThemeMode {
  const stored = localStorage.getItem(MODE_STORAGE_KEY)
  return stored === 'light' || stored === 'dark' ? stored : 'system'
}

function readStoredColorTheme(): ColorThemeKey {
  const stored = localStorage.getItem(COLOR_STORAGE_KEY)
  return (COLOR_THEMES.find((t) => t.key === stored)?.key ?? DEFAULT_COLOR_THEME) as ColorThemeKey
}

export function ThemeProvider({ children }: PropsWithChildren) {
  const [mode, setModeState] = useState<ThemeMode>(readStoredMode)
  const [colorTheme, setColorThemeState] = useState<ColorThemeKey>(readStoredColorTheme)

  useEffect(() => {
    const root = document.documentElement
    if (mode === 'system') {
      root.removeAttribute('data-theme')
    } else {
      root.setAttribute('data-theme', mode)
    }
  }, [mode])

  useEffect(() => {
    document.documentElement.setAttribute('data-color-theme', colorTheme)
  }, [colorTheme])

  function setMode(m: ThemeMode) {
    setModeState(m)
    if (m === 'system') {
      localStorage.removeItem(MODE_STORAGE_KEY)
    } else {
      localStorage.setItem(MODE_STORAGE_KEY, m)
    }
  }

  function setColorTheme(t: ColorThemeKey) {
    setColorThemeState(t)
    localStorage.setItem(COLOR_STORAGE_KEY, t)
  }

  return <ThemeCtx.Provider value={{ mode, setMode, colorTheme, setColorTheme }}>{children}</ThemeCtx.Provider>
}

export function useTheme() {
  const ctx = useContext(ThemeCtx)
  if (!ctx) {
    throw new Error('useTheme must be used within a ThemeProvider')
  }
  return ctx
}
