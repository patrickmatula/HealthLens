import { Surface } from '../components/Surface'
import { TopAppBar } from '../components/TopAppBar'
import './DashboardPage.css'

export function PlaceholderPage({ title }: { title: string }) {
  return (
    <div>
      <TopAppBar title={title} />
      <div className="ghl-page-content">
        <Surface tone="low">
          <p>Dieser Bereich wird in einer der nächsten Ausbaustufen gebaut.</p>
        </Surface>
      </div>
    </div>
  )
}
