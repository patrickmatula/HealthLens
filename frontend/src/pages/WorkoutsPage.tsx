import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Scatter, ScatterChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis, ZAxis } from 'recharts'
import { api } from '../api/client'
import type { PersonalRecordDto, TimeframePreset, WorkoutListItemDto } from '../api/types'
import { Icon } from '../components/Icon'
import { Leaderboard, type LeaderboardEntry } from '../components/Leaderboard'
import { PersonalRecordCard } from '../components/PersonalRecordCard'
import { SegmentedButton } from '../components/SegmentedButton'
import { Surface } from '../components/Surface'
import { TopAppBar } from '../components/TopAppBar'
import { categorizeWorkout, formatDate, formatDateTime, formatDistanceKm, formatDuration, formatPace, type WorkoutCategory } from '../utils/format'
import './WorkoutsPage.css'
import './DashboardPage.css'

const tooltipStyle = { background: 'var(--md-sys-color-surface-container-high)', border: 'none', borderRadius: 8 }

const PRESETS: { value: TimeframePreset; label: string }[] = [
  { value: '30d', label: '30 Tage' },
  { value: '1y', label: '1 Jahr' },
  { value: 'all', label: 'Alle' },
]

const CATEGORIES: { value: WorkoutCategory | 'Alle'; label: string }[] = [
  { value: 'Alle', label: 'Alle' },
  { value: 'Lauf', label: 'Läufe' },
  { value: 'Spaziergang', label: 'Spaziergänge' },
  { value: 'Rad', label: 'Rad' },
  { value: 'Kraft', label: 'Kraft' },
  { value: 'Sonstiges', label: 'Sonstiges' },
]

const PR_ORDER = [
  'FASTEST_KILOMETRE_NAME',
  'FASTEST_MILE_NAME',
  'FASTEST_2_KILOMETRES_NAME',
  'FASTEST_5K_NAME',
  'FASTEST_10K_NAME',
  'FASTEST_2_MILES_NAME',
  'FARTHEST_RUN_NAME',
]

