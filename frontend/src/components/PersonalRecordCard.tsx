import { Link } from 'react-router-dom'
import type { PersonalRecordDto } from '../api/types'
import { formatDate, formatRecordValue, recordLabel } from '../utils/format'
import { Icon } from './Icon'
import { Surface } from './Surface'
import './PersonalRecordCard.css'

export function PersonalRecordCard({ record }: { record: PersonalRecordDto }) {
  const content = (
    <>
      <div className="ghl-pr-card__header">
        <Icon name="trophy" size={18} />
        <span className="ghl-pr-card__label">{recordLabel(record.nameLocalizationId)}</span>
      </div>
      <div className="ghl-pr-card__value">{formatRecordValue(record)}</div>
      <div className="ghl-pr-card__date">{formatDate(record.achieveTimeUtc)}</div>
      {record.state === 'PERSONAL_RECORD_STATE_STANDING' && <span className="ghl-pr-card__state">Aktuell</span>}
    </>
  )

  if (record.workoutId) {
    return (
      <Link to={`/workouts/${record.workoutId}`} className="ghl-pr-card">
        <Surface tone="low" padded className="ghl-pr-card__surface">
          {content}
        </Surface>
      </Link>
    )
  }

  return (
    <Surface tone="low" className="ghl-pr-card" padded>
      {content}
    </Surface>
  )
}
