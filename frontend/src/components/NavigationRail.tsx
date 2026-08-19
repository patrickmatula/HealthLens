import { NavLink } from 'react-router-dom'
import { Icon, type IconName } from './Icon'
import './NavigationRail.css'

const ITEMS: { to: string; icon: IconName; label: string }[] = [
  { to: '/', icon: 'dashboard', label: 'Übersicht' },
  { to: '/workouts', icon: 'workouts', label: 'Workouts' },
  { to: '/sleep', icon: 'sleep', label: 'Schlaf' },
  { to: '/heart', icon: 'heart', label: 'Herz' },
  { to: '/recovery', icon: 'recovery', label: 'Erholung' },
  { to: '/more', icon: 'more', label: 'Mehr' },
]

export function NavigationRail() {
  return (
    <nav className="ghl-nav-rail" aria-label="Hauptnavigation">
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
          <span className="ghl-nav-rail__label">{item.label}</span>
        </NavLink>
      ))}
    </nav>
  )
}
