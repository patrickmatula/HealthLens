import { useEffect, useState } from 'react'
import { Area, AreaChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { api } from '../api/client'
import type { DashboardOverviewDto, TimeframePreset } from '../api/types'
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
              <h2 className="ghl-chart-card__title">Schritte im Zeitverlauf</h2>
              <ResponsiveContainer width="100%" height={280}>
                <AreaChart data={data.days}>
                  <defs>
                    <linearGradient id="stepsGradient" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="0%" stopColor="var(--md-sys-color-primary)" stopOpacity={0.35} />
                      <stop offset="100%" stopColor="var(--md-sys-color-primary)" stopOpacity={0} />
                    </linearGradient>
                  </defs>
                  <XAxis dataKey="date" tick={{ fontSize: 11 }} tickFormatter={(d) => d.slice(5)} stroke="var(--md-sys-color-outline)" />
                  <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={40} />
                  <Tooltip
                    contentStyle={{ background: 'var(--md-sys-color-surface-container-high)', border: 'none', borderRadius: 8 }}
                  />
                  <Area type="monotone" dataKey="steps" stroke="var(--md-sys-color-primary)" fill="url(#stepsGradient)" strokeWidth={2} />
                </AreaChart>
              </ResponsiveContainer>
            </Surface>
          </>
        )}
      </div>
    </div>
  )
}
