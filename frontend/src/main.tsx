import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import '@fontsource-variable/roboto-flex'
import 'leaflet/dist/leaflet.css'
import './components/mw'
import './index.css'
import { App } from './App'
import { LanguageProvider } from './i18n/LanguageContext'
import { ThemeProvider } from './theme/ThemeContext'
import { UnitsProvider } from './units/UnitsContext'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <LanguageProvider>
      <ThemeProvider>
        <UnitsProvider>
          <BrowserRouter>
            <App />
          </BrowserRouter>
        </UnitsProvider>
      </ThemeProvider>
    </LanguageProvider>
  </StrictMode>,
)
