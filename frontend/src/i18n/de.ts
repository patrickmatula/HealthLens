// German dictionary — the source of truth for keys (this app started German-only).
// Scaffold covers the app "shell" (nav, page titles, upload, settings) first;
// per-page content strings are translated incrementally — see project memory
// "project-googlehealthlens-backlog" for the remaining file list.
const de = {
  'nav.mainLabel': 'Hauptnavigation',
  'nav.dashboard': 'Übersicht',
  'nav.workouts': 'Workouts',
  'nav.sleep': 'Schlaf',
  'nav.heart': 'Herz',
  'nav.recovery': 'Erholung',
  'nav.more': 'Mehr',

  'theme.system': 'Design: System — klicken für Hell',
  'theme.light': 'Design: Hell — klicken für Dunkel',
  'theme.dark': 'Design: Dunkel — klicken für System',

  'upload.title': 'GoogleHealthLens',
  'upload.subtitle': 'Lade deinen Google-Takeout-Export (Google Health) als .zip hoch, um deine Fitbit/Health-Daten zu erkunden.',
  'upload.dropzoneCta': 'Zip-Datei auswählen…',
  'upload.persistentTitle': 'Dauerhaft speichern',
  'upload.persistentHintOn': 'Daten werden in eine lokale SQLite-Datenbank geschrieben und bleiben nach einem Neustart erhalten.',
  'upload.persistentHintOff': 'Daten werden nur für diese Sitzung geladen — nach einem Neustart der App sind sie wieder weg.',
  'upload.scopeGroupLabel': 'Datenumfang',
  'upload.scopeCuratedTitle': 'Kuratiert (empfohlen)',
  'upload.scopeCuratedHint':
    'Zusammenfassungen vollständig, hochfrequente Rohdaten (Herzfrequenz u.a.) aggregiert — außer bei Workouts. Schneller Import.',
  'upload.scopeFullTitle': 'Vollständig, alles roh',
  'upload.scopeFullHint': 'Jede Zeile 1:1 importiert. Größere Datenbank, deutlich längerer Import.',
  'upload.startButton': 'Import starten',
  'upload.backButton': '← Zurück',
  'upload.dashboardButton': 'Zum Dashboard →',
  'upload.progressRows': '{count} Zeilen importiert',
  'upload.errorGeneric': 'Import konnte nicht gestartet werden.',
  'upload.errorFailed': 'Import fehlgeschlagen.',
  'upload.errorConnection': 'Verbindung zum Server verloren.',

  'more.settingsTitle': 'Einstellungen',
  'more.unitsLabel': 'Einheiten',
  'more.unitMetric': 'Metrisch (km)',
  'more.unitImperial': 'Imperial (mi)',
  'more.languageLabel': 'Sprache',
  'more.colorThemeLabel': 'Farbthema',
  'more.dataSourceTitle': 'Datenquelle',
  'more.storageLabel': 'Speicherung',
  'more.storagePersistent': 'Dauerhaft (übersteht einen Neustart)',
  'more.storageEphemeral': 'Nur für diese Sitzung',
  'more.lastImportLabel': 'Letzter Import',
  'more.scopeLabel': 'Umfang',
  'more.scopeCurated': 'Kuratiert',
  'more.scopeFull': 'Vollständig',
  'more.rowsImportedLabel': 'Importierte Zeilen',
  'more.reimportButton': 'Neuen Export importieren',
  'more.aboutTitle': 'Über GoogleHealthLens',
  'more.aboutText':
    'Eine lokale Auswertungs-App für deinen Google-Takeout-Export (Google Health/Fitbit). Alle Daten bleiben auf diesem Rechner — es wird nichts an einen Server außerhalb deines eigenen Backends gesendet.',
} as const

export default de
export type TranslationKey = keyof typeof de
