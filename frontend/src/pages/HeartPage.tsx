import { useEffect, useState } from 'react'
import { Bar, BarChart, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { api } from '../api/client'
import type { HeartOverviewDto, TimeframePreset } from '../api/types'
import { KpiTile } from '../components/KpiTile'
import { ReferenceRangeGauge } from '../components/ReferenceRangeGauge'
import { SegmentedButton } from '../components/SegmentedButton'
import { Surface } from '../components/Surface'
import { TopAppBar } from '../components/TopAppBar'
import { RESTING_HR_DOMAIN, RESTING_HR_SOURCE, RESTING_HR_SOURCE_URL, RESTING_HR_ZONES, restingHrAssessment } from '../utils/references'
import './DashboardPage.css'

const PRESETS: { value: TimeframePreset; label: string }[] = [
  { value: '30d', label: '30 Tage' },
  { value: '1y', label: '1 Jahr' },
  { value: 'all', label: 'Alle' },
]

const tooltipStyle = { background: 'var(--md-sys-color-surface-container-high)', border: 'none', borderRadius: 8 }

export function HeartPage() {
  const [preset, setPreset] = useState<TimeframePreset>('1y')
  const [data, setData] = useState<HeartOverviewDto | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    setLoading(true)
    api
      .heartOverview({ preset })
      .then(setData)
      .finally(() => setLoading(false))
  }, [preset])

  const hasData = data && (data.restingHeartRate.length > 0 || data.hrv.length > 0)

  return (
    <div>
      <TopAppBar title="Herz">
        <SegmentedButton options={PRESETS} value={preset} onChange={setPreset} />
      </TopAppBar>

      <div className="ghl-page-content">
        {!loading && !hasData && (
          <Surface tone="low">
            <p>Für diesen Zeitraum sind noch keine Herzdaten vorhanden.</p>
          </Surface>
        )}

        {hasData && data && (
          <>
            <div className="ghl-kpi-row">
              {data.avgRestingHeartRate != null && (
                <KpiTile label="Ø Ruhepuls" value={Math.round(data.avgRestingHeartRate).toString()} unit="bpm" />
              )}
              {data.avgHrv != null && <KpiTile label="Ø HRV (RMSSD)" value={data.avgHrv.toFixed(1)} unit="ms" />}
            </div>

            {data.avgRestingHeartRate != null && (
              <Surface tone="low" className="ghl-chart-card">
                <h2 className="ghl-chart-card__title">Wie gut ist mein Ruhepuls?</h2>
                <ReferenceRangeGauge value={data.avgRestingHeartRate} domain={RESTING_HR_DOMAIN} zones={RESTING_HR_ZONES} unit="bpm" />
                <p className="ghl-chart-card__hint">
                  {restingHrAssessment(data.avgRestingHeartRate)} Quelle:{' '}
                  <a href={RESTING_HR_SOURCE_URL} target="_blank" rel="noreferrer">
                    {RESTING_HR_SOURCE}
                  </a>
                  . Keine medizinische Diagnose — bei Beschwerden ärztlichen Rat einholen.
                </p>
              </Surface>
            )}

            {data.restingHeartRate.length > 0 && (
              <Surface tone="low" className="ghl-chart-card">
                <h2 className="ghl-chart-card__title">Ruhepuls im Zeitverlauf</h2>
                <ResponsiveContainer width="100%" height={240}>
                  <LineChart data={data.restingHeartRate}>
                    <XAxis dataKey="date" tick={{ fontSize: 11 }} tickFormatter={(d: string) => d.slice(5)} stroke="var(--md-sys-color-outline)" />
                    <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={36} domain={['dataMin - 3', 'dataMax + 3']} />
                    <Tooltip contentStyle={tooltipStyle} formatter={(v) => `${Number(v).toFixed(0)} bpm`} />
                    <Line type="monotone" dataKey="bpm" stroke="#e53935" dot={false} strokeWidth={2} />
                  </LineChart>
                </ResponsiveContainer>
              </Surface>
            )}

            {data.hrv.length > 0 && (
              <Surface tone="low" className="ghl-chart-card">
                <h2 className="ghl-chart-card__title">Herzfrequenzvariabilität (RMSSD)</h2>
                <ResponsiveContainer width="100%" height={220}>
                  <LineChart data={data.hrv}>
                    <XAxis dataKey="date" tick={{ fontSize: 11 }} tickFormatter={(d: string) => d.slice(5)} stroke="var(--md-sys-color-outline)" />
                    <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={36} />
                    <Tooltip contentStyle={tooltipStyle} formatter={(v) => `${Number(v).toFixed(1)} ms`} />
                    <Line type="monotone" dataKey="rmssdMs" stroke="#8e24aa" dot={false} strokeWidth={2} />
                  </LineChart>
                </ResponsiveContainer>
              </Surface>
            )}

            {data.activeZoneMinutes.length > 0 && (
              <Surface tone="low" className="ghl-chart-card">
                <h2 className="ghl-chart-card__title">Active Zone Minutes</h2>
                <ResponsiveContainer width="100%" height={220}>
                  <BarChart data={data.activeZoneMinutes}>
                    <XAxis dataKey="date" tick={{ fontSize: 11 }} tickFormatter={(d: string) => d.slice(5)} stroke="var(--md-sys-color-outline)" />
                    <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={30} />
                    <Tooltip contentStyle={tooltipStyle} />
                    <Bar dataKey="fatBurnMinutes" stackId="azm" fill="#ffb300" name="Fettverbrennung" />
                    <Bar dataKey="cardioMinutes" stackId="azm" fill="#fb8c00" name="Cardio" />
                    <Bar dataKey="peakMinutes" stackId="azm" fill="#e53935" name="Peak" />
                  </BarChart>
                </ResponsiveContainer>
              </Surface>
            )}

            {data.respiratoryRate.length > 0 && (
              <Surface tone="low" className="ghl-chart-card">
                <h2 className="ghl-chart-card__title">Atemfrequenz im Schlaf</h2>
                <ResponsiveContainer width="100%" height={180}>
                  <LineChart data={data.respiratoryRate}>
                    <XAxis dataKey="date" tick={{ fontSize: 11 }} tickFormatter={(d: string) => d.slice(5)} stroke="var(--md-sys-color-outline)" />
                    <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={30} domain={['dataMin - 1', 'dataMax + 1']} />
                    <Tooltip contentStyle={tooltipStyle} formatter={(v) => `${Number(v).toFixed(1)} /min`} />
                    <Line type="monotone" dataKey="breathsPerMinute" stroke="#26a69a" dot={false} strokeWidth={2} />
                  </LineChart>
                </ResponsiveContainer>
              </Surface>
            )}
          </>
        )}
      </div>
    </div>
  )
}
