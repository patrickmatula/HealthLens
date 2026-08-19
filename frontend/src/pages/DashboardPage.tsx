import { useEffect, useMemo, useState } from 'react'
import { Area, AreaChart, Bar, BarChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { api } from '../api/client'
import type { DailyActivityPointDto, DashboardOverviewDto, TimeframePreset } from '../api/types'
import { KpiTile } from '../components/KpiTile'
import { SegmentedButton } from '../components/SegmentedButton'
import { Surface } from '../components/Surface'
import { TopAppBar } from '../components/TopAppBar'
import { Icon } from '../components/Icon'
import './DashboardPage.css'

const PRESETS: { value: TimeframePreset; label: string }[] = [
  { value: '7d', label: '7 Tage' },
  { value: '30d', label: '30 Tage' },
  { value: '1y', label: '1 Jahr' },
  { value: 'all', label: 'Alle' },
]

const numberFmt = new Intl.NumberFormat('de-AT', { maximumFractionDigits: 0 })

type ChartGranularity = 'daily' | 'week' | 'month'

function bucketKey(dateStr: string, mode: 'week' | 'month'): string {
  if (mode === 'month') return dateStr.slice(0, 7)
  const d = new Date(`${dateStr}T00:00:00Z`)
  const isoDay = d.getUTCDay() || 7
  d.setUTCDate(d.getUTCDate() - isoDay + 1)
  return d.toISOString().slice(0, 10)
}

function aggregateDays(days: DailyActivityPointDto[], mode: 'week' | 'month') {
  const buckets = new Map<string, { date: string; steps: number }>()
  for (const d of days) {
    const key = bucketKey(d.date, mode)
    const entry = buckets.get(key) ?? { date: key, steps: 0 }
    entry.steps += d.steps ?? 0
    buckets.set(key, entry)
  }
  return [...buckets.values()].sort((a, b) => a.date.localeCompare(b.date))
}

function formatBucketLabel(date: string, mode: ChartGranularity): string {
  if (mode === 'month') {
    const [, m] = date.split('-')
    return m
  }
  return date.slice(5)
}

export function DashboardPage() {
  const [preset, setPreset] = useState<TimeframePreset>('30d')
  const [data, setData] = useState<DashboardOverviewDto | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    setLoading(true)
    api
      .dashboardOverview({ preset })
      .then(setData)
      .finally(() => setLoading(false))
  }, [preset])

  const hasData = data && data.daysWithData > 0

  const chartGranularity: ChartGranularity = preset === '1y' ? 'week' : preset === 'all' ? 'month' : 'daily'
  const chartData = useMemo(() => {
    if (!data) return []
    return chartGranularity === 'daily' ? data.days.map((d) => ({ date: d.date, steps: d.steps ?? 0 })) : aggregateDays(data.days, chartGranularity)
  }, [data, chartGranularity])

  return (
    <div>
      <TopAppBar title="Übersicht">
        <div className="ghl-dashboard__timeframe">
          <SegmentedButton options={PRESETS} value={preset} onChange={setPreset} />
        </div>
      </TopAppBar>

      <div className="ghl-page-content">
        {!loading && !hasData && (
          <Surface tone="low">
            <p>Für diesen Zeitraum sind noch keine Aktivitätsdaten vorhanden.</p>
          </Surface>
        )}

        {hasData && data && (
          <>
            {data.insights.length > 0 && (
              <div className="ghl-insights">
                {data.insights.map((insight, i) => (
                  <Surface key={i} className="ghl-insight-card">
                    <Icon name="trophy" size={18} />
                    <span>{insight}</span>
                  </Surface>
                ))}
              </div>
            )}

            <div className="ghl-kpi-row">
              <KpiTile label="Schritte gesamt" value={numberFmt.format(data.totalSteps)} icon={<Icon name="workouts" size={20} />} />
              <KpiTile label="Ø Schritte/Tag" value={numberFmt.format(data.avgStepsPerDay)} icon={<Icon name="dashboard" size={20} />} />
              <KpiTile
                label="Distanz gesamt"
                value={numberFmt.format(data.totalDistanceMeters / 1000)}
                unit="km"
                icon={<Icon name="route" size={20} />}
              />
              <KpiTile label="Kalorien gesamt" value={numberFmt.format(data.totalCalories)} unit="kcal" icon={<Icon name="heart" size={20} />} />
              <KpiTile
                label="Ø aktive Minuten/Tag"
                value={numberFmt.format(data.avgActiveMinutesPerDay)}
                unit="min"
                icon={<Icon name="recovery" size={20} />}
              />
              <KpiTile label="Workouts" value={data.workoutsInRange.toString()} icon={<Icon name="workouts" size={20} />} />
              {data.avgSleepScore != null && (
                <KpiTile label="Ø Schlaf-Score" value={Math.round(data.avgSleepScore).toString()} icon={<Icon name="sleep" size={20} />} />
              )}
              {data.avgRestingHeartRate != null && (
                <KpiTile label="Ø Ruhepuls" value={Math.round(data.avgRestingHeartRate).toString()} unit="bpm" icon={<Icon name="heart" size={20} />} />
              )}
            </div>

            <Surface tone="low" className="ghl-chart-card">
              <h2 className="ghl-chart-card__title">
                Schritte im Zeitverlauf
                {chartGranularity !== 'daily' && (
                  <span className="ghl-chart-card__subtitle"> — {chartGranularity === 'week' ? 'pro Woche' : 'pro Monat'}</span>
                )}
              </h2>
              <ResponsiveContainer width="100%" height={280}>
                {chartGranularity === 'daily' ? (
                  <AreaChart data={chartData}>
                    <defs>
                      <linearGradient id="stepsGradient" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="0%" stopColor="var(--md-sys-color-primary)" stopOpacity={0.35} />
                        <stop offset="100%" stopColor="var(--md-sys-color-primary)" stopOpacity={0} />
                      </linearGradient>
                    </defs>
                    <XAxis dataKey="date" tick={{ fontSize: 11 }} tickFormatter={(d) => formatBucketLabel(d, chartGranularity)} stroke="var(--md-sys-color-outline)" />
                    <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={40} />
                    <Tooltip contentStyle={{ background: 'var(--md-sys-color-surface-container-high)', border: 'none', borderRadius: 8 }} />
                    <Area type="monotone" dataKey="steps" stroke="var(--md-sys-color-primary)" fill="url(#stepsGradient)" strokeWidth={2} />
                  </AreaChart>
                ) : (
                  <BarChart data={chartData}>
                    <XAxis dataKey="date" tick={{ fontSize: 11 }} tickFormatter={(d) => formatBucketLabel(d, chartGranularity)} stroke="var(--md-sys-color-outline)" />
                    <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={44} />
                    <Tooltip
                      contentStyle={{ background: 'var(--md-sys-color-surface-container-high)', border: 'none', borderRadius: 8 }}
                      formatter={(v) => numberFmt.format(Number(v))}
                    />
                    <Bar dataKey="steps" fill="var(--md-sys-color-primary)" radius={[4, 4, 0, 0]} />
                  </BarChart>
                )}
              </ResponsiveContainer>
            </Surface>
          </>
        )}
      </div>
    </div>
  )
}
