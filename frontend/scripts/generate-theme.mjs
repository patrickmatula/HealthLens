// Regenerates src/theme/colors.generated.css from a set of named seed colors, using Google's own
// material-color-utilities (the HCT color-science library behind Material Theme Builder) per the
// official Material 3 color system (m3.material.io/styles/color/system/overview).
// Run with: node scripts/generate-theme.mjs
import { argbFromHex, hexFromArgb, Hct, SchemeExpressive, SchemeFidelity, SchemeMonochrome } from '@material/material-color-utilities'
import { writeFileSync } from 'node:fs'

// Each theme gets a full light+dark M3 palette (primary/secondary/tertiary/error, each with its own
// container and "on" pairs, plus the neutral/neutral-variant surface roles). The default theme
// (isDefault: true) also applies when no [data-color-theme] attribute is set yet (first paint, before
// ThemeContext mounts).
//
// The 4 named color themes use the "Fidelity" scheme variant, not "Expressive": Fidelity keeps the
// primary color close to the literal seed hue (M3 docs: "the resulting color palettes match the seed
// color, even if the seed color is very bright"), whereas Expressive deliberately rotates the primary
// hue away from the seed "for variety" -- which is exactly backwards for a theme picker where the name
// ("Orange") needs to visibly match what you get. Verified empirically: the same 4 seeds under
// Expressive previously produced an orange-brown teal, a purple-shifted sand, etc.; under Fidelity every
// theme's primary now falls in the same hue family as its name.
//
// "white" is the deliberately neutral theme and uses "Monochrome" (M3 docs: "all colors are grayscale,
// no chroma") rather than Fidelity or Expressive -- both of those keep some hue in the accent roles
// (primary/secondary/tertiary) even from a gray seed, which showed up as a violet accent throughout the
// UI (selected nav item, selected segmented button, etc. all read from secondary) even though the
// surfaces themselves were neutral. Monochrome zeroes chroma everywhere, so primary lands on literal
// black (light mode) / white (dark mode) and every other role is a true gray in between.
const THEMES = [
  { key: 'teal', label: 'Teal', seed: '#12876F', isDefault: true, variant: 'fidelity' },
  { key: 'blue', label: 'Blau', seed: '#1565C0', variant: 'fidelity' },
  { key: 'violet', label: 'Violett', seed: '#7B1FA2', variant: 'fidelity' },
  { key: 'orange', label: 'Orange', seed: '#E65100', variant: 'fidelity' },
  { key: 'white', label: 'Weiß', seed: '#787878', variant: 'monochrome' },
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

const VARIANT_CLASSES = { fidelity: SchemeFidelity, monochrome: SchemeMonochrome }

let css = `/* GENERATED FILE — do not hand-edit. Regenerate with: node scripts/generate-theme.mjs */
/* Material 3 color schemes via @material/material-color-utilities, one per named color theme. */
`

for (const { key, seed, isDefault, variant } of THEMES) {
  const sourceHct = Hct.fromInt(argbFromHex(seed))
  const SchemeClass = VARIANT_CLASSES[variant] ?? SchemeExpressive
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
