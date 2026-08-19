import type { ReactNode } from 'react'
import { Surface } from './Surface'
import './KpiTile.css'

export function KpiTile({ label, value, unit, icon }: { label: string; value: string; unit?: string; icon?: ReactNode }) {
  return (
    <Surface className="ghl-kpi" tone="low">
      <div className="ghl-kpi__header">
        {icon && <span className="ghl-kpi__icon">{icon}</span>}
        <span className="ghl-kpi__label">{label}</span>
      </div>
      <div className="ghl-kpi__value">
        {value}
        {unit && <span className="ghl-kpi__unit"> {unit}</span>}
      </div>
    </Surface>
  )
}
