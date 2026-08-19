import './ReferenceRangeGauge.css'

export interface RangeZone {
  label: string
  from: number
  to: number
  color: string
}

export function ReferenceRangeGauge({
  value,
  domain,
  zones,
  unit,
  valueLabel,
}: {
  value: number
  domain: [number, number]
  zones: RangeZone[]
  unit: string
  valueLabel?: string
}) {
  const [min, max] = domain
  const span = max - min
  const clamped = Math.min(Math.max(value, min), max)
  const markerPercent = ((clamped - min) / span) * 100
  const activeZone = zones.find((z) => value >= z.from && value < z.to) ?? (value >= zones[zones.length - 1]?.to ? zones[zones.length - 1] : zones[0])

  return (
    <div className="ghl-range-gauge">
      <div className="ghl-range-gauge__value">
        {valueLabel ?? value.toFixed(1)} <span className="ghl-range-gauge__unit">{unit}</span>
        {activeZone && <span className="ghl-range-gauge__zone-label">{activeZone.label}</span>}
      </div>
      <div className="ghl-range-gauge__track">
        {zones.map((z) => (
          <div
            key={z.label}
            className="ghl-range-gauge__segment"
            style={{ flexGrow: Math.max(z.to, min) - Math.max(z.from, min), background: z.color }}
            title={`${z.label}: ${z.from}–${z.to} ${unit}`}
          />
        ))}
        <div className="ghl-range-gauge__marker" style={{ left: `${markerPercent}%` }} />
      </div>
      <div className="ghl-range-gauge__labels">
        {zones.map((z) => (
          <span key={z.label} className="ghl-range-gauge__legend-item">
            <span className="ghl-range-gauge__legend-swatch" style={{ background: z.color }} />
            {z.label}
          </span>
        ))}
      </div>
    </div>
  )
}
