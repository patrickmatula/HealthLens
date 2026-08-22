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
// "white" additionally sets trueNeutralSurfaces: SchemeExpressive's own "neutral"/"neutral-variant"
// palettes always carry a small fixed chroma no matter how gray the seed is (a plain achromatic seed
// still produced a visibly tinted #f3fbff background), so surface-ish roles are recomputed at chroma 0
// directly via Hct.from(0, 0, tone) instead -- same tone each role would have gotten, just genuinely
// R=G=B this time -- while primary/secondary/tertiary are left as the normal Expressive derivation, so
// charts and accents stay colorful against the now truly neutral surface.
const THEMES = [
  { key: 'teal', label: 'Teal', seed: '#12876F', isDefault: true },
  { key: 'blue', label: 'Blau', seed: '#1565C0' },
  { key: 'purple', label: 'Violett', seed: '#7B1FA2' },
  { key: 'amber', label: 'Bernstein', seed: '#E65100' },
  { key: 'sand', label: 'Sand', seed: '#9C8B73', variant: 'neutral' },
  { key: 'white', label: 'Weiß', seed: '#787878', trueNeutralSurfaces: true },
]

const NEUTRAL_ROLES = new Set([
  'background', 'onBackground',
  'surface', 'onSurface', 'surfaceVariant', 'onSurfaceVariant',
  'surfaceDim', 'surfaceBright',
  'surfaceContainerLowest', 'surfaceContainerLow', 'surfaceContainer', 'surfaceContainerHigh', 'surfaceContainerHighest',
  'outline', 'outlineVariant',
  'inverseSurface', 'inverseOnSurface',
])

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

function block(scheme, { trueNeutralSurfaces } = {}) {
  return ROLES.map((role) => {
    let argb = scheme[role]
    if (trueNeutralSurfaces && NEUTRAL_ROLES.has(role)) {
      argb = Hct.from(0, 0, Hct.fromInt(argb).tone).toInt()
    }
    return `  --md-sys-color-${kebab(role)}: ${hexFromArgb(argb)};`
  }).join('\n')
}

let css = `/* GENERATED FILE — do not hand-edit. Regenerate with: node scripts/generate-theme.mjs */
/* Material 3 Expressive schemes via @material/material-color-utilities, one per named color theme. */
`

for (const { key, seed, isDefault, variant, trueNeutralSurfaces } of THEMES) {
  const sourceHct = Hct.fromInt(argbFromHex(seed))
  const SchemeClass = variant === 'neutral' ? SchemeNeutral : SchemeExpressive
  const light = new SchemeClass(sourceHct, false, 0)
  const dark = new SchemeClass(sourceHct, true, 0)
  const blockOpts = { trueNeutralSurfaces }

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
${block(light, blockOpts)}
}

@media (prefers-color-scheme: dark) {
  ${darkMediaSelector} {
${block(dark, blockOpts)}
  }
}

${darkExplicitSelector} {
${block(dark, blockOpts)}
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
