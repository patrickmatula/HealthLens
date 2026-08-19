import { useEffect, useState } from 'react'
import { Route, Routes } from 'react-router-dom'
import { api } from './api/client'
import { NavigationRail } from './components/NavigationRail'
import { DashboardPage } from './pages/DashboardPage'
import { PlaceholderPage } from './pages/PlaceholderPage'
import { UploadPage } from './pages/UploadPage'
import { WorkoutDetailPage } from './pages/WorkoutDetailPage'
import { WorkoutsPage } from './pages/WorkoutsPage'
import { ThemeToggle } from './theme/ThemeToggle'
import './App.css'

export function App() {
  const [hasData, setHasData] = useState<boolean | null>(null)

  useEffect(() => {
    api
      .importCurrent()
      .then((r) => setHasData(r.hasData))
      .catch(() => setHasData(false))
  }, [])

  if (hasData === null) {
    return <div className="ghl-boot-loading" />
  }

  if (!hasData) {
    return (
      <>
        <ThemeToggle />
        <UploadPage onImported={() => setHasData(true)} />
      </>
    )
  }

  return (
    <div className="ghl-shell">
      <ThemeToggle />
      <NavigationRail />
      <main className="ghl-shell__content">
        <Routes>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/workouts" element={<WorkoutsPage />} />
          <Route path="/workouts/:id" element={<WorkoutDetailPage />} />
          <Route path="/sleep" element={<PlaceholderPage title="Schlaf" />} />
          <Route path="/heart" element={<PlaceholderPage title="Herz" />} />
          <Route path="/recovery" element={<PlaceholderPage title="Erholung" />} />
          <Route path="/more" element={<PlaceholderPage title="Mehr" />} />
        </Routes>
      </main>
    </div>
  )
}
