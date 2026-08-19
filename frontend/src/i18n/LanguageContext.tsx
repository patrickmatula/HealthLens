import { createContext, useContext, useMemo, useState, type PropsWithChildren } from 'react'
import de, { type TranslationKey } from './de'
import en from './en'

export type { TranslationKey }

export type Language = 'de' | 'en'

const DICTIONARIES: Record<Language, Record<TranslationKey, string>> = { de, en }
const STORAGE_KEY = 'ghl-language'

function readStored(): Language {
  return localStorage.getItem(STORAGE_KEY) === 'en' ? 'en' : 'de'
}

interface LanguageState {
  language: Language
  setLanguage: (l: Language) => void
  t: (key: TranslationKey, params?: Record<string, string | number>) => string
}

const LanguageCtx = createContext<LanguageState | null>(null)

export function LanguageProvider({ children }: PropsWithChildren) {
  const [language, setLanguageState] = useState<Language>(readStored)

  const value = useMemo<LanguageState>(() => {
    const dict = DICTIONARIES[language]
    const t: LanguageState['t'] = (key, params) => {
      let text = dict[key] ?? DICTIONARIES.de[key] ?? key
      if (params) {
        for (const [k, v] of Object.entries(params)) text = text.replaceAll(`{${k}}`, String(v))
      }
      return text
    }
    return {
      language,
      setLanguage: (l) => {
        setLanguageState(l)
        localStorage.setItem(STORAGE_KEY, l)
      },
      t,
    }
  }, [language])

  return <LanguageCtx.Provider value={value}>{children}</LanguageCtx.Provider>
}

export function useLanguage() {
  const ctx = useContext(LanguageCtx)
  if (!ctx) {
    throw new Error('useLanguage must be used within a LanguageProvider')
  }
  return ctx
}
