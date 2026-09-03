import { useEffect, useState } from 'react'
import { Bar, BarChart, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { Link, useParams } from 'react-router-dom'
import { api } from '../api/client'
import type { ShoeDto, WorkoutDetailDto } from '../api/types'
import { Icon } from '../components/Icon'
import { KpiTile } from '../components/KpiTile'
import { Surface } from '../components/Surface'
import { TopAppBar } from '../components/TopAppBar'
import { ReferenceRangeGauge } from '../components/ReferenceRangeGauge'
import { WorkoutRouteMap } from '../components/WorkoutRouteMap'
import { useLanguage } from '../i18n/LanguageContext'
import { useShoesFeature } from '../shoes/ShoesFeatureContext'
import { formatDateTime, formatDistanceKm, formatDuration, formatPace, formatRecordValue, recordLabel } from '../utils/format'
import {
  CADENCE_DOMAIN,
  GCT_DOMAIN,
  RUNNING_DYNAMICS_SOURCE,
  RUNNING_DYNAMICS_SOURCE_URL,
  VERTICAL_OSC_DOMAIN,
  getCadenceZones,
  getGctZones,
  getVerticalOscZones,
} from '../utils/references'
import './WorkoutDetailPage.css'

export function WorkoutDetailPage() {
  const { language, t } = useLanguage()
  const { enabled: shoesEnabled } = useShoesFeature()
  const { id } = useParams<{ id: string }>()
  const [workout, setWorkout] = useState<WorkoutDetailDto | null>(null)
  const [loading, setLoading] = useState(true)
  const [shoes, setShoes] = useState<ShoeDto[]>([])

  useEffect(() => {
    if (!id) return
    setLoading(true)
    api
      .workoutDetail(id)
      .then(setWorkout)
      .finally(() => setLoading(false))
  }, [id])

  useEffect(() => {
    if (shoesEnabled) {
      api.shoes().then(setShoes)
    }
  }, [shoesEnabled])

  async function handleShoeChange(value: string) {
    if (!workout) return
    const shoeId = value === '' ? null : Number(value)
    await api.assignShoe(shoeId, [workout.id])
    const shoeName = shoeId === null ? null : (shoes.find((s) => s.id === shoeId)?.name ?? null)
    setWorkout({ ...workout, shoeId, shoeName })
  }

  if (loading) {
    return (
      <div>
        <TopAppBar title={t('detail.title')} />
      </div>
    )
  }

  if (!workout) {
    return (
      <div>
        <TopAppBar title={t('detail.title')} />
        <div className="ghl-page-content">
          <Surface tone="low">
            <p>{t('detail.notFound')}</p>
          </Surface>
        </div>
      </div>
    )
  }

  const startMs = new Date(workout.startUtc + 'Z').getTime()
  const chartData = workout.samples.map((s) => ({
    elapsedMin: (new Date(s.timestamp + 'Z').getTime() - startMs) / 60000,
    heartRateBpm: s.heartRateBpm,
    cadenceSpm: s.cadenceSpm,
  }))

  const hasHr = chartData.some((d) => d.heartRateBpm != null)
  const hasCadence = chartData.some((d) => d.cadenceSpm != null)
  // Instantaneous per-second pace is dominated by noise (a red light, a sip of water, a GPS jitter all
  // spike momentary pace to absurd values), which blows out the chart's scale and makes the real trend
  // unreadable. Per-km splits are already averaged over a full kilometer, so they're stable enough to
  // chart directly -- the same "bar per km" convention Strava/Garmin use for exactly this reason.
  const hasPaceTrend = workout.kmSplits.length > 1

  return (
    <div>
      <TopAppBar title={workout.activityName} actions={<Link to="/workouts" className="ghl-back-link">{t('detail.backToWorkouts')}</Link>} />

      <div className="ghl-page-content">
        <Surface tone="low" className="ghl-workout-header">
          <div>{formatDateTime(workout.startUtc)}</div>
          <div className="ghl-workout-header__stats">
            <KpiTile label={t('detail.duration')} value={formatDuration(workout.durationSeconds)} />
            {workout.distanceMeters != null && <KpiTile label={t('detail.distance')} value={formatDistanceKm(workout.distanceMeters)} />}
            {workout.avgPaceSecPerKm != null && <KpiTile label={t('detail.avgPace')} value={formatPace(workout.avgPaceSecPerKm)} />}
            {workout.avgHeartRate != null && <KpiTile label={t('detail.avgHr')} value={Math.round(workout.avgHeartRate).toString()} unit="bpm" />}
            {workout.peakHeartRate != null && <KpiTile label={t('detail.maxHr')} value={Math.round(workout.peakHeartRate).toString()} unit="bpm" />}
            {workout.calories != null && <KpiTile label={t('detail.calories')} value={Math.round(workout.calories).toString()} unit="kcal" />}
            {workout.cadenceAvgSpm != null && <KpiTile label={t('detail.avgCadence')} value={Math.round(workout.cadenceAvgSpm).toString()} unit="spm" />}
            {workout.elevationGainMeters != null && <KpiTile label={t('detail.elevation')} value={Math.round(workout.elevationGainMeters).toString()} unit="m" />}
          </div>

          {shoesEnabled && (
            <div className="ghl-workout-header__shoe">
              <Icon name="shoe" size={18} />
              <span className="ghl-workout-header__shoe-label">{t('shoes.assignedLabel')}</span>
              <select
                className="ghl-shoe-select-bar__picker"
                value={workout.shoeId ?? ''}
                onChange={(e) => handleShoeChange(e.target.value)}
              >
                <option value="">{t('shoes.noneOption')}</option>
                {shoes.map((shoe) => (
                  <option key={shoe.id} value={shoe.id}>
                    {shoe.name}
                  </option>
                ))}
              </select>
            </div>
          )}
        </Surface>

        {workout.personalRecords.length > 0 && (
          <Surface tone="low">
            <h2 className="ghl-section-title">{t('detail.prsInWorkout')}</h2>
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
            <h2 className="ghl-chart-card__title">{t('detail.runMetricsCompare')}</h2>
            {workout.cadenceAvgSpm != null && (
              <div>
                <div className="ghl-metric-name">{t('detail.cadence')}</div>
                <ReferenceRangeGauge value={workout.cadenceAvgSpm} domain={CADENCE_DOMAIN} zones={getCadenceZones(language)} unit="spm" />
              </div>
            )}
            {workout.groundContactTimeMs != null && (
              <div>
                <div className="ghl-metric-name">{t('detail.groundContactTime')}</div>
                <ReferenceRangeGauge value={workout.groundContactTimeMs} domain={GCT_DOMAIN} zones={getGctZones(language)} unit="ms" />
              </div>
            )}
            {workout.verticalOscillationMm != null && (
              <div>
                <div className="ghl-metric-name">{t('detail.verticalOscillation')}</div>
                <ReferenceRangeGauge value={workout.verticalOscillationMm / 10} domain={VERTICAL_OSC_DOMAIN} zones={getVerticalOscZones(language)} unit="cm" />
              </div>
            )}
            <p className="ghl-chart-card__hint">
              {t('detail.referenceHintPrefix')}{' '}
              <a href={RUNNING_DYNAMICS_SOURCE_URL} target="_blank" rel="noreferrer">
                {RUNNING_DYNAMICS_SOURCE}
              </a>
              {t('detail.referenceHintSuffix')}
            </p>
          </Surface>
        )}

        {workout.hasGps && <WorkoutRouteMap samples={workout.samples} />}

        {hasHr && (
          <Surface tone="low" className="ghl-chart-card">
            <h2 className="ghl-chart-card__title">{t('detail.hrOverTime')}</h2>
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

        {hasPaceTrend && (
          <Surface tone="low" className="ghl-chart-card">
            <h2 className="ghl-chart-card__title">{t('detail.paceOverTime')}</h2>
            <ResponsiveContainer width="100%" height={220}>
              <BarChart data={workout.kmSplits}>
                <XAxis dataKey="km" tickFormatter={(v: number) => `${v} km`} tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" />
                <YAxis
                  tick={{ fontSize: 11 }}
                  stroke="var(--md-sys-color-outline)"
                  width={48}
                  reversed
                  domain={['dataMin - 15', 'dataMax + 15']}
                  tickFormatter={(v: number) => formatPace(v)}
                />
                <Tooltip
                  contentStyle={{ background: 'var(--md-sys-color-surface-container-high)', border: 'none', borderRadius: 8 }}
                  formatter={(v) => formatPace(Number(v))}
                  labelFormatter={(v) => `km ${v}`}
                />
                <Bar dataKey="durationSeconds" fill="#1e88e5" radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </Surface>
        )}

        {hasCadence && (
          <Surface tone="low" className="ghl-chart-card">
            <h2 className="ghl-chart-card__title">{t('detail.cadenceOverTime')}</h2>
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
            <h2 className="ghl-section-title">{t('detail.kmSplits')}</h2>
            <div className="ghl-table-scroll">
              <table className="ghl-splits-table">
                <thead>
                  <tr>
                    <th>km</th>
                    <th>Pace</th>
                    <th>{t('detail.tableAvgHr')}</th>
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
            <h2 className="ghl-section-title">{t('detail.splits')}</h2>
            <div className="ghl-table-scroll">
            <table className="ghl-splits-table">
              <thead>
                <tr>
                  <th>#</th>
                  <th>{t('detail.tableType')}</th>
                  <th>{t('detail.tableDistance')}</th>
                  <th>Pace</th>
                  <th>{t('detail.tableAvgHr')}</th>
                  <th>{t('detail.tableCalories')}</th>
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
