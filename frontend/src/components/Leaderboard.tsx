import { Link } from 'react-router-dom'
import type { WorkoutListItemDto } from '../api/types'
import { formatDate } from '../utils/format'
import { Icon, type IconName } from './Icon'
import { Surface } from './Surface'
import './Leaderboard.css'

export interface LeaderboardEntry {
  workout: WorkoutListItemDto
  value: string
}

export function Leaderboard({ title, icon, entries }: { title: string; icon: IconName; entries: LeaderboardEntry[] }) {
  if (entries.length === 0) {
    return null
  }

  return (
    <Surface tone="low" className="ghl-leaderboard">
      <h3 className="ghl-leaderboard__title">
        <Icon name={icon} size={18} />
        {title}
      </h3>
      <ol className="ghl-leaderboard__list">
        {entries.map((entry, i) => (
          <li key={entry.workout.id}>
            <Link to={`/workouts/${entry.workout.id}`} className="ghl-leaderboard__row">
              <span className="ghl-leaderboard__rank">{i + 1}</span>
              <span className="ghl-leaderboard__date">{formatDate(entry.workout.startUtc)}</span>
              <span className="ghl-leaderboard__value">{entry.value}</span>
            </Link>
          </li>
        ))}
      </ol>
    </Surface>
  )
}
