// Builds out/hotas-tutorial.srt and a chapter list from script.json (same timing rules as src/Tutorial.tsx).
const fs = require('fs');
const s = JSON.parse(fs.readFileSync(__dirname + '/script.json', 'utf8'));
const FPS = 30, PRE = 9, TAIL = 30;
const ts = (sec) => { const ms = Math.round(sec * 1000); const p = (n, w = 2) => String(n).padStart(w, '0'); return `${p(Math.floor(ms / 3600000))}:${p(Math.floor(ms / 60000) % 60)}:${p(Math.floor(ms / 1000) % 60)},${p(ms % 1000, 3)}`; };
let at = 0, n = 1, srt = '', chap = '';
for (const c of s) {
  const frames = Math.max(Math.ceil((c.dur + 1) * FPS) + PRE + TAIL - 30, Math.ceil(c.dur * FPS) + PRE + TAIL);
  const start = at / FPS + PRE / FPS;
  chap += `${ts(at / FPS).slice(3, 8)} ${c.title}\n`;
  const ss = (c.text.replace('dot F C hotas', '.fchotas').match(/[^.?!]+[.?!]+/g) || [c.text]).map((x) => x.trim());
  const tot = ss.reduce((a, x) => a + x.length, 0); let acc = 0;
  for (const x of ss) { const a = start + (acc / tot) * c.dur; acc += x.length; const b = start + (acc / tot) * c.dur; srt += `${n++}\n${ts(a)} --> ${ts(b)}\n${x}\n\n`; }
  at += frames;
}
fs.mkdirSync(__dirname + '/out', {recursive: true});
fs.writeFileSync(__dirname + '/out/hotas-tutorial.srt', srt);
fs.writeFileSync(__dirname + '/out/chapters.txt', chap);
console.log(chap);
