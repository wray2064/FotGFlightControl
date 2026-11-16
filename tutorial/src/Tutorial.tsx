import React from 'react';
import {AbsoluteFill, Audio, Img, Sequence, interpolate, spring, staticFile, useCurrentFrame, useVideoConfig, Easing} from 'remotion';
import script from '../script.json';

const FPS = 30;
const PRE = 9; // narration starts 0.3 s into each chapter
const TAIL = 30;
const C = {bg: '#050504', panel: '#0b0b08', yellow: '#f0d04a', dim: '#9a8a3a', green: '#62e85a', red: '#ff5a44', cyan: '#7fe7e0'};
const FONT = 'Consolas, "Courier New", monospace';

type Box = {x: number; y: number; w: number; h: number; label: string; at?: number; below?: boolean};
type Focus = {x: number; y: number; w: number; h: number};
type Stage =
  | {kind: 'shot'; src: string; w: number; h: number; from: number; to: number; focus?: Focus; boxes?: Box[]}
  | {kind: 'diagram'; name: 'wheel' | 'encoder' | 'backup' | 'share' | 'calib'; from: number; to: number};

const S = (src: string, from: number, to: number, boxes?: Box[], focus?: Focus): Stage => ({kind: 'shot', src, w: 1140, h: 776, from, to, boxes, focus});
const D = (name: 'wheel' | 'encoder' | 'backup' | 'share' | 'calib', from: number, to: number): Stage => ({kind: 'diagram', name, from, to});
const dlg = (src: string, from: number, to: number, boxes?: Box[], focus?: Focus): Stage => ({kind: 'shot', src, w: 940, h: 724, from, to, boxes, focus});

const LIST: Focus = {x: 20, y: 240, w: 500, h: 460};
const PREVIEW: Focus = {x: 500, y: 240, w: 600, h: 420};
const BOTTOM: Focus = {x: 10, y: 600, w: 1110, h: 160};

