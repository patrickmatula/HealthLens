import { useEffect, useState } from 'react'
import { Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { Link, useParams } from 'react-router-dom'
import { api } from '../api/client'
import type { WorkoutDetailDto } from '../api/types'
import { Icon } from '../components/Icon'
import { KpiTile } from '../components/KpiTile'
import { Surface } from '../components/Surface'
import { TopAppBar } from '../components/TopAppBar'
import { ReferenceRangeGauge } from '../components/ReferenceRangeGauge'
import { WorkoutRouteMap } from '../components/WorkoutRouteMap'
import { formatDateTime, formatDistanceKm, formatDuration, formatPace, formatRecordValue, recordLabel } from '../utils/format'
import {
  CADENCE_DOMAIN,
  CADENCE_ZONES,
  GCT_DOMAIN,
  GCT_ZONES,
  RUNNING_DYNAMICS_SOURCE,
  RUNNING_DYNAMICS_SOURCE_URL,
  VERTICAL_OSC_DOMAIN,
  VERTICAL_OSC_ZONES,
} from '../utils/references'
import './WorkoutDetailPage.css'

export function WorkoutDetailPage() {
  const { id } = useParams<{ id: string }>()
  const [workout, setWorkout] = useState<WorkoutDetailDto | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    if (!id) return
    setLoading(true)
    api
      .workoutDetail(id)
      .then(setWorkout)
      .finally(() => setLoading(false))
  }, [id])

  if (loading) {
    return (
      <div>
        <TopAppBar title="Workout" />
      </div>
    )
  }

  if (!workout) {
    return (
      <div>
        <TopAppBar title="Workout" />
        <div className="ghl-page-content">
          <Surface tone="low">
            <p>Workout nicht gefunden.</p>
          </Surface>
        </div>
      </div>
    )
  }

  const startMs = new Date(workout.startUtc + 'Z').getTime()
  const chartData = workout.samples.map((s) => ({
    elapsedMin: (new Date(s.timestamp + 'Z').getTime() - startMs) / 60000,
    heartRateBpm: s.heartRateBpm,
    paceSecPerKm: s.paceSecPerKm,
    cadenceSpm: s.cadenceSpm,
  }))

  const hasHr = chartData.some((d) => d.heartRateBpm != null)
  const hasPace = chartData.some((d) => d.paceSecPerKm != null)
  const hasCadence = chartData.some((d) => d.cadenceSpm != null)

  return (
    <div>
      <TopAppBar title={workout.activityName} actions={<Link to="/workouts" className="ghl-back-link">Alle Workouts</Link>} />

      <div className="ghl-page-content">
        <Surface tone="low" className="ghl-workout-header">
          <div>{formatDateTime(workout.startUtc)}</div>
          <div className="ghl-workout-header__stats">
            <KpiTile label="Dauer" value={formatDuration(workout.durationSeconds)} />
            {workout.distanceMeters != null && <KpiTile label="Distanz" value={formatDistanceKm(workout.distanceMeters)} />}
            {workout.avgPaceSecPerKm != null && <KpiTile label="Ø Pace" value={formatPace(workout.avgPaceSecPerKm)} />}
            {workout.avgHeartRate != null && <KpiTile label="Ø Herzfrequenz" value={Math.round(workout.avgHeartRate).toString()} unit="bpm" />}
            {workout.peakHeartRate != null && <KpiTile label="Max. Herzfrequenz" value={Math.round(workout.peakHeartRate).toString()} unit="bpm" />}
            {workout.calories != null && <KpiTile label="Kalorien" value={Math.round(workout.calories).toString()} unit="kcal" />}
            {workout.cadenceAvgSpm != null && <KpiTile label="Ø Kadenz" value={Math.round(workout.cadenceAvgSpm).toString()} unit="spm" />}
            {workout.elevationGainMeters != null && <KpiTile label="Höhenmeter" value={Math.round(workout.elevationGainMeters).toString()} unit="m" />}
          </div>
        </Surface>

        {workout.personalRecords.length > 0 && (
          <Surface tone="low">
            <h2 className="ghl-section-title">Bestleistungen bei diesem Workout</h2>
            <ul className="ghl-workout-pr-list">
              {workout.personalRecords.map((r) => (
                <li key={r.nameLocalizationId}>
                  <Icon name="trophy" size={16} /> {recordLabel(r.nameLocalizationId)}: <strong>{formatRecordValue(r)}</strong>
                </li>
              ))}
            </ul>
          </Surface>
        )}

        {(workout.cadenceAvgSpm != null || workout.groundContactTimeMs != null || workout.verticalOscillationMm != null) && (
          <Surface tone="low" className="ghl-chart-card">
            <h2 className="ghl-chart-card__title">Lauf-Metriken im Vergleich</h2>
            {workout.cadenceAvgSpm != null && (
              <div>
                <div className="ghl-metric-name">Kadenz</div>
                <ReferenceRangeGauge value={workout.cadenceAvgSpm} domain={CADENCE_DOMAIN} zones={CADENCE_ZONES} unit="spm" />
              </div>
            )}
            {workout.groundContactTimeMs != null && (
              <div>
                <div className="ghl-metric-name">Bodenkontaktzeit</div>
                <ReferenceRangeGauge value={workout.groundContactTimeMs} domain={GCT_DOMAIN} zones={GCT_ZONES} unit="ms" />
              </div>
            )}
            {workout.verticalOscillationMm != null && (
              <div>
                <div className="ghl-metric-name">Vertikalbewegung</div>
                <ReferenceRangeGauge value={workout.verticalOscillationMm / 10} domain={VERTICAL_OSC_DOMAIN} zones={VERTICAL_OSC_ZONES} unit="cm" />
              </div>
            )}
            <p className="ghl-chart-card__hint">
              Referenzbereiche aus Perzentil-Daten von{' '}
              <a href={RUNNING_DYNAMICS_SOURCE_URL} target="_blank" rel="noreferrer">
                {RUNNING_DYNAMICS_SOURCE}
              </a>
              . Werte anderer Hersteller/Sensorpositionen können leicht abweichen — als Orientierung, nicht als exakter Vergleichsmaßstab gedacht.
            </p>
          </Surface>
        )}

        {workout.hasGps && <WorkoutRouteMap samples={workout.samples} />}

        {hasHr && (
          <Surface tone="low" className="ghl-chart-card">
            <h2 className="ghl-chart-card__title">Herzfrequenz im Verlauf</h2>
            <ResponsiveContainer width="100%" height={220}>
              <LineChart data={chartData}>
                <XAxis dataKey="elapsedMin" tickFormatter={(v: number) => `${Math.round(v)}'`} tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" />
                <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={36} domain={['dataMin - 5', 'dataMax + 5']} />
                <Tooltip contentStyle={{ background: 'var(--md-sys-color-surface-container-high)', border: 'none', borderRadius: 8 }} labelFormatter={(v) => `${Number(v).toFixed(1)} min`} />
                <Line type="monotone" dataKey="heartRateBpm" stroke="#e53935" dot={false} strokeWidth={2} connectNulls />
              </LineChart>
            </ResponsiveContainer>
          </Surface>
        )}

        {hasPace && (
          <Surface tone="low" className="ghl-chart-card">
            <h2 className="ghl-chart-card__title">Pace im Verlauf</h2>
            <ResponsiveContainer width="100%" height={220}>
              <LineChart data={chartData}>
                <XAxis dataKey="elapsedMin" tickFormatter={(v: number) => `${Math.round(v)}'`} tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" />
                <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={48} reversed tickFormatter={(v: number) => formatPace(v)} />
                <Tooltip contentStyle={{ background: 'var(--md-sys-color-surface-container-high)', border: 'none', borderRadius: 8 }} formatter={(v) => formatPace(Number(v))} labelFormatter={(v) => `${Number(v).toFixed(1)} min`} />
                <Line type="monotone" dataKey="paceSecPerKm" stroke="#1e88e5" dot={false} strokeWidth={2} connectNulls />
              </LineChart>
            </ResponsiveContainer>
          </Surface>
        )}

        {hasCadence && (
          <Surface tone="low" className="ghl-chart-card">
            <h2 className="ghl-chart-card__title">Kadenz im Verlauf</h2>
            <ResponsiveContainer width="100%" height={180}>
              <LineChart data={chartData}>
                <XAxis dataKey="elapsedMin" tickFormatter={(v: number) => `${Math.round(v)}'`} tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" />
                <YAxis tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={36} domain={['dataMin - 5', 'dataMax + 5']} />
                <Tooltip contentStyle={{ background: 'var(--md-sys-color-surface-container-high)', border: 'none', borderRadius: 8 }} labelFormatter={(v) => `${Number(v).toFixed(1)} min`} />
                <Line type="monotone" dataKey="cadenceSpm" stroke="#8e24aa" dot={false} strokeWidth={2} connectNulls />
              </LineChart>
            </ResponsiveContainer>
          </Surface>
        )}

        {workout.kmSplits.length > 0 && (
          <Surface tone="low">
            <h2 className="ghl-section-title">Kilometer-Splits</h2>
            <div className="ghl-table-scroll">
              <table className="ghl-splits-table">
                <thead>
                  <tr>
                    <th>km</th>
                    <th>Pace</th>
                    <th>Ø HF</th>
                  </tr>
                </thead>
                <tbody>
                  {workout.kmSplits.map((s) => (
                    <tr key={s.km}>
                      <td>{s.km}</td>
                      <td>{formatPace(s.durationSeconds)}</td>
                      <td>{s.avgHeartRate ? `${Math.round(s.avgHeartRate)} bpm` : '–'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Surface>
        )}

        {workout.splits.length > 0 && (
          <Surface tone="low">
            <h2 className="ghl-section-title">Splits</h2>
            <div className="ghl-table-scroll">
            <table className="ghl-splits-table">
              <thead>
                <tr>
                  <th>#</th>
                  <th>Typ</th>
                  <th>Distanz</th>
                  <th>Pace</th>
                  <th>Ø HF</th>
                  <th>Kalorien</th>
                </tr>
              </thead>
              <tbody>
                {workout.splits.map((s) => (
                  <tr key={s.splitIndex}>
                    <td>{s.splitIndex + 1}</td>
                    <td>{s.type}</td>
                    <td>{formatDistanceKm(s.distanceMeters)}</td>
                    <td>{s.avgSpeedKmh ? formatPace(3600 / s.avgSpeedKmh) : '–'}</td>
                    <td>{s.avgHeartRate ? Math.round(s.avgHeartRate) : '–'}</td>
                    <td>{Math.round(s.calories)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            </div>
          </Surface>
        )}
      </div>
    </div>
  )
}
