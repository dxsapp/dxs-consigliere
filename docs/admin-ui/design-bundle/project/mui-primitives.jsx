// mui-primitives.jsx — visual mocks of the MUI primitives used in this design.
// Each component matches MUI's real sizing & visual behaviour so engineers can map
// the mockup 1-to-1 to <Card> / <DataGrid> / <Stepper> / <Chip> in code.

// ─── ThemeContext ───────────────────────────────────────────────────────────
const ThemeCtx = React.createContext({ p: ConsigliereThemeConfig.palette.dark, t: ConsigliereThemeConfig.typography, density: 'comfortable' });

function useTheme() { return React.useContext(ThemeCtx); }

// `m` helper picks palette token by dotted path: m(p, 'severity.warning.main')
function m(p, path) { return path.split('.').reduce((a,k) => a && a[k], p); }

// Icon — Material Symbols Outlined (matches @mui/icons-material visual language)
function Icon({ name, size = 20, color, sx = {} }) {
  return (
    <span className="material-symbols-outlined" style={{
      fontSize: size, lineHeight: 1, color: color || 'inherit',
      fontVariationSettings: '"FILL" 0,"wght" 400,"GRAD" 0,"opsz" 24',
      userSelect: 'none', ...sx,
    }}>{name}</span>
  );
}

