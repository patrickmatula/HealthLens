import { useEffect, useState } from 'react'
import { Area, AreaChart, CartesianGrid, Line, LineChart, ResponsiveContainer, Scatter, ScatterChart, Tooltip, XAxis, YAxis, ZAxis } from 'recharts'
import { api } from '../api/client'
import type { CorrelationPointDto, RecoveryOverviewDto, TimeframePreset } from '../api/types'
import { SegmentedButton } from '../components/SegmentedButton'
import { Surface } from '../components/Surface'
import { TopAppBar } from '../components/TopAppBar'
import './DashboardPage.css'

const PRESETS: { value: TimeframePreset; label: string }[] = [
  { value: '30d', label: '30 Tage' },
  { value: '1y', label: '1 Jahr' },
  { value: 'all', label: 'Alle' },
]

const tooltipStyle = { background: 'var(--md-sys-color-surface-container-high)', border: 'none', borderRadius: 8 }

export function RecoveryPage() {
  const [preset, setPreset] = useState<TimeframePreset>('all')
  const [data, setData] = useState<RecoveryOverviewDto | null>(null)
  const [correlation, setCorrelation] = useState<CorrelationPointDto[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    setLoading(true)
    Promise.all([api.recoveryOverview({ preset }), api.correlation()])
      .then(([overview, corr]) => {
        setData(overview)
        setCorrelation(corr)
      })
      .finally(() => setLoading(false))
  }, [preset])

  const hasData =
    data && (data.stressScore.length > 0 || data.readiness.length > 0 || data.spO2.length > 0 || data.temperature.length > 0)

  const rhrCorrelation = correlation.filter((c) => c.restingHeartRate != null)

  return (
    <div>
      <TopAppBar title="Erholung">
        <SegmentedButton options={PRESETS} value={preset} onChange={setPreset} />
      </TopAppBar>

      <div className="ghl-page-content">
        {!loading && !hasData && correlation.length === 0 && (
          <Surface tone="low">
            <p>Für diesen Zeitraum sind noch keine Erholungsdaten vorhanden.</p>
          </Surface>
        )}

        {rhrCorrelation.length >= 3 && (
          <Surface tone="low" className="ghl-chart-card">
            <h2 className="ghl-chart-card__title">Schlaf-Score vs. Ruhepuls am selben Tag</h2>
            <p className="ghl-chart-card__hint">
              Jeder Punkt ist eine Nacht: schlechter Schlaf-Score korreliert oft mit höherem Ruhepuls am Folgetag — etwas, das die
              offizielle App nicht direkt gegenüberstellt.
            </p>
            <ResponsiveContainer width="100%" height={280}>
              <ScatterChart>
                <CartesianGrid stroke="var(--md-sys-color-outline-variant)" />
                <XAxis type="number" dataKey="sleepScore" name="Schlaf-Score" tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" domain={['dataMin - 5', 'dataMax + 5']} />
                <YAxis type="number" dataKey="restingHeartRate" name="Ruhepuls" tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={36} domain={['dataMin - 2', 'dataMax + 2']} />
                <ZAxis range={[60, 60]} />
                <Tooltip contentStyle={tooltipStyle} cursor={{ strokeDasharray: '3 3' }} formatter={(v) => Number(v).toFixed(1)} />
                <Scatter data={rhrCorrelation} fill="#8e24aa" />
              </ScatterChart>
            </ResponsiveContainer>
          </Surface>
        )}

        {data && data.stressScore.length > 0 && (
          <Surface tone="low" className="ghl-chart-card">
            <h2 className="ghl-chart-card__title">Stress-Score</h2>
            <ResponsiveContainer width="100%" height={200}>
              <LineChart data={data.stressScore}>
                <XAxis dataKey="date" tick={{ fontSize: 11 }} tickFormatter={(d: string) => d.slice(5, 10)} stroke="var(--md-sys-color-outline)" />
                <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={30} domain={[0, 100]} />
                <Tooltip contentStyle={tooltipStyle} />
                <Line type="monotone" dataKey="score" stroke="#fb8c00" dot={{ r: 3 }} strokeWidth={2} />
              </LineChart>
            </ResponsiveContainer>
          </Surface>
        )}

        {data && data.readiness.length > 0 && (
          <Surface tone="low" className="ghl-chart-card">
            <h2 className="ghl-chart-card__title">Readiness</h2>
            <ResponsiveContainer width="100%" height={200}>
              <LineChart data={data.readiness}>
                <XAxis dataKey="date" tick={{ fontSize: 11 }} tickFormatter={(d: string) => d.slice(5, 10)} stroke="var(--md-sys-color-outline)" />
                <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={30} domain={[0, 100]} />
                <Tooltip contentStyle={tooltipStyle} />
                <Line type="monotone" dataKey="score" stroke="#43a047" dot={{ r: 3 }} strokeWidth={2} />
              </LineChart>
            </ResponsiveContainer>
          </Surface>
        )}

        {data && data.spO2.length > 0 && (
          <Surface tone="low" className="ghl-chart-card">
            <h2 className="ghl-chart-card__title">Sauerstoffsättigung (SpO2)</h2>
            <ResponsiveContainer width="100%" height={200}>
              <AreaChart data={data.spO2}>
                <XAxis dataKey="date" tick={{ fontSize: 11 }} tickFormatter={(d: string) => d.slice(5, 10)} stroke="var(--md-sys-color-outline)" />
                <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={30} domain={['dataMin - 2', 100]} />
                <Tooltip contentStyle={tooltipStyle} formatter={(v) => `${Number(v).toFixed(1)}%`} />
                <Area type="monotone" dataKey="averagePercent" stroke="#039be5" fill="#039be5" fillOpacity={0.2} strokeWidth={2} />
              </AreaChart>
            </ResponsiveContainer>
          </Surface>
        )}

        {data && data.temperature.length > 0 && (
          <Surface tone="low" className="ghl-chart-card">
            <h2 className="ghl-chart-card__title">Hauttemperatur (Abweichung zur Baseline)</h2>
            <ResponsiveContainer width="100%" height={200}>
              <LineChart data={data.temperature}>
                <XAxis dataKey="date" tick={{ fontSize: 11 }} tickFormatter={(d: string) => d.slice(5, 10)} stroke="var(--md-sys-color-outline)" />
                <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={30} unit="°" />
                <Tooltip contentStyle={tooltipStyle} formatter={(v) => `${Number(v).toFixed(2)}°C`} />
                <Line type="monotone" dataKey="deltaFromBaseline" stroke="#d81b60" dot={{ r: 3 }} strokeWidth={2} connectNulls />
              </LineChart>
            </ResponsiveContainer>
          </Surface>
        )}
      </div>
    </div>
  )
}
