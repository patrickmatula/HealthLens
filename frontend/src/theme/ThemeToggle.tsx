import { Icon, type IconName } from '../components/Icon'
import { useTheme, type ThemeMode } from './ThemeContext'
import './ThemeToggle.css'

const NEXT: Record<ThemeMode, ThemeMode> = { system: 'light', light: 'dark', dark: 'system' }
const ICON: Record<ThemeMode, IconName> = { system: 'auto', light: 'sun', dark: 'moon' }
const LABEL: Record<ThemeMode, string> = {
  system: 'Design: System — klicken für Hell',
  light: 'Design: Hell — klicken für Dunkel',
  dark: 'Design: Dunkel — klicken für System',
}

export function ThemeToggle() {
  const { mode, setMode } = useTheme()

  return (
    <button
      type="button"
      className="ghl-theme-toggle"
      title={LABEL[mode]}
      aria-label={LABEL[mode]}
      onClick={() => setMode(NEXT[mode])}
    >
      <Icon name={ICON[mode]} size={20} />
    </button>
  )
}