// ─── Typography ─────────────────────────────────────────────────────────────
function Typography({ variant = 'body1', color, sx = {}, children, component, noWrap, ...rest }) {
  const { t, p } = useTheme();
  const v = t[variant] || t.body1;
  const Tag = component || (variant.match(/^h[1-6]$/) ? variant : variant === 'overline' || variant === 'caption' ? 'span' : 'p');
  const c = color
    ? (color.includes('.') ? m(p, color) : (p.text && p.text[color]) || color)
    : p.text.primary;
  return (
    <Tag style={{
      margin: 0, fontFamily: v.fontFamily || t.fontFamily,
      fontSize: v.fontSize, fontWeight: v.fontWeight, lineHeight: v.lineHeight,
      letterSpacing: v.letterSpacing, textTransform: v.textTransform,
      color: c, ...(noWrap ? { whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis' } : {}),
      ...sx,
    }} {...rest}>{children}</Tag>
  );
}

// ─── Card ───────────────────────────────────────────────────────────────────
function Card({ variant = 'outlined', children, sx = {}, stale = false, onClick }) {
  const { p } = useTheme();
  const border = variant === 'outlined' ? `1px solid ${p.divider}` : 'none';
  const shadow = variant === 'outlined' ? 'none'
    : `0 1px 2px 0 ${p.mode==='dark'?'rgba(0,0,0,0.4)':'rgba(0,0,0,0.06)'}, 0 1px 3px 0 ${p.mode==='dark'?'rgba(0,0,0,0.3)':'rgba(0,0,0,0.08)'}`;
  return (
    <div onClick={onClick} style={{
      background: p.background.paper, border, boxShadow: shadow,
      borderRadius: 8, overflow: 'hidden', position: 'relative',
      cursor: onClick ? 'pointer' : 'default', ...sx,
    }}>
      {children}
      {stale && (
        <>
          <div style={{ position:'absolute', inset:0, background: p.background.paper, opacity: 0.4, pointerEvents:'none' }} />
          <div style={{ position:'absolute', top:8, right:8, zIndex:2 }}>
            <Chip label="stale" size="small" color="warning" variant="outlined" icon="warning" />
          </div>
        </>
      )}
    </div>
  );
}
function CardHeader({ title, subheader, action, avatar, sx = {} }) {
  return (
    <div style={{ display:'flex', alignItems:'center', padding:'16px 16px 8px 16px', gap:12, ...sx }}>
      {avatar}
      <div style={{ flex:1, minWidth:0 }}>
        {typeof title === 'string' ? <Typography variant="h6" sx={{ fontSize:'0.9375rem', fontWeight:600 }}>{title}</Typography> : title}
        {subheader && (typeof subheader === 'string'
          ? <Typography variant="caption" color="secondary">{subheader}</Typography>
          : subheader)}
      </div>
      {action}
    </div>
  );
}
function CardContent({ children, sx = {} }) {
  return <div style={{ padding:'8px 16px 16px 16px', ...sx }}>{children}</div>;
}

// ─── Chip ───────────────────────────────────────────────────────────────────
// `color` accepts 'default'|'primary'|'severity.warning'|... or any palette dotted path.
function Chip({ label, color = 'default', variant = 'filled', size = 'small', icon, onDelete, sx = {} }) {
  const { p } = useTheme();
  let bg, fg, br;
  if (color === 'default') {
    bg = p.action.hover; fg = p.text.primary; br = p.divider;
  } else {
    const token = color.includes('.') ? m(p, color) : (m(p, `severity.${color}`) || p[color]);
    const main = token?.main || token;
    const bgTok = token?.bg;
    bg = variant === 'outlined' ? 'transparent' : (bgTok || (p.mode==='dark' ? main+'22' : main+'15'));
    fg = variant === 'outlined' ? main : (p.mode==='dark' ? main : main);
    br = main;
  }
  const h = size === 'small' ? 22 : 30;
  const fs = size === 'small' ? '0.75rem' : '0.8125rem';
  return (
    <span style={{
      display:'inline-flex', alignItems:'center', gap: icon ? 4 : 0,
      height: h, padding: icon ? '0 8px 0 6px' : '0 9px',
      background: bg, color: fg, fontSize: fs, fontWeight: 500,
      borderRadius: 16, border: variant === 'outlined' ? `1px solid ${br}` : '1px solid transparent',
      letterSpacing: '0.16px', whiteSpace:'nowrap', ...sx,
    }}>
      {icon && <Icon name={icon} size={size === 'small' ? 14 : 16} />}
      {label}
      {onDelete && <Icon name="close" size={14} sx={{ marginLeft:2, opacity:0.7 }} />}
    </span>
  );
}

// ─── Button & IconButton ────────────────────────────────────────────────────
function Button({ children, variant = 'text', color = 'primary', size = 'medium', startIcon, endIcon, sx = {}, fullWidth, disabled }) {
  const { p } = useTheme();
  const tok = (color === 'inherit') ? { main: 'currentColor' } : (m(p, `severity.${color}`) || p[color] || p.primary);
  const h = size === 'small' ? 30 : size === 'large' ? 42 : 36;
  const pad = size === 'small' ? '4px 10px' : size === 'large' ? '8px 22px' : '6px 16px';
  let bg, fg, br;
  if (variant === 'contained') { bg = tok.main; fg = tok.contrastText || '#FFF'; br = 'none'; }
  else if (variant === 'outlined') { bg = 'transparent'; fg = tok.main; br = `1px solid ${tok.main}80`; }
  else { bg = 'transparent'; fg = tok.main; br = 'none'; }
  return (
    <button disabled={disabled} style={{
      display:'inline-flex', alignItems:'center', justifyContent:'center', gap:8,
      height: h, padding: pad, fontFamily:'inherit', fontWeight: 500,
      fontSize: size === 'small' ? '0.8125rem' : '0.875rem',
      letterSpacing: '0.4px', textTransform:'uppercase',
      background: bg, color: fg, border: br, borderRadius: 6,
      width: fullWidth ? '100%' : 'auto',
      cursor: disabled ? 'not-allowed' : 'pointer', opacity: disabled ? 0.5 : 1,
      ...sx,
    }}>
      {startIcon} {children} {endIcon}
    </button>
  );
}
function IconButton({ icon, name, size = 'medium', color, onClick, sx = {}, edge, badge }) {
  const { p } = useTheme();
  const s = size === 'small' ? 30 : size === 'large' ? 48 : 40;
  const is = size === 'small' ? 18 : size === 'large' ? 28 : 22;
  return (
    <button onClick={onClick} style={{
      width: s, height: s, borderRadius: '50%', background:'transparent', border:'none',
      color: color || p.text.secondary, cursor:'pointer', display:'inline-flex',
      alignItems:'center', justifyContent:'center', position:'relative',
      marginLeft: edge === 'start' ? -8 : 0, marginRight: edge === 'end' ? -8 : 0, ...sx,
    }}>
      <Icon name={icon || name} size={is} />
      {badge != null && (
        <span style={{
          position:'absolute', top:4, right:4, minWidth:18, height:18, padding:'0 4px',
          borderRadius:9, background: p.severity.error.main, color: '#FFF',
          fontSize:'0.625rem', fontWeight:700, display:'flex', alignItems:'center', justifyContent:'center',
        }}>{badge}</span>
      )}
    </button>
  );
}

// ─── List / ListItem ────────────────────────────────────────────────────────
function List({ children, sx = {}, dense }) {
  return <ul style={{ listStyle:'none', margin:0, padding: dense ? '4px 0' : '8px 0', ...sx }}>{children}</ul>;
}
function ListItemButton({ children, selected, sx = {}, onClick, dense, indent = 0 }) {
  const { p } = useTheme();
  return (
    <li onClick={onClick} style={{
      display:'flex', alignItems:'center', gap:12,
      padding: dense ? `4px 16px 4px ${16+indent}px` : `8px 16px 8px ${16+indent}px`,
      margin:'0 8px', borderRadius: 8, cursor:'pointer',
      background: selected ? p.action.selected : 'transparent',
      color: selected ? p.primary.main : p.text.primary,
      fontWeight: selected ? 500 : 400,
      minHeight: dense ? 32 : 40, ...sx,
    }}>
      {children}
    </li>
  );
}

// ─── Divider ────────────────────────────────────────────────────────────────
function Divider({ sx = {}, orientation = 'horizontal', children }) {
  const { p } = useTheme();
  if (children) {
    return (
      <div style={{ display:'flex', alignItems:'center', gap:12, padding:'4px 0', ...sx }}>
        <div style={{ flex:1, height:1, background: p.divider }} />
        <Typography variant="overline" color="secondary" sx={{ fontSize:'0.6875rem' }}>{children}</Typography>
        <div style={{ flex:1, height:1, background: p.divider }} />
      </div>
    );
  }
  return orientation === 'vertical'
    ? <div style={{ width:1, alignSelf:'stretch', background: p.divider, ...sx }} />
    : <hr style={{ border:'none', borderTop:`1px solid ${p.divider}`, margin:0, ...sx }} />;
}

// ─── Stack ──────────────────────────────────────────────────────────────────
function Stack({ children, direction = 'column', spacing = 0, alignItems, justifyContent, sx = {}, divider, flex, wrap }) {
  const gap = typeof spacing === 'number' ? spacing * 8 : spacing;
  const arr = React.Children.toArray(children).filter(Boolean);
  const out = [];
  arr.forEach((c, i) => {
    if (i > 0 && divider) out.push(<React.Fragment key={`d${i}`}>{divider}</React.Fragment>);
    out.push(c);
  });
  return (
    <div style={{
      display:'flex', flexDirection: direction, gap, alignItems, justifyContent,
      flex, flexWrap: wrap ? 'wrap' : 'nowrap', ...sx,
    }}>{out}</div>
  );
}

Object.assign(window, { ThemeCtx, useTheme, Icon, Typography, Card, CardHeader, CardContent, Chip, Button, IconButton, List, ListItemButton, Divider, Stack, mTok: m });
