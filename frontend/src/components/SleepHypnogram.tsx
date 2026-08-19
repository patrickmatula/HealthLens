import type { SleepStageDto } from '../api/types'
import './SleepHypnogram.css'

const STAGE_LEVEL: Record<string, number> = { AWAKE: 3, RESTLESS: 3, REM: 2, LIGHT: 1, ASLEEP: 1, DEEP: 0 }
const STAGE_COLOR: Record<string, string> = {
  AWAKE: '#f4511e',
  RESTLESS: '#f4511e',
  REM: '#8e24aa',
  LIGHT: '#42a5f5',
  ASLEEP: '#42a5f5',
  DEEP: '#1a237e',
}
const STAGE_LABEL: Record<string, string> = {
  AWAKE: 'Wach',
  RESTLESS: 'Unruhig',
  REM: 'REM',
  LIGHT: 'Leicht',
  ASLEEP: 'Schlaf',
  DEEP: 'Tief',
}

export function SleepHypnogram({ stages, startUtc, endUtc }: { stages: SleepStageDto[]; startUtc: string; endUtc: string }) {
  const start = new Date(startUtc + 'Z').getTime()
  const end = new Date(endUtc + 'Z').getTime()
  const totalMs = Math.max(end - start, 1)

  const width = 1000
  const height = 160
  const levelHeight = height / 4

  const types = [...new Set(stages.map((s) => s.stageType))]

  return (
    <div>
      <svg viewBox={`0 0 ${width} ${height}`} width="100%" height={height} preserveAspectRatio="none">
        {stages.map((stage, i) => {
          const sStart = new Date(stage.startUtc + 'Z').getTime()
          const sEnd = new Date(stage.endUtc + 'Z').getTime()
          const x = ((sStart - start) / totalMs) * width
          const w = Math.max(((sEnd - sStart) / totalMs) * width, 1)
          const level = STAGE_LEVEL[stage.stageType] ?? 3
          const y = level * levelHeight
          return <rect key={i} x={x} y={y} width={w} height={levelHeight} fill={STAGE_COLOR[stage.stageType] ?? '#9e9e9e'} rx={2} />
        })}
      </svg>
      <div className="ghl-hypnogram-legend">
        {types.map((t) => (
          <span key={t} className="ghl-hypnogram-legend__item">
            <span className="ghl-hypnogram-legend__swatch" style={{ background: STAGE_COLOR[t] ?? '#9e9e9e' }} />
            {STAGE_LABEL[t] ?? t}
          </span>
        ))}
      </div>
    </div>
  )
}
