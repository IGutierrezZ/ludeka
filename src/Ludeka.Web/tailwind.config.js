/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./Components/**/*.razor",
    "./wwwroot/**/*.html"
  ],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        'brand-primary': 'var(--brand-primary)',
        'brand-primary-hover': 'var(--brand-primary-hover)',
        'brand-accent': 'var(--brand-accent)',
        'theme-main': 'var(--bg-main)',
        'theme-surface': 'var(--bg-surface)',
        'theme-surface-elevated': 'var(--bg-surface-elevated)',
        'theme-card': 'var(--bg-card)',
        'theme-nav': 'var(--bg-nav)',
        'theme-border': 'var(--border-subtle)',
        'theme-border-highlight': 'var(--border-highlight)',
        'theme-text': 'var(--text-primary)',
        'theme-text-secondary': 'var(--text-secondary)',
        'theme-text-muted': 'var(--text-muted)',
        'ludeka-bg': '#121418',
        'ludeka-surface': '#181B21',
        'ludeka-card': '#1C1F27',
        'ludeka-border': 'rgba(255, 255, 255, 0.08)',
        'mustplay': '#10B981',
        'recommended': '#F59E0B',
        'notrec': '#F43F5E',
        // INC-113: Tokens canónicos de Revista Lúdica y paleta editorial
        paper: 'var(--paper)',
        'paper-2': 'var(--paper-2)',
        card: 'var(--card)',
        ink: 'var(--ink)',
        muted: 'var(--muted)',
        line: 'var(--line)',
        rule: 'var(--rule)',
        brand: 'var(--brand)',
        'on-brand': 'var(--on-brand)',
        accent: 'var(--accent)',
        mustard: 'var(--mustard)',
        'on-mustard': 'var(--on-mustard)',
        green: 'var(--green)',
        'on-green': 'var(--on-green)',
        'green-soft': 'var(--green-soft)',
        'accent-green': 'var(--accent-green)',
        sky: 'var(--sky)',
        'blue-band': 'var(--blue-band)',
        'on-blue': 'var(--on-blue)',
        'blue-soft': 'var(--blue-soft)',
        plum: 'var(--plum)',
        'plum-soft': 'var(--plum-soft)',
        silver: 'var(--silver)',
        bronze: 'var(--bronze)',
        pink: 'var(--pink)',
        inverse: 'var(--inverse)',
        'on-inverse': 'var(--on-inverse)',
        ticker: 'var(--ticker)',
        'p-blue': 'var(--p-blue)',
        'p-pink': 'var(--p-pink)',
        'p-peach': 'var(--p-peach)',
        'p-mint': 'var(--p-mint)',
        alert: 'var(--alert)',
        terracota: 'var(--color-terracota)',
        salvia: 'var(--color-salvia)',
        arena: 'var(--color-arena)',
        'arena-light': 'var(--color-arena-light)',
        tinta: 'var(--color-tinta)',
        'semantic-verde': 'var(--semantic-verde)',
        'semantic-ambar': 'var(--semantic-ambar)',
        'semantic-carmin': 'var(--semantic-carmin)',
        slate: {
          700: '#2A2E39',
          800: '#1F232D',
          900: '#16181F',
          950: '#111317',
        },
        orange: {
          400: '#E77457',
          500: '#E05A38',
          600: '#C84727',
          900: '#3D1B14',
          950: '#26110D',
        }
      },
      fontFamily: {
        sans: ['"Plus Jakarta Sans"', 'system-ui', '-apple-system', 'sans-serif'],
        mono: ['"JetBrains Mono"', 'monospace']
      }
    }
  },
  safelist: [
    'status-mustplay',
    'status-recommended',
    'status-notrecommended',
    'active-pill',
    'aspect-square',
    'aspect-video',
    'skip-link',
    {
      pattern: /(bg|text|border)-(emerald|amber|rose|purple|cyan|slate|orange|pink|blue)-(500|600|700|800|900|950)(\/\d+)?/
    }
  ],
  plugins: []
};
