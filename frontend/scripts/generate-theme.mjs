// Regenerates src/theme/colors.generated.css from a set of named seed colors, using Google's own
// material-color-utilities (the HCT color-science library behind Material Theme Builder).
// Run with: node scripts/generate-theme.mjs
import { argbFromHex, hexFromArgb, Hct, SchemeExpressive, SchemeNeutral } from '@material/material-color-utilities'
import { writeFileSync } from 'node:fs'

// Each theme gets a full light+dark M3 palette. The default theme (isDefault: true) also applies when
// no [data-color-theme] attribute is set yet (first paint, before ThemeContext mounts). Themes use the
// "Expressive" scheme variant by default (vivid, hue-shifted accents even from a muted seed -- that's
// the point of Expressive). "sand" opts into "neutral" instead: SchemeNeutral keeps accent colors close
// to the seed's own low-chroma hue rather than shifting them, which is what makes it read as a calm,
// neutral/beige theme instead of just another vivid accent color with beige-ish surfaces.
const THEMES = [
  { key: 'teal', label: 'Teal', seed: '#12876F', isDefault: true },
  { key: 'blue', label: 'Blau', seed: '#1565C0' },
  { key: 'purple', label: 'Violett', seed: '#7B1FA2' },
  { key: 'amber', label: 'Bernstein', seed: '#E65100' },
  { key: 'sand', label: 'Sand', seed: '#9C8B73', variant: 'neutral' },
]

const ROLES = [
  'primary', 'onPrimary', 'primaryContainer', 'onPrimaryContainer',
  'secondary', 'onSecondary', 'secondaryContainer', 'onSecondaryContainer',
  'tertiary', 'onTertiary', 'tertiaryContainer', 'onTertiaryContainer',
  'error', 'onError', 'errorContainer', 'onErrorContainer',
  'background', 'onBackground',
  'surface', 'onSurface', 'surfaceVariant', 'onSurfaceVariant',
  'surfaceDim', 'surfaceBright',
  'surfaceContainerLowest', 'surfaceContainerLow', 'surfaceContainer', 'surfaceContainerHigh', 'surfaceContainerHighest',
  'outline', 'outlineVariant',
  'inverseSurface', 'inverseOnSurface', 'inversePrimary',
  'scrim', 'shadow', 'surfaceTint',
]

function kebab(role) {
  return role.replace(/([a-z0-9])([A-Z])/g, '$1-$2').toLowerCase()
}

function block(scheme) {
  return ROLES.map((role) => `  --md-sys-color-${kebab(role)}: ${hexFromArgb(scheme[role])};`).join('\n')
}

let css = `/* GENERATED FILE — do not hand-edit. Regenerate with: node scripts/generate-theme.mjs */
/* Material 3 Expressive schemes via @material/material-color-utilities, one per named color theme. */
`

for (const { key, seed, isDefault, variant } of THEMES) {
  const sourceHct = Hct.fromInt(argbFromHex(seed))
  const SchemeClass = variant === 'neutral' ? SchemeNeutral : SchemeExpressive
  const light = new SchemeClass(sourceHct, false, 0)
  const dark = new SchemeClass(sourceHct, true, 0)

  const lightSelector = isDefault ? `:root, :root[data-color-theme='${key}']` : `:root[data-color-theme='${key}']`
  const darkMediaSelector = isDefault
    ? `:root:not([data-theme='light']), :root[data-color-theme='${key}']:not([data-theme='light'])`
    : `:root[data-color-theme='${key}']:not([data-theme='light'])`
  const darkExplicitSelector = isDefault
    ? `:root[data-theme='dark'], :root[data-color-theme='${key}'][data-theme='dark']`
    : `:root[data-color-theme='${key}'][data-theme='dark']`

  css += `
/* Theme: ${key} (seed ${seed}) */
${lightSelector} {
${block(light)}
}

@media (prefers-color-scheme: dark) {
  ${darkMediaSelector} {
${block(dark)}
  }
}

${darkExplicitSelector} {
${block(dark)}
}
`
}

writeFileSync(new URL('../src/theme/colors.generated.css', import.meta.url), css)
writeFileSync(
  new URL('../src/theme/themes.generated.ts', import.meta.url),
  `// GENERATED FILE — do not hand-edit. Regenerate with: node scripts/generate-theme.mjs\nexport const COLOR_THEMES = ${JSON.stringify(
    THEMES.map(({ key, label, seed }) => ({ key, label, seed })),
  )} as const\nexport type ColorThemeKey = (typeof COLOR_THEMES)[number]['key']\n`,
)
console.log(`Wrote src/theme/colors.generated.css and themes.generated.ts (${THEMES.length} themes)`)
