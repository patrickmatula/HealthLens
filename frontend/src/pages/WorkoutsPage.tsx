import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import type { PersonalRecordDto, TimeframePreset, WorkoutListItemDto } from '../api/types'
import { Icon } from '../components/Icon'
import { PersonalRecordCard } from '../components/PersonalRecordCard'
import { SegmentedButton } from '../components/SegmentedButton'
import { Surface } from '../components/Surface'
import { TopAppBar } from '../components/TopAppBar'
import { categorizeWorkout, formatDateTime, formatDistanceKm, formatDuration, formatPace, type WorkoutCategory } from '../utils/format'
import './WorkoutsPage.css'

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
                <PersonalRecordCard key={r.id} record={r} />
              ))}
            </div>
          </section>
        )}

        <section>
          <div className="ghl-workout-filters">
            <h2 className="ghl-section-title">Alle Workouts</h2>
            <md-outlined-text-field
              placeholder="Suchen nach Name oder Datum…"
              value={search}
              onInput={(e) => setSearch((e.target as HTMLInputElement).value)}
              className="ghl-workout-search"
            />
            <SegmentedButton options={CATEGORIES} value={category} onChange={setCategory} />
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
