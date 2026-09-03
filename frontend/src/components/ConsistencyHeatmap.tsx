import { useMemo } from 'react'
import type { ConsistencyDayDto } from '../api/types'
import './ConsistencyHeatmap.css'

const MONTH_KEYS_DE = ['Jan', 'Feb', 'Mär', 'Apr', 'Mai', 'Jun', 'Jul', 'Aug', 'Sep', 'Okt', 'Nov', 'Dez']
const MONTH_KEYS_EN = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

type Cell = { date: string; steps: number | null; workoutCount: number; level: number } | null

/**
 * GitHub-contributions-style calendar: one column per ISO week (Monday-start, matching the week
 * bucketing already used on the Dashboard's steps chart), one row per weekday. Intensity is bucketed
 * relative to this dataset's own busiest day (quartiles) rather than a fixed step count, so it stays
 * meaningful whether the underlying data is a brisk walker or a 20k-steps/day runner.
 */
function buildWeeks(days: ConsistencyDayDto[]): Cell[][] {
  if (days.length === 0) return []

  const maxSteps = Math.max(1, ...days.map((d) => d.steps ?? 0))
  const cells: Cell[] = days.map((d) => {
    const steps = d.steps ?? 0
    const ratio = steps / maxSteps
    const level = steps <= 0 ? 0 : ratio > 0.75 ? 4 : ratio > 0.5 ? 3 : ratio > 0.25 ? 2 : 1
    return { date: d.date, steps: d.steps, workoutCount: d.workoutCount, level }
  })

  // Pad the front so the grid always starts on a Monday, and the back so it always ends on a Sunday --
  // otherwise the "7 rows per column" grid layout below would misalign every week.
  const firstDow = new Date(`${days[0].date}T00:00:00Z`).getUTCDay() || 7 // 1=Mon..7=Sun
  const leadingPad: Cell[] = Array(firstDow - 1).fill(null)
  const withLead = [...leadingPad, ...cells]
  const trailingPad: Cell[] = Array((7 - (withLead.length % 7)) % 7).fill(null)
  const padded = [...withLead, ...trailingPad]

  const weeks: Cell[][] = []
  for (let i = 0; i < padded.length; i += 7) {
    weeks.push(padded.slice(i, i + 7))
  }
  return weeks
}

export function ConsistencyHeatmap({ days, language, stepsLabel }: { days: ConsistencyDayDto[]; language: string; stepsLabel: string }) {
  const weeks = useMemo(() => buildWeeks(days), [days])
  const monthNames = language === 'en' ? MONTH_KEYS_EN : MONTH_KEYS_DE

  if (weeks.length === 0) {
    return null
  }

  // A month label is shown above the first week-column in which that month's 1st (or the data's own
  // start) falls, so labels don't repeat every column. Threaded through a reduce accumulator rather than
  // a mutated outer `let` so this stays a pure per-render computation.
  const monthLabels = weeks.reduce<{ labels: (string | null)[]; lastMonth: number }>(
    (acc, week) => {
      const firstReal = week.find((c) => c != null)
      if (!firstReal) {
        acc.labels.push(null)
        return acc
      }
      const month = new Date(`${firstReal.date}T00:00:00Z`).getUTCMonth()
      if (month === acc.lastMonth) {
        acc.labels.push(null)
      } else {
        acc.labels.push(monthNames[month])
        acc.lastMonth = month
      }
      return acc
    },
    { labels: [], lastMonth: -1 },
  ).labels

  return (
    <div className="ghl-heatmap">
      <div className="ghl-heatmap__scroll">
        <div className="ghl-heatmap__months">
          {monthLabels.map((label, i) => (
            <span key={i} className="ghl-heatmap__month">
              {label ?? ''}
            </span>
          ))}
        </div>
        <div className="ghl-heatmap__grid">
          {weeks.map((week, wi) =>
            week.map((cell, di) => {
              if (!cell) {
                return <div key={`${wi}-${di}`} className="ghl-heatmap__cell ghl-heatmap__cell--empty" />
              }
              const title = `${cell.date}: ${cell.steps != null ? `${cell.steps.toLocaleString(language === 'en' ? 'en-US' : 'de-AT')} ${stepsLabel}` : '–'}${cell.workoutCount > 0 ? ` · ${cell.workoutCount} 🏃` : ''}`
              return <div key={`${wi}-${di}`} className={`ghl-heatmap__cell ghl-heatmap__cell--level-${cell.level}`} title={title} />
            }),
          )}
        </div>
      </div>
    </div>
  )
}
