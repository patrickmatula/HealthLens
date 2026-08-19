import { NavLink } from 'react-router-dom'
import { useLanguage, type TranslationKey } from '../i18n/LanguageContext'
import { Icon, type IconName } from './Icon'
import { Logo } from './Logo'
import './NavigationRail.css'

const ITEMS: { to: string; icon: IconName; labelKey: TranslationKey }[] = [
  { to: '/', icon: 'dashboard', labelKey: 'nav.dashboard' },
  { to: '/workouts', icon: 'workouts', labelKey: 'nav.workouts' },
  { to: '/sleep', icon: 'sleep', labelKey: 'nav.sleep' },
  { to: '/heart', icon: 'heart', labelKey: 'nav.heart' },
  { to: '/recovery', icon: 'recovery', labelKey: 'nav.recovery' },
  { to: '/more', icon: 'more', labelKey: 'nav.more' },
]

export function NavigationRail() {
  const { t } = useLanguage()
  return (
    <nav className="ghl-nav-rail" aria-label={t('nav.mainLabel')}>
      <Logo size={28} className="ghl-nav-rail__logo" />
      {ITEMS.map((item) => (
        <NavLink
          key={item.to}
          to={item.to}
          end={item.to === '/'}
          className={({ isActive }) => `ghl-nav-rail__item ${isActive ? 'ghl-nav-rail__item--active' : ''}`}
        >
          <span className="ghl-nav-rail__indicator">
            <Icon name={item.icon} />
          </span>
          <span className="ghl-nav-rail__label">{t(item.labelKey)}</span>
        </NavLink>
      ))}
    </nav>
  )
}