const stages: Record<string, Stage[]> = {
  intro: [S('01-hotas-tab-overview.png', 0, 1)],
  advanced: [
    S('08-hotas-switched-off-in-simple.png', 0, 0.5, [
      {x: 834, y: 276, w: 266, h: 58, label: 'Controls: Simple | Advanced', at: 0.1, below: true},
      {x: 668, y: 282, w: 152, h: 54, label: 'HOTAS is greyed out', at: 0.45},
    ]),
    S('01-hotas-tab-overview.png', 0.5, 1, [
      {x: 966, y: 126, w: 136, h: 38, label: 'Advanced', at: 0.1},
      {x: 668, y: 54, w: 154, h: 56, label: 'HOTAS is available', at: 0.4},
    ]),
  ],
  scan: [
    S('01-hotas-tab-overview.png', 0, 0.5, [
      {x: 896, y: 180, w: 186, h: 58, label: 'Scan controllers', at: 0.08, below: true},
      {x: 512, y: 268, w: 508, h: 36, label: 'One chip per device, numbered if identical', at: 0.3},
    ]),
    S('04-second-controller.png', 0.5, 1, [{x: 512, y: 308, w: 572, h: 332, label: 'Live preview of every control', at: 0.15}], PREVIEW),
  ],
  assign: [
    S('02-listening-for-a-control.png', 0, 0.72, [
      {x: 40, y: 304, w: 458, h: 252, label: '1  Pick an action', at: 0.08},
      {x: 40, y: 634, w: 220, h: 48, label: '2  Press the control', at: 0.3},
      {x: 512, y: 308, w: 572, h: 332, label: 'or click it here', at: 0.55},
    ]),
    S('01-hotas-tab-overview.png', 0.72, 1, [{x: 40, y: 570, w: 458, h: 62, label: 'Conflicts are reported immediately', at: 0.2}], LIST),
  ],
  kinds: [
    S('05-game-action-on-a-hat.png', 0, 0.5, [{x: 40, y: 304, w: 458, h: 252, label: 'Game actions: written into the game', at: 0.1}], LIST),
    S('07-flight-meaning-of-a-button.png', 0.5, 1, [{x: 40, y: 304, w: 458, h: 252, label: 'Flight Control actions: wheels and gamepad features', at: 0.1}], LIST),
  ],
  wheels: [
    S('03-wheel-roles.png', 0, 0.3, [{x: 40, y: 308, w: 458, h: 92, label: 'Wheel select and cancel', at: 0.1}], LIST),
    D('wheel', 0.3, 1),
  ],
  encoder: [
    S('03-wheel-roles.png', 0, 0.3, [{x: 40, y: 344, w: 458, h: 38, label: 'Step clockwise / anticlockwise', at: 0.1}], LIST),
    D('encoder', 0.3, 1),
  ],
  axes: [
    dlg('10-axis-settings-game-mode.png', 0, 0.2, [{x: 290, y: 198, w: 630, h: 220, label: 'Game: one dead zone for every axis', at: 0.15}]),
    dlg('09-axis-settings-precision.png', 0.2, 0.5, [
      {x: 22, y: 74, w: 898, h: 38, label: 'Game | Precision', at: 0.05},
      {x: 288, y: 458, w: 620, h: 140, label: 'Dead zone, full deflection, curve, invert', at: 0.35},
      {x: 290, y: 606, w: 160, h: 82, label: 'Calibrate', at: 0.7},
    ]),
    D('calib', 0.5, 1),
  ],
  apply: [
    S('01-hotas-tab-overview.png', 0, 0.4, [
      {x: 926, y: 696, w: 174, h: 54, label: 'Apply to pilot', at: 0.1},
      {x: 272, y: 696, w: 136, h: 54, label: 'Restore backup', at: 0.4},
      {x: 532, y: 696, w: 136, h: 54, label: 'Gamepad mode', at: 0.7},
    ], BOTTOM),
    D('backup', 0.4, 1),
  ],
  share: [
    S('01-hotas-tab-overview.png', 0, 0.3, [
      {x: 24, y: 696, w: 126, h: 54, label: 'Open profile', at: 0.1},
      {x: 148, y: 696, w: 120, h: 54, label: 'Save profile', at: 0.4},
    ], BOTTOM),
    D('share', 0.3, 1),
  ],
  outro: [S('01-hotas-tab-overview.png', 0, 1, [{x: 940, y: 634, w: 144, h: 48, label: 'Control not listed?', at: 0.15}], PREVIEW)],
};

const chapters = script.map((c) => {
  const frames = Math.ceil((c.dur + 1) * FPS) + PRE + TAIL - 30;
  return {...c, frames: Math.max(frames, Math.ceil(c.dur * FPS) + PRE + TAIL)};
});
export const totalFrames = chapters.reduce((a, c) => a + c.frames, 0);

// ---------- helpers
const ease = Easing.bezier(0.22, 1, 0.36, 1);
const fade = (f: number, a: number, b: number) => interpolate(f, [a, b], [0, 1], {extrapolateLeft: 'clamp', extrapolateRight: 'clamp'});

const frame = (focus: Focus | undefined, w: number, h: number, W: number, H: number, t: number) => {
  const full = {x: 0, y: 0, w, h};
  const f = focus ?? full;
  const sFull = Math.min(W / w, H / h);
  const sFoc = Math.min(W / f.w, H / f.h) * 0.96;
  const s = sFull + (sFoc - sFull) * t;
  const cx = full.w / 2 + (f.x + f.w / 2 - full.w / 2) * t;
  const cy = full.h / 2 + (f.y + f.h / 2 - full.h / 2) * t;
  return {s, tx: W / 2 - cx * s, ty: H / 2 - cy * s};
};

const SW = 1760;
const SH = 700;

