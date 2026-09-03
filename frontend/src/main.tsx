import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import '@fontsource-variable/roboto-flex'
import 'leaflet/dist/leaflet.css'
import './components/mw'
import './index.css'
import { App } from './App'
import { BodyFeatureProvider } from './body/BodyFeatureContext'
import { LanguageProvider } from './i18n/LanguageContext'
import { ShoesFeatureProvider } from './shoes/ShoesFeatureContext'
import { ThemeProvider } from './theme/ThemeContext'
import { UnitsProvider } from './units/UnitsContext'
import { WeatherFeatureProvider } from './weather/WeatherFeatureContext'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <LanguageProvider>
      <ThemeProvider>
        <UnitsProvider>
          <ShoesFeatureProvider>
            <BodyFeatureProvider>
              <WeatherFeatureProvider>
                <BrowserRouter>
                  <App />
                </BrowserRouter>
              </WeatherFeatureProvider>
            </BodyFeatureProvider>
          </ShoesFeatureProvider>
        </UnitsProvider>
      </ThemeProvider>
    </LanguageProvider>
  </StrictMode>,
)
