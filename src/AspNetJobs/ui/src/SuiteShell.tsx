import type { ReactNode } from 'react';

interface Panel { name: string; route: string; icon: string; }
interface Nav { current: string; panels: Panel[]; }

// Server injects the installed tools into <script type="application/json" id="__suite_nav__">.
// Unreplaced token or a solo install → no sidebar, the tool looks standalone.
function readNav(): Nav {
  try {
    const el = document.getElementById('__suite_nav__');
    if (el?.textContent) return JSON.parse(el.textContent);
  } catch { /* not in a suite */ }
  return { current: '', panels: [] };
}

const nav = readNav();

const brand = `<svg width="16" height="16" viewBox="0 0 16 16" fill="none"><rect x="1" y="1" width="14" height="14" rx="3.5" stroke="var(--accent)" stroke-width="1.5"/><path d="M4.5 8h2l1.5-3.5L10 11l1.5-3" stroke="var(--accent)" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>`;

export default function SuiteShell({ current, children }: { current: string; children: ReactNode }) {
  if (nav.panels.length <= 1) return <>{children}</>;
  return (
    <div className="shell">
      <nav className="sidebar">
        <div className="brand"><span dangerouslySetInnerHTML={{ __html: brand }} />suite</div>
        <div className="nav-group">Tools</div>
        {nav.panels.map((p) => (
          <a key={p.route} href={p.route} className={`nav-item${p.route === current ? ' active' : ''}`} style={{ textDecoration: 'none' }}>
            <span dangerouslySetInnerHTML={{ __html: p.icon }} />
            {p.name}
          </a>
        ))}
      </nav>
      {children}
    </div>
  );
}