const Shot: React.FC<{st: Extract<Stage, {kind: 'shot'}>; f: number; len: number}> = ({st, f, len}) => {
  const zoomIn = st.focus ? ease(fade(f, 8, 40)) : 0;
  const drift = f / Math.max(len, 1); // slow drift so stills never feel frozen
  const base = frame(st.focus, st.w, st.h, SW, SH, zoomIn);
  const s = base.s * (1 + 0.025 * drift);
  const k = s / base.s;
  const tx = SW / 2 - (SW / 2 - base.tx) * k;
  const ty = SH / 2 - (SH / 2 - base.ty) * k;
  return (
    <div style={{position: 'relative', width: SW, height: SH, overflow: 'hidden', border: `2px solid ${C.dim}`, background: '#000', boxShadow: `0 0 40px rgba(240,208,74,0.12)`}}>
      <div style={{position: 'absolute', left: 0, top: 0, width: st.w, height: st.h, transformOrigin: '0 0', transform: `translate(${tx}px, ${ty}px) scale(${s})`}}>
        <Img src={staticFile('shots/' + st.src)} style={{width: st.w, height: st.h, display: 'block'}} />
        {st.boxes?.map((b, i) => {
          const at = Math.floor((b.at ?? 0) * len);
          const o = fade(f, at, at + 10);
          const pulse = 0.65 + 0.35 * Math.sin((f - at) / 6);
          const nextAt = st.boxes!.map((q) => Math.floor((q.at ?? 0) * len)).filter((v) => v > at).sort((a, c) => a - c)[0] ?? 1e9;
          const labelOn = f < nextAt;
          const lw = b.label.length * 14.6 + 26;
          const screenLeft = tx + b.x * s;
          const shiftPx = Math.max(10 - screenLeft, Math.min(0, SW - 10 - (screenLeft + lw)));
          const inv = 1 / s;
          return (
            <div key={i} style={{position: 'absolute', left: b.x, top: b.y, width: b.w, height: b.h, opacity: o * (labelOn ? 1 : 0.55)}}>
              <div style={{position: 'absolute', inset: -4 * inv, border: `${3 * inv}px solid ${C.green}`, borderRadius: 6 * inv, opacity: pulse, boxShadow: `0 0 ${14 * inv}px ${C.green}`}} />
              <div
                style={{
                  position: 'absolute', left: shiftPx * inv, display: labelOn ? 'block' : 'none', top: b.below ? b.h + 12 * inv : -44 * inv, transformOrigin: b.below ? '0 0' : '0 100%', transform: `scale(${inv})`,
                  background: C.green, color: '#021', fontFamily: FONT, fontWeight: 700, fontSize: 24, padding: '4px 12px', whiteSpace: 'nowrap',
                }}
              >
                {b.label}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
};

// ---------- diagrams
const Mono: React.FC<React.SVGProps<SVGTextElement> & {children: React.ReactNode}> = ({children, ...p}) => (
  <text fontFamily={FONT} {...p}>{children}</text>
);

const WheelGraphic: React.FC<{cx: number; cy: number; r: number; active: number; open: number}> = ({cx, cy, r, active, open}) => {
  const n = 6;
  return (
    <g opacity={open}>
      {Array.from({length: n}).map((_, i) => {
        const a0 = (i / n) * Math.PI * 2 - Math.PI / 2 - Math.PI / n + 0.04;
        const a1 = ((i + 1) / n) * Math.PI * 2 - Math.PI / 2 - Math.PI / n - 0.04;
        const p = (a: number, rr: number) => `${cx + Math.cos(a) * rr},${cy + Math.sin(a) * rr}`;
        const d = `M ${p(a0, r * 0.45)} L ${p(a0, r)} A ${r} ${r} 0 0 1 ${p(a1, r)} L ${p(a1, r * 0.45)} A ${r * 0.45} ${r * 0.45} 0 0 0 ${p(a0, r * 0.45)} Z`;
        const on = i === active;
        return <path key={i} d={d} fill={on ? C.yellow : '#14120a'} stroke={on ? C.yellow : C.dim} strokeWidth={3} />;
      })}
    </g>
  );
};

const Button: React.FC<{x: number; y: number; label: string; down: boolean}> = ({x, y, label, down}) => (
  <g>
    <circle cx={x} cy={y} r={58} fill={down ? C.green : '#5a1a10'} stroke={C.cyan} strokeWidth={4} />
    <Mono x={x} y={y + 12} textAnchor="middle" fontSize={34} fontWeight={700} fill={down ? '#021' : C.yellow}>7</Mono>
    <Mono x={x} y={y + 100} textAnchor="middle" fontSize={26} fill={C.dim}>{label}</Mono>
  </g>
);

const DiagramWheel: React.FC<{f: number; len: number}> = ({f, len}) => {
  const p = f / len;
  const open = p > 0.5;
  const wOpen = ease(fade(p, 0.48, 0.58));
  const press = (p > 0.18 && p < 0.34) || (p > 0.7 && p < 0.86);
  const guard = p > 0.5 && p < 0.62;
  return (
    <svg width={SW} height={SH} viewBox={`0 0 ${SW} ${SH}`}>
      <rect width={SW} height={SH} fill={C.panel} />
      <Mono x={80} y={80} fontSize={34} fill={C.yellow} fontWeight={700}>ONE BUTTON, TWO STATES</Mono>
      <Button x={300} y={340} label="Logitech / Button 7" down={press} />
      <line x1={380} y1={340} x2={760} y2={340} stroke={C.dim} strokeWidth={3} strokeDasharray="10 8" />
      <polygon points="760,340 738,326 738,354" fill={C.dim} />
      <g opacity={open ? 0.25 : 1}>
        <rect x={820} y={240} width={330} height={200} rx={8} fill="#14120a" stroke={C.yellow} strokeWidth={3} />
        <Mono x={985} y={310} textAnchor="middle" fontSize={34} fontWeight={700} fill={C.yellow}>IN FLIGHT</Mono>
        <Mono x={985} y={365} textAnchor="middle" fontSize={44} fontWeight={700} fill={press && !open ? C.green : C.yellow}>FIRE</Mono>
      </g>
      <g opacity={open ? 1 : 0.15}>
        <WheelGraphic cx={1430} cy={340} r={190} active={press && open ? 2 : 1} open={Math.max(wOpen, 0.3)} />
        <Mono x={1430} y={332} textAnchor="middle" fontSize={26} fontWeight={700} fill={C.yellow}>WHEEL</Mono>
        <Mono x={1430} y={364} textAnchor="middle" fontSize={26} fontWeight={700} fill={press && open ? C.green : C.yellow}>{press && open ? 'SELECT' : 'OPEN'}</Mono>
      </g>
      <Mono x={80} y={560} fontSize={30} fill={C.yellow}>Wheel select / cancel only act while a wheel is open.</Mono>
      <Mono x={80} y={612} fontSize={30} fill={guard ? C.green : C.dim}>
        {guard ? 'A button held while the wheel opens is ignored until you let go.' : 'Release guard: nothing fires by accident.'}
      </Mono>
    </svg>
  );
};

const DiagramEncoder: React.FC<{f: number; len: number}> = ({f, len}) => {
  const p = f / len;
  const steps = Math.min(5, Math.max(0, Math.floor((p - 0.12) / 0.11)));
  const dir = p > 0.12 && p < 0.7 ? 1 : 0;
  const confirm = p > 0.78;
  const active = (steps % 6 + 6) % 6;
  const knobAngle = steps * 20 + interpolate(((p - 0.12) / 0.11) % 1, [0, 1], [0, 20], {extrapolateRight: 'clamp'}) * (dir ? 1 : 0);
  return (
    <svg width={SW} height={SH} viewBox={`0 0 ${SW} ${SH}`}>
      <rect width={SW} height={SH} fill={C.panel} />
      <Mono x={80} y={80} fontSize={34} fill={C.yellow} fontWeight={700}>ENCODER: CLICK BY CLICK</Mono>
      <g transform={`translate(330 360)`}>
        <circle r={120} fill="#14120a" stroke={C.yellow} strokeWidth={4} />
        <g transform={`rotate(${knobAngle})`}>
          <circle r={96} fill="#0b0b08" stroke={C.dim} strokeWidth={3} />
          <rect x={-6} y={-96} width={12} height={46} fill={C.yellow} />
        </g>
        <Mono y={175} textAnchor="middle" fontSize={26} fill={C.dim}>rotary encoder</Mono>
        <Mono x={-170} y={-130} textAnchor="middle" fontSize={26} fill={C.cyan}>{'↺'} Step anticlockwise</Mono>
        <Mono x={170} y={-130} textAnchor="middle" fontSize={26} fill={C.green}>Step clockwise {'↻'}</Mono>
      </g>
      <line x1={520} y1={360} x2={900} y2={360} stroke={C.dim} strokeWidth={3} strokeDasharray="10 8" />
      <polygon points="900,360 878,346 878,374" fill={C.dim} />
      <WheelGraphic cx={1250} cy={360} r={230} active={active} open={1} />
      <Mono x={1250} y={355} textAnchor="middle" fontSize={30} fontWeight={700} fill={C.yellow}>SLOT {active + 1}</Mono>
      <Mono x={1250} y={392} textAnchor="middle" fontSize={26} fill={confirm ? C.green : C.dim}>{confirm ? 'Wheel select: confirmed' : 'each click = one slot'}</Mono>
      <Mono x={80} y={650} fontSize={30} fill={C.yellow}>Any encoder that appears as two buttons works.</Mono>
    </svg>
  );
};

const DiagramBackup: React.FC<{f: number; len: number}> = ({f, len}) => {
  const p = f / len;
  const Node: React.FC<{x: number; label: string; sub: string; on: boolean; col?: string}> = ({x, label, sub, on, col}) => (
    <g opacity={on ? 1 : 0.3}>
      <rect x={x} y={250} width={460} height={150} rx={8} fill="#14120a" stroke={col ?? C.yellow} strokeWidth={3} />
      <Mono x={x + 230} y={318} textAnchor="middle" fontSize={32} fontWeight={700} fill={col ?? C.yellow}>{label}</Mono>
      <Mono x={x + 230} y={358} textAnchor="middle" fontSize={22} fill={C.dim}>{sub}</Mono>
    </g>
  );
  return (
    <svg width={SW} height={SH} viewBox={`0 0 ${SW} ${SH}`}>
      <rect width={SW} height={SH} fill={C.panel} />
      <Mono x={80} y={80} fontSize={34} fill={C.yellow} fontWeight={700}>APPLY IS ALWAYS REVERSIBLE</Mono>
      <Node x={80} label="YOUR SETUP" sub="gamepad, pilot, map" on={p > 0.02} />
      <Node x={650} label="BACKUP SAVED" sub="state before HOTAS" on={p > 0.3} col={C.cyan} />
      <Node x={1220} label="HOTAS ACTIVE" sub="controls + Flight Control map" on={p > 0.45} col={C.green} />
      <g opacity={fade(p, 0.25, 0.33)}>
        <line x1={540} y1={325} x2={650} y2={325} stroke={C.yellow} strokeWidth={4} />
        <Mono x={595} y={300} textAnchor="middle" fontSize={26} fill={C.yellow}>Apply</Mono>
      </g>
      <g opacity={fade(p, 0.42, 0.5)}>
        <line x1={1110} y1={325} x2={1220} y2={325} stroke={C.green} strokeWidth={4} />
      </g>
      <g opacity={fade(p, 0.62, 0.72)}>
        <path d="M 1450 400 C 1450 560 310 560 310 400" fill="none" stroke={C.cyan} strokeWidth={4} strokeDasharray="12 8" />
        <polygon points="310,400 298,424 322,424" fill={C.cyan} />
        <Mono x={880} y={590} textAnchor="middle" fontSize={28} fill={C.cyan}>Restore backup  /  Gamepad mode</Mono>
      </g>
      <Mono x={80} y={670} fontSize={28} fill={C.yellow}>Close the game before Apply. Start it from the Flight Control launcher afterwards.</Mono>
    </svg>
  );
};

const DiagramShare: React.FC<{f: number; len: number}> = ({f, len}) => {
  const p = f / len;
  const File: React.FC<{x: number; label: string; col: string; items: {t: string; gone: boolean}[]}> = ({x, label, col, items}) => (
    <g>
      <rect x={x} y={170} width={420} height={360} rx={10} fill="#14120a" stroke={col} strokeWidth={3} />
      <Mono x={x + 210} y={215} textAnchor="middle" fontSize={34} fontWeight={700} fill={col}>{label}</Mono>
      {items.map((it, i) => (
        <Mono key={i} x={x + 30} y={280 + i * 48} fontSize={26} fill={it.gone ? C.red : C.yellow} textDecoration={it.gone ? 'line-through' : 'none'}>
          {it.t}
        </Mono>
      ))}
    </g>
  );
  const strip = p > 0.32;
  const items = [
    {t: 'bindings', gone: false}, {t: 'wheel roles', gone: false}, {t: 'axis settings', gone: false},
    {t: 'device serials', gone: strip}, {t: 'picture paths', gone: strip},
  ];
  const fly = ease(fade(p, 0.45, 0.65));
  return (
    <svg width={SW} height={SH} viewBox={`0 0 ${SW} ${SH}`}>
      <rect width={SW} height={SH} fill={C.panel} />
      <Mono x={80} y={80} fontSize={34} fill={C.yellow} fontWeight={700}>SHARING A .FCHOTAS PROFILE</Mono>
      <File x={80} label="my.fchotas" col={C.yellow} items={items} />
      <g transform={`translate(${interpolate(fly, [0, 1], [0, 0])} 0)`} opacity={fade(p, 0.4, 0.5)}>
        <line x1={520} y1={350} x2={1060} y2={350} stroke={C.green} strokeWidth={4} strokeDasharray="12 8" />
        <polygon points="1070,350 1046,336 1046,364" fill={C.green} />
        <Mono x={790} y={330} textAnchor="middle" fontSize={26} fill={C.green}>safe to send</Mono>
      </g>
      <g opacity={fade(p, 0.5, 0.6)}>
        <File x={1100} label="friend's Console" col={C.cyan} items={[{t: 'backup of theirs: saved', gone: false}, {t: 'preview: what is dropped', gone: false}, {t: 'then load', gone: false}]} />
      </g>
      <circle cx={interpolate(fly, [0, 1], [520, 1100])} cy={350} r={14} fill={C.green} opacity={fly > 0 && fly < 1 ? 1 : 0} />
      <Mono x={80} y={620} fontSize={28} fill={C.yellow}>Loading keeps your previous profile as a backup first.</Mono>
    </svg>
  );
};

const DiagramCalib: React.FC<{f: number; len: number}> = ({f, len}) => {
  const p = f / len;
  const phase = p < 0.15 ? 0 : p < 0.5 ? 1 : p < 0.82 ? 2 : 3;
  const wobble = Math.sin(f / 2.3) * 0.025 + Math.sin(f / 1.1) * 0.012;
  const sweep = Math.sin(((p - 0.5) / 0.32) * Math.PI * 2.5);
  const pos = phase === 1 ? 0.5 + wobble : phase === 2 ? 0.5 + 0.5 * sweep : 0.5;
  const x0 = 160, x1 = 1600, y = 330;
  const dz = phase === 3 ? 0.08 : 0;
  const step = (n: number, t: string, a: boolean) => (
    <g opacity={a ? 1 : 0.3}>
      <rect x={80 + n * 560} y={470} width={520} height={110} rx={8} fill="#14120a" stroke={a ? C.green : C.dim} strokeWidth={3} />
      <Mono x={110 + n * 560} y={515} fontSize={28} fontWeight={700} fill={a ? C.green : C.yellow}>{t.split('|')[0]}</Mono>
      <Mono x={110 + n * 560} y={553} fontSize={24} fill={C.dim}>{t.split('|')[1]}</Mono>
    </g>
  );
  return (
    <svg width={SW} height={SH} viewBox={`0 0 ${SW} ${SH}`}>
      <rect width={SW} height={SH} fill={C.panel} />
      <Mono x={80} y={80} fontSize={34} fill={C.yellow} fontWeight={700}>CALIBRATION IN TWO STEPS</Mono>
      <rect x={x0} y={y - 26} width={x1 - x0} height={52} fill="#14120a" stroke={C.dim} strokeWidth={2} />
      <rect x={(x0 + x1) / 2 - dz * (x1 - x0)} y={y - 26} width={dz * 2 * (x1 - x0)} height={52} fill="#7a2a18" opacity={0.8} />
      <line x1={(x0 + x1) / 2} y1={y - 40} x2={(x0 + x1) / 2} y2={y + 40} stroke={C.dim} strokeWidth={2} />
      <rect x={x0 + pos * (x1 - x0) - 6} y={y - 40} width={12} height={80} fill={C.green} />
      <Mono x={x0} y={y + 80} fontSize={24} fill={C.dim}>min</Mono>
      <Mono x={x1} y={y + 80} fontSize={24} fill={C.dim} textAnchor="end">max</Mono>
      <Mono x={(x0 + x1) / 2} y={y - 70} textAnchor="middle" fontSize={26} fill={C.yellow}>
        {phase === 1 ? 'resting: measuring centre and wobble' : phase === 2 ? 'sweeping: measuring full travel' : phase === 3 ? 'recommended dead zone applied' : 'ready'}
      </Mono>
      {step(0, '1  REST|leave it alone for 2 seconds', phase === 1)}
      {step(1, '2  SWEEP|move to every end for 6 seconds', phase === 2)}
      {step(2, '3  RESULT|recentre, stretch, dead zone', phase === 3)}
    </svg>
  );
};

const Diagram: React.FC<{name: string; f: number; len: number}> = ({name, f, len}) => {
  const p = {f, len};
  return (
    <div style={{width: SW, height: SH, border: `2px solid ${C.dim}`, overflow: 'hidden'}}>
      {name === 'wheel' ? <DiagramWheel {...p} /> : name === 'encoder' ? <DiagramEncoder {...p} /> : name === 'backup' ? <DiagramBackup {...p} /> : name === 'share' ? <DiagramShare {...p} /> : <DiagramCalib {...p} />}
    </div>
  );
};

// ---------- captions
const sentences = (t: string) => (t.replace('dot F C hotas', '.fchotas').match(/[^.?!]+[.?!]+/g) ?? [t]).map((s) => s.trim());

const Captions: React.FC<{text: string; dur: number}> = ({text, dur}) => {
  const f = useCurrentFrame();
  const ss = sentences(text);
  const total = ss.reduce((a, s) => a + s.length, 0);
  let acc = 0;
  const t = (f - PRE) / FPS;
  let current = '';
  for (const s of ss) {
    const a = (acc / total) * dur;
    acc += s.length;
    const b = (acc / total) * dur;
    if (t >= a - 0.05 && t < b + 0.1) {current = s; break;}
  }
  return (
    <div style={{position: 'absolute', left: 80, right: 80, bottom: 46, minHeight: 110, display: 'flex', alignItems: 'center', justifyContent: 'center', textAlign: 'center'}}>
      {current && (
        <div style={{background: 'rgba(0,0,0,0.78)', border: `1px solid ${C.dim}`, color: '#fff7cc', fontFamily: FONT, fontSize: 34, lineHeight: 1.3, padding: '14px 28px', maxWidth: 1700}}>
          {current}
        </div>
      )}
    </div>
  );
};

// ---------- chapter + title cards
const Chapter: React.FC<{c: (typeof chapters)[number]}> = ({c}) => {
  const f = useCurrentFrame();
  const len = c.frames;
  const list = stages[c.id];
  const active = list.find((s) => f / len >= s.from && f / len < s.to) ?? list[list.length - 1];
  const from = Math.floor(active.from * len);
  const to = Math.floor(active.to * len);
  const lf = f - from;
  const xf = fade(lf, 0, 10) * (1 - fade(lf, to - from - 8, to - from));
  const showTitle = c.id === 'intro' && f < 105;
  const showEnd = c.id === 'outro' && f > len - 100;
  return (
    <AbsoluteFill style={{background: C.bg, fontFamily: FONT}}>
      <Sequence from={PRE}><Audio src={staticFile(`audio/${c.id}.mp3`)} /></Sequence>
      <div style={{position: 'absolute', left: 80, top: 36, display: 'flex', alignItems: 'baseline', gap: 24}}>
        <span style={{color: C.green, fontSize: 30}}>{String(c.n).padStart(2, '0')} / {String(chapters.length).padStart(2, '0')}</span>
        <span style={{color: C.yellow, fontSize: 44, fontWeight: 700, letterSpacing: 2, textTransform: 'uppercase'}}>{c.title}</span>
      </div>
      <div style={{position: 'absolute', right: 80, top: 44, color: C.dim, fontSize: 24}}>FLIGHT CONTROL / HOTAS</div>
      <div style={{position: 'absolute', left: 80, top: 110, opacity: xf, transform: `translateY(${(1 - xf) * 10}px)`}}>
        {active.kind === 'shot' ? <Shot st={active} f={lf} len={to - from} /> : <Diagram name={active.name} f={lf} len={to - from} />}
      </div>
      <Captions text={c.text} dur={c.dur} />
      {showTitle && (
        <AbsoluteFill style={{background: `rgba(5,5,4,${0.92 * (1 - fade(f, 80, 105))})`, justifyContent: 'center', alignItems: 'center', opacity: 1 - fade(f, 85, 105)}}>
          <div style={{color: C.dim, fontSize: 34, letterSpacing: 6}}>FATE OF THE GALAXY 2</div>
          <div style={{color: C.yellow, fontSize: 110, fontWeight: 700, letterSpacing: 6, marginTop: 18}}>FLIGHT CONTROL</div>
          <div style={{color: C.green, fontSize: 52, letterSpacing: 10, marginTop: 10}}>HOTAS TUTORIAL</div>
        </AbsoluteFill>
      )}
      {showEnd && (
        <AbsoluteFill style={{background: `rgba(5,5,4,${0.9 * fade(f, len - 100, len - 70)})`, justifyContent: 'center', alignItems: 'center', opacity: fade(f, len - 100, len - 70)}}>
          <div style={{color: C.yellow, fontSize: 96, fontWeight: 700, letterSpacing: 6}}>FLY SAFE</div>
          <div style={{color: C.dim, fontSize: 32, marginTop: 20}}>Manual and downloads: manictrashpanda.dev/games/fotgflightcontrol</div>
        </AbsoluteFill>
      )}
    </AbsoluteFill>
  );
};

export const Tutorial: React.FC = () => {
  let at = 0;
  return (
    <AbsoluteFill style={{background: C.bg}}>
      {chapters.map((c) => {
        const from = at;
        at += c.frames;
        return (
          <Sequence key={c.id} from={from} durationInFrames={c.frames}>
            <Chapter c={c} />
          </Sequence>
        );
      })}
    </AbsoluteFill>
  );
};
