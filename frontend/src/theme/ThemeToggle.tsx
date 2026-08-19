import { Icon, type IconName } from '../components/Icon'
import { useLanguage, type TranslationKey } from '../i18n/LanguageContext'
import { useTheme, type ThemeMode } from './ThemeContext'
import './ThemeToggle.css'

const NEXT: Record<ThemeMode, ThemeMode> = { system: 'light', light: 'dark', dark: 'system' }
const ICON: Record<ThemeMode, IconName> = { system: 'auto', light: 'sun', dark: 'moon' }
const LABEL_KEY: Record<ThemeMode, TranslationKey> = {
  system: 'theme.system',
  light: 'theme.light',
  dark: 'theme.dark',
}

export function ThemeToggle() {
  const { mode, setMode } = useTheme()
  const { t } = useLanguage()
  const label = t(LABEL_KEY[mode])

  return (
    <button
      type="button"
      className="ghl-theme-toggle"
      title={label}
      aria-label={label}
      onClick={() => setMode(NEXT[mode])}
    >
      <Icon name={ICON[mode]} size={20} />
    </button>
  )
}
