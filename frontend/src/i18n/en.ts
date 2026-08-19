import type { TranslationKey } from './de'

const en: Record<TranslationKey, string> = {
  'nav.mainLabel': 'Main navigation',
  'nav.dashboard': 'Overview',
  'nav.workouts': 'Workouts',
  'nav.sleep': 'Sleep',
  'nav.heart': 'Heart',
  'nav.recovery': 'Recovery',
  'nav.more': 'More',

  'theme.system': 'Theme: System — click for Light',
  'theme.light': 'Theme: Light — click for Dark',
  'theme.dark': 'Theme: Dark — click for System',

  'upload.title': 'GoogleHealthLens',
  'upload.subtitle': 'Upload your Google Takeout export (Google Health) as a .zip to explore your Fitbit/health data.',
  'upload.dropzoneCta': 'Choose a zip file…',
  'upload.persistentTitle': 'Save permanently',
  'upload.persistentHintOn': 'Data is written to a local SQLite database and survives a restart.',
  'upload.persistentHintOff': 'Data is only loaded for this session — it will be gone after restarting the app.',
  'upload.scopeGroupLabel': 'Data scope',
  'upload.scopeCuratedTitle': 'Curated (recommended)',
  'upload.scopeCuratedHint':
    'Full summaries, high-frequency raw data (heart rate etc.) aggregated — except for workouts. Faster import.',
  'upload.scopeFullTitle': 'Full, everything raw',
  'upload.scopeFullHint': 'Every row imported 1:1. Larger database, significantly longer import.',
  'upload.startButton': 'Start import',
  'upload.backButton': '← Back',
  'upload.dashboardButton': 'Go to dashboard →',
  'upload.progressRows': '{count} rows imported',
  'upload.errorGeneric': 'Could not start the import.',
  'upload.errorFailed': 'Import failed.',
  'upload.errorConnection': 'Lost connection to the server.',

  'more.settingsTitle': 'Settings',
  'more.unitsLabel': 'Units',
  'more.unitMetric': 'Metric (km)',
  'more.unitImperial': 'Imperial (mi)',
  'more.languageLabel': 'Language',
  'more.colorThemeLabel': 'Color theme',
  'more.dataSourceTitle': 'Data source',
  'more.storageLabel': 'Storage',
  'more.storagePersistent': 'Permanent (survives a restart)',
  'more.storageEphemeral': 'This session only',
  'more.lastImportLabel': 'Last import',
  'more.scopeLabel': 'Scope',
  'more.scopeCurated': 'Curated',
  'more.scopeFull': 'Full',
  'more.rowsImportedLabel': 'Rows imported',
  'more.reimportButton': 'Import a new export',
  'more.aboutTitle': 'About GoogleHealthLens',
  'more.aboutText':
    'A local analytics app for your Google Takeout export (Google Health/Fitbit). All data stays on this machine — nothing is sent to any server outside your own backend.',
}

export default en