export function WorkoutsPage() {
  const [preset, setPreset] = useState<TimeframePreset>('all')
  const [workouts, setWorkouts] = useState<WorkoutListItemDto[]>([])
  const [records, setRecords] = useState<PersonalRecordDto[]>([])
  const [loading, setLoading] = useState(true)
  const [search, setSearch] = useState('')
  const [category, setCategory] = useState<WorkoutCategory | 'Alle'>('Alle')

  useEffect(() => {
    setLoading(true)
    Promise.all([api.workouts({ preset }), api.bestPersonalRecords()])
      .then(([w, r]) => {
        setWorkouts(w)
        setRecords(r)
      })
      .finally(() => setLoading(false))
  }, [preset])

  const bestRecords = PR_ORDER.map((name) => records.find((r) => r.nameLocalizationId === name)).filter((r): r is PersonalRecordDto => r != null)

  const leaderboards = useMemo(() => {
    const fastest5k: LeaderboardEntry[] = workouts
      .filter((w) => categorizeWorkout(w) === 'Lauf' && w.distanceMeters != null && w.distanceMeters >= 4900 && w.distanceMeters <= 5100)
      .sort((a, b) => a.durationSeconds - b.durationSeconds)
      .slice(0, 5)
      .map((w) => ({ workout: w, value: formatDuration(w.durationSeconds) }))

    const longest: LeaderboardEntry[] = workouts
      .filter((w) => w.distanceMeters != null)
      .sort((a, b) => (b.distanceMeters ?? 0) - (a.distanceMeters ?? 0))
      .slice(0, 5)
      .map((w) => ({ workout: w, value: formatDistanceKm(w.distanceMeters) }))

    const mostCalories: LeaderboardEntry[] = workouts
      .filter((w) => w.calories != null)
      .sort((a, b) => (b.calories ?? 0) - (a.calories ?? 0))
      .slice(0, 5)
      .map((w) => ({ workout: w, value: `${Math.round(w.calories!)} kcal` }))

    return { fastest5k, longest, mostCalories }
  }, [workouts])

  // Pace tends to be noisy run-to-run (terrain, effort, distance), but the trend across many runs
  // answers a question the official app doesn't: am I actually getting faster over time?
  const paceTrend = useMemo(
    () =>
      workouts
        .filter((w) => categorizeWorkout(w) === 'Lauf' && w.avgPaceSecPerKm != null && w.distanceMeters != null && w.distanceMeters >= 1500)
        .map((w) => ({ dateMs: new Date(`${w.startUtc}Z`).getTime(), pace: w.avgPaceSecPerKm!, label: formatDate(w.startUtc) }))
        .sort((a, b) => a.dateMs - b.dateMs),
    [workouts],
  )

  const filteredWorkouts = useMemo(() => {
    const q = search.trim().toLowerCase()
    return workouts.filter((w) => {
      if (category !== 'Alle' && categorizeWorkout(w) !== category) return false
      if (q && !w.activityName.toLowerCase().includes(q) && !formatDateTime(w.startUtc).toLowerCase().includes(q)) return false
      return true
    })
  }, [workouts, search, category])

  return (
    <div>
      <TopAppBar title="Workouts">
        <SegmentedButton options={PRESETS} value={preset} onChange={setPreset} />
      </TopAppBar>

      <div className="ghl-page-content">
        {bestRecords.length > 0 && (
          <section>
            <h2 className="ghl-section-title">Bestleistungen</h2>
            <div className="ghl-pr-grid">
              {bestRecords.map((r) => (
                <PersonalRecordCard key={r.nameLocalizationId} record={r} />
              ))}
            </div>
          </section>
        )}

        {(leaderboards.fastest5k.length > 0 || leaderboards.longest.length > 0 || leaderboards.mostCalories.length > 0) && (
          <section>
            <h2 className="ghl-section-title">Bestenlisten</h2>
            <div className="ghl-leaderboard-grid">
              <Leaderboard title="Top 5 schnellste 5-km-Läufe" icon="route" entries={leaderboards.fastest5k} />
              <Leaderboard title="Top 5 längste Workouts" icon="workouts" entries={leaderboards.longest} />
              <Leaderboard title="Top 5 meiste Kalorien" icon="heart" entries={leaderboards.mostCalories} />
            </div>
          </section>
        )}

        {paceTrend.length >= 5 && (
          <Surface tone="low" className="ghl-chart-card">
            <h2 className="ghl-chart-card__title">Pace-Entwicklung deiner Läufe</h2>
            <p className="ghl-chart-card__hint">Jeder Punkt ist ein Lauf ab 1,5 km — zeigt, ob du über die Zeit schneller wirst.</p>
            <ResponsiveContainer width="100%" height={240}>
              <ScatterChart>
                <CartesianGrid stroke="var(--md-sys-color-outline-variant)" />
                <XAxis
                  dataKey="dateMs"
                  type="number"
                  domain={['dataMin', 'dataMax']}
                  tickFormatter={(v: number) => new Date(v).toLocaleDateString('de-AT', { month: '2-digit', year: '2-digit' })}
                  tick={{ fontSize: 11 }}
                  stroke="var(--md-sys-color-outline)"
                />
                <YAxis dataKey="pace" reversed tickFormatter={(v: number) => formatPace(v)} tick={{ fontSize: 11 }} stroke="var(--md-sys-color-outline)" width={56} />
                <ZAxis range={[50, 50]} />
                <Tooltip contentStyle={tooltipStyle} formatter={(v) => formatPace(Number(v))} labelFormatter={() => ''} />
                <Scatter data={paceTrend} fill="#1e88e5" />
              </ScatterChart>
            </ResponsiveContainer>
          </Surface>
        )}

        <section>
          <div className="ghl-workout-filters">
            <h2 className="ghl-section-title">Alle Workouts</h2>
            <div className="ghl-workout-filters__row">
              <label className="ghl-search-bar">
                <Icon name="search" size={20} />
                <input
                  type="text"
                  className="ghl-search-bar__input"
                  placeholder="Suchen nach Name oder Datum…"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                />
                {search && (
                  <button type="button" className="ghl-search-bar__clear" onClick={() => setSearch('')} aria-label="Suche löschen">
                    <Icon name="close" size={16} />
                  </button>
                )}
              </label>
              <SegmentedButton options={CATEGORIES} value={category} onChange={setCategory} />
            </div>
          </div>

          {!loading && workouts.length === 0 && (
            <Surface tone="low">
              <p>Keine Workouts in diesem Zeitraum.</p>
            </Surface>
          )}

          {!loading && workouts.length > 0 && filteredWorkouts.length === 0 && (
            <Surface tone="low">
              <p>Keine Workouts passen zu diesem Filter.</p>
            </Surface>
          )}

          <div className="ghl-workout-list">
            {filteredWorkouts.map((w) => (
              <Link key={w.id} to={`/workouts/${w.id}`} className="ghl-workout-row">
                <Surface tone="low" className="ghl-workout-row__surface">
                  <div className="ghl-workout-row__icon">
                    <Icon name={w.hasGps ? 'route' : 'workouts'} />
                  </div>
                  <div className="ghl-workout-row__main">
                    <div className="ghl-workout-row__title">{w.activityName}</div>
                    <div className="ghl-workout-row__date">{formatDateTime(w.startUtc)}</div>
                  </div>
                  <div className="ghl-workout-row__stats">
                    <span>{formatDuration(w.durationSeconds)}</span>
                    {w.distanceMeters != null && <span>{formatDistanceKm(w.distanceMeters)}</span>}
                    {w.avgPaceSecPerKm != null && <span>{formatPace(w.avgPaceSecPerKm)}</span>}
                    {w.avgHeartRate != null && <span>{Math.round(w.avgHeartRate)} bpm</span>}
                  </div>
                </Surface>
              </Link>
            ))}
          </div>
        </section>
      </div>
    </div>
  )
}
