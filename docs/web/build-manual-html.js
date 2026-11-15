// Turns HOTAS-Manual.md into a standalone HTML page (no dependencies): node build-manual-html.js
const fs = require("fs");
const path = require("path");
const dir = __dirname;
const md = fs.readFileSync(path.join(dir, "HOTAS-Manual.md"), "utf8").replace(/\r\n/g, "\n");

const esc = s => s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
function inline(text) {
  let out = esc(text);
  out = out.replace(/!\[([^\]]*)\]\(([^)]+)\)/g, (m, alt, src) => `<img src="${src}" alt="${alt}" loading="lazy">`);
  out = out.replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2">$1</a>');
  out = out.replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>");
  out = out.replace(/`([^`]+)`/g, "<code>$1</code>");
  return out;
}

const lines = md.split("\n");
const html = [];
let i = 0;
let title = "HOTAS guide";
while (i < lines.length) {
  const line = lines[i];
  if (/^# /.test(line)) { title = line.slice(2); html.push(`<h1>${inline(title)}</h1>`); i++; continue; }
  if (/^## /.test(line)) { const t = line.slice(3); html.push(`<h2 id="${t.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "")}">${inline(t)}</h2>`); i++; continue; }
  if (/^> /.test(line)) { const block = []; while (i < lines.length && /^> /.test(lines[i])) { block.push(lines[i].slice(2)); i++; } html.push(`<blockquote>${inline(block.join(" "))}</blockquote>`); continue; }
  if (/^\|/.test(line)) {
    const rows = []; while (i < lines.length && /^\|/.test(lines[i])) { rows.push(lines[i]); i++; }
    const cells = r => r.replace(/^\||\|$/g, "").split("|").map(c => c.trim());
    const head = cells(rows[0]); const body = rows.slice(2).map(cells);
    html.push("<table><thead><tr>" + head.map(c => `<th>${inline(c)}</th>`).join("") + "</tr></thead><tbody>" +
      body.map(r => "<tr>" + r.map(c => `<td>${inline(c)}</td>`).join("") + "</tr>").join("") + "</tbody></table>");
    continue;
  }
  if (/^- /.test(line)) { const items = []; while (i < lines.length && /^- /.test(lines[i])) { items.push(lines[i].slice(2)); i++; } html.push("<ul>" + items.map(t => `<li>${inline(t)}</li>`).join("") + "</ul>"); continue; }
  if (/^\d+\. /.test(line)) { const items = []; while (i < lines.length && /^\d+\. /.test(lines[i])) { items.push(lines[i].replace(/^\d+\. /, "")); i++; } html.push("<ol>" + items.map(t => `<li>${inline(t)}</li>`).join("") + "</ol>"); continue; }
  if (/^!\[/.test(line)) { html.push(`<figure>${inline(line)}<figcaption>${esc(line.match(/!\[([^\]]*)\]/)[1])}</figcaption></figure>`); i++; continue; }
  if (line.trim() === "") { i++; continue; }
  const para = []; while (i < lines.length && lines[i].trim() !== "" && !/^(#|>|\||- |\d+\. |!\[)/.test(lines[i])) { para.push(lines[i]); i++; }
  html.push(`<p>${inline(para.join(" "))}</p>`);
}

const page = `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>${esc(title)}</title>
<style>
:root{--bg:#000;--panel:#08090a;--ink:#f7dc52;--dim:#b39d4d;--line:#957d13;--green:#66e752;--text:#ece6c4}
@media (prefers-color-scheme: light){:root{--bg:#fffdf2;--panel:#fff;--ink:#5c4a00;--dim:#7a6a2a;--line:#c9b04a;--green:#2a7d1c;--text:#222}}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:16px/1.6 "Segoe UI",system-ui,sans-serif}
main{max-width:860px;margin:0 auto;padding:24px 16px 64px}
h1,h2{font-family:Consolas,"Courier New",monospace;color:var(--ink);letter-spacing:.02em}
h1{font-size:1.9rem;border-bottom:1px solid var(--line);padding-bottom:.4em}
h2{font-size:1.25rem;margin-top:2.2em;border-left:4px solid var(--line);padding-left:.6em}
a{color:var(--ink)}code{font-family:Consolas,monospace;background:var(--panel);border:1px solid var(--line);padding:.05em .3em;border-radius:3px;color:var(--green)}
blockquote{margin:1em 0;padding:.6em 1em;background:var(--panel);border:1px solid var(--line);border-left:4px solid var(--ink)}
figure{margin:1.4em 0}figure img{max-width:100%;height:auto;border:1px solid var(--line);display:block}
figcaption{font-size:.85rem;color:var(--dim);margin-top:.4em}
table{border-collapse:collapse;width:100%;margin:1em 0;font-size:.95rem}th,td{border:1px solid var(--line);padding:.45em .6em;text-align:left;vertical-align:top}
th{background:var(--panel);color:var(--ink);font-family:Consolas,monospace}
@media (max-width:600px){table{display:block;overflow-x:auto}}
</style></head><body><main>
${html.join("\n")}
</main></body></html>`;
fs.writeFileSync(path.join(dir, "HOTAS-Manual.html"), page);
console.log("wrote HOTAS-Manual.html (" + page.length + " bytes)");
