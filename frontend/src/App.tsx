import { useEffect, useState } from 'react'
import { Route, Routes } from 'react-router-dom'
import { api } from './api/client'
import { NavigationRail } from './components/NavigationRail'
import { DashboardPage } from './pages/DashboardPage'
import { HeartPage } from './pages/HeartPage'
import { MorePage } from './pages/MorePage'
import { RecoveryPage } from './pages/RecoveryPage'
import { ShoesPage } from './pages/ShoesPage'
import { SleepDetailPage } from './pages/SleepDetailPage'
import { SleepPage } from './pages/SleepPage'
import { UploadPage } from './pages/UploadPage'
import { WorkoutDetailPage } from './pages/WorkoutDetailPage'
import { WorkoutsPage } from './pages/WorkoutsPage'
import { ThemeToggle } from './theme/ThemeToggle'
import './App.css'

export function App() {
  const [hasData, setHasData] = useState<boolean | null>(null)
  const [isPersistentDb, setIsPersistentDb] = useState(false)

  useEffect(() => {
    api
      .importCurrent()
      .then((r) => {
        setHasData(r.hasData)
        setIsPersistentDb(r.mode === 'Persistent')
      })
      .catch(() => setHasData(false))
  }, [])

  if (hasData === null) {
    return <div className="ghl-boot-loading" />
  }

  if (!hasData) {
    return (
      <>
        <ThemeToggle />
        <UploadPage onImported={() => setHasData(true)} onGoToDashboard={isPersistentDb ? () => setHasData(true) : undefined} />
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
          <Route path="/sleep" element={<SleepPage />} />
          <Route path="/sleep/:id" element={<SleepDetailPage />} />
          <Route path="/heart" element={<HeartPage />} />
          <Route path="/recovery" element={<RecoveryPage />} />
          <Route path="/more" element={<MorePage />} />
          <Route path="/shoes" element={<ShoesPage />} />
        </Routes>
      </main>
    </div>
  )
}
