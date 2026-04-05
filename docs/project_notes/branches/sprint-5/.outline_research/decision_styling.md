# Decision Research: Styling Approach

## CSS Modules in Vite (Recommended)
- Scoped by default via Vite's built-in PostCSS pipeline
- Zero runtime cost, no new dependencies
- Full control over custom game aesthetic (armor/leather/metal)
- CSS custom properties for theming (--color-leather, --color-metal, etc.)
- Stamina bar's 5 color states handled via data-attribute selectors — no JS style injection

## Vanilla CSS + Custom Properties
- Same as above but without component scoping
- Fine for small component count but doesn't scale as well

## Tailwind CSS
- Poor fit: utility classes fight the custom game aesthetic
- Arbitrary value overrides dominate ([background:url(...)], custom gradients)
- Overhead rather than speedup for this use case

## Styled Components / Emotion
- Unnecessary runtime cost and dependency
- Solves dynamic styling problem that CSS attribute selectors already handle
- 5-state stamina color doesn't need JS-driven styling
