import type { PropsWithChildren, ReactNode } from 'react'
import './TopAppBar.css'

export function TopAppBar({ title, actions, children }: PropsWithChildren<{ title: string; actions?: ReactNode }>) {
  return (
    <header className="ghl-top-app-bar">
      <div className="ghl-top-app-bar__inner">
        <div className="ghl-top-app-bar__row">
          <h1 className="ghl-top-app-bar__title">{title}</h1>
          {actions && <div className="ghl-top-app-bar__actions">{actions}</div>}
        </div>
        {children}
      </div>
    </header>
  )
}
