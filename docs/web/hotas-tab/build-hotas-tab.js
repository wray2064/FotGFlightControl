// Builds the self-contained HOTAS tab for the Flight Control web page.
//   node build-hotas-tab.js
// Reads ../HOTAS-WEB-COPY.md (cards, text, FAQ, changelog) and ../HOTAS-Manual.md (the guide), writes:
//   (next to the copy file) hotas-tab.html          the fragment to drop into the tab (no <html>/<body>, all CSS scoped to .fc-hotas)
//   hotas-tab-preview.html  the fragment inside a fake "normal page" with a tab bar, for checking only
const fs = require("fs");
const path = require("path");
const here = __dirname;
const base = fs.existsSync(path.join(here, "HOTAS-WEB-COPY.md")) ? here : path.join(here, ".."); // works in docs/web/hotas-tab/ and in the handoff web/ folder
const read = f => fs.readFileSync(path.join(base, f), "utf8").replace(/\r\n/g, "\n");

// ---- settings the web builder may change -------------------------------------------------------
const VIDEO_ID = "EyNQRSUJw8I";
const VIDEO_URL = "https://youtu.be/" + VIDEO_ID;
const ASSET = "hotas/";                 // where screenshots/ and video/thumbnail.png are served from, relative to the page
const BRANCH = "codex/flight-control-hotas";
const CHAPTERS = [["Welcome", 0], ["Turn on Advanced", 20], ["Scan your devices", 37], ["Press to assign", 56], ["Game and Flight Control actions", 80],
  ["One button, different jobs", 106], ["Encoders and dials", 128], ["Flight axes", 146], ["Apply and go back", 179], ["Save and share", 199], ["If something is missing", 221]];
// -------------------------------------------------------------------------------------------------

const esc = s => s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
const todos = [];
function inline(text) {
  let out = esc(text);
  out = out.replace(/!\[([^\]]*)\]\(([^)]+)\)/g, (m, alt, src) => `<img src="${ASSET}${src}" alt="${alt}" loading="lazy">`);
  out = out.replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2">$1</a>');
  out = out.replace(/\*\*\[([^\]]+)\]\*\*/g, (m, t) => { todos.push(t); return `<mark class="fc-todo">[${t}]</mark>`; });
  out = out.replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>");
  out = out.replace(/`([^`]+)`/g, "<code>$1</code>");
  return out;
}
const slug = t => "hotas-" + t.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");

function mdToHtml(md, shiftHeadings) {
  const lines = md.split("\n"), html = [];
  let i = 0;
  while (i < lines.length) {
    const line = lines[i];
    if (/^# /.test(line)) { i++; continue; } // the tab has its own heading
    if (/^## /.test(line)) { const t = line.slice(3); html.push(`<h${2 + shiftHeadings} id="${slug(t)}">${inline(t)}</h${2 + shiftHeadings}>`); i++; continue; }
    if (/^> /.test(line)) { const b = []; while (i < lines.length && /^> /.test(lines[i])) { b.push(lines[i].slice(2)); i++; } html.push(`<blockquote>${inline(b.join(" "))}</blockquote>`); continue; }
    if (/^\|/.test(line)) {
      const rows = []; while (i < lines.length && /^\|/.test(lines[i])) { rows.push(lines[i]); i++; }
      const cells = r => r.replace(/^\||\|$/g, "").split("|").map(c => c.trim());
      html.push('<div class="fc-scroll"><table><thead><tr>' + cells(rows[0]).map(c => `<th>${inline(c)}</th>`).join("") + "</tr></thead><tbody>" +
        rows.slice(2).map(cells).map(r => "<tr>" + r.map(c => `<td>${inline(c)}</td>`).join("") + "</tr>").join("") + "</tbody></table></div>");
      continue;
    }
    if (/^- /.test(line)) { const it = []; while (i < lines.length && /^- /.test(lines[i])) { it.push(lines[i].slice(2)); i++; } html.push("<ul>" + it.map(t => `<li>${inline(t)}</li>`).join("") + "</ul>"); continue; }
    if (/^\d+\. /.test(line)) { const it = []; while (i < lines.length && /^\d+\. /.test(lines[i])) { it.push(lines[i].replace(/^\d+\. /, "")); i++; } html.push("<ol>" + it.map(t => `<li>${inline(t)}</li>`).join("") + "</ol>"); continue; }
    if (/^!\[/.test(line)) { html.push(`<figure>${inline(line)}<figcaption>${esc(line.match(/!\[([^\]]*)\]/)[1])}</figcaption></figure>`); i++; continue; }
    if (line.trim() === "") { i++; continue; }
    const p = []; while (i < lines.length && lines[i].trim() !== "" && !/^(#|>|\||- |\d+\. |!\[)/.test(lines[i])) { p.push(lines[i]); i++; }
    html.push(`<p>${inline(p.join(" "))}</p>`);
  }
  return html.join("\n");
}

// ---- pull the pieces out of the copy file
const copy = read("HOTAS-WEB-COPY.md");
const sec = n => { const m = copy.match(new RegExp("^## " + n + "\\. [^\\n]*\\n([\\s\\S]*?)(?=^## \\d+\\. |$(?![\\s\\S]))", "m")); if (!m) throw new Error("copy section " + n + " missing"); return m[1].trim(); };
const intro = (sec(3).match(/\*\*Intro:\*\*\n([^\n]+(?:\n[^\n]+)*)/) || [])[1] || "";
const cards = [...sec(3).matchAll(/^(\d\d) \*\*(.+?)\.?\*\* (.+)$/gm)].map(m => ({ n: m[1], t: m[2], d: m[3] }));
const tested = sec(4);
const steps = sec(5).split("\n").filter(l => /^\d+\. /.test(l));
const faq = [...sec(8).matchAll(/^\*\*(.+?\?)\*\* (.+)$/gm)].map(m => ({ q: m[1], a: m[2] }));
const changelog = sec(9).split("\n").filter(l => /^- /.test(l));
const manual = read("HOTAS-Manual.md");

const fig = (file, alt) => `<figure><img src="${ASSET}screenshots/${file}" alt="${esc(alt)}" loading="lazy"><figcaption>${esc(alt)}</figcaption></figure>`;

const fragment = `<!-- Flight Control: HOTAS tab (EXPERIMENTAL branch ${BRANCH}). Self-contained: one scoped <style>, one optional <script>. -->
<section class="fc-hotas" id="hotas" data-experimental="true" aria-labelledby="hotas-title">
<style>
.fc-hotas{--bg:#000;--panel:#08090a;--ink:#f7dc52;--dim:#b39d4d;--line:#957d13;--green:#66e752;--text:#ece6c4;--warn:#ff8a4a;color:var(--text);font:16px/1.6 "Segoe UI",system-ui,sans-serif}
@media (prefers-color-scheme: light){.fc-hotas{--bg:#fffdf2;--panel:#fff;--ink:#5c4a00;--dim:#7a6a2a;--line:#c9b04a;--green:#2a7d1c;--text:#222;--warn:#a33a00}}
.fc-hotas *,.fc-hotas *::before,.fc-hotas *::after{box-sizing:border-box}
.fc-hotas h1,.fc-hotas h2,.fc-hotas h3,.fc-hotas h4{font-family:Consolas,"Courier New",monospace;color:var(--ink);letter-spacing:.02em;line-height:1.25}
.fc-hotas h1{font-size:2rem;margin:.2em 0 .3em}.fc-hotas h2{font-size:1.4rem;margin:1.8em 0 .5em;border-left:4px solid var(--line);padding-left:.6em}
.fc-hotas h3{font-size:1.12rem;margin:1.6em 0 .4em}
.fc-hotas a{color:var(--ink)}.fc-hotas a:focus-visible,.fc-hotas button:focus-visible{outline:3px solid var(--green);outline-offset:2px}
.fc-hotas code{font-family:Consolas,monospace;background:var(--panel);border:1px solid var(--line);padding:.05em .3em;border-radius:3px;color:var(--green)}
.fc-hotas .fc-banner{border:2px solid var(--warn);background:var(--panel);padding:.8em 1em;margin:0 0 1.2em}
.fc-hotas .fc-banner strong.fc-tag{display:inline-block;background:var(--warn);color:#000;font-family:Consolas,monospace;padding:.05em .5em;margin-right:.5em;letter-spacing:.08em}
.fc-hotas .fc-eyebrow{font-family:Consolas,monospace;color:var(--green);letter-spacing:.12em;font-size:.85rem}
.fc-hotas .fc-nav{display:flex;flex-wrap:wrap;gap:.5rem;margin:1.2em 0;border-bottom:1px solid var(--line);padding-bottom:.6em}
.fc-hotas .fc-nav a{font-family:Consolas,monospace;text-decoration:none;border:1px solid var(--line);padding:.3em .8em;background:var(--panel)}
.fc-hotas .fc-nav a[aria-current="true"]{background:var(--ink);color:var(--bg)}
.fc-hotas .fc-cards{display:grid;grid-template-columns:repeat(auto-fill,minmax(260px,1fr));gap:1rem;padding:0;list-style:none;margin:1em 0}
.fc-hotas .fc-cards li{border:1px solid var(--line);background:var(--panel);padding:.9em 1em}
.fc-hotas .fc-cards b{font-family:Consolas,monospace;color:var(--green);display:block}
.fc-hotas figure{margin:1.2em 0}.fc-hotas figure img{max-width:100%;height:auto;border:1px solid var(--line);display:block}
.fc-hotas figcaption{font-size:.85rem;color:var(--dim);margin-top:.4em}
.fc-hotas blockquote{margin:1em 0;padding:.6em 1em;background:var(--panel);border:1px solid var(--line);border-left:4px solid var(--ink)}
.fc-hotas .fc-scroll{overflow-x:auto}.fc-hotas table{border-collapse:collapse;width:100%;margin:1em 0;font-size:.95rem}
.fc-hotas th,.fc-hotas td{border:1px solid var(--line);padding:.45em .6em;text-align:left;vertical-align:top}.fc-hotas th{background:var(--panel);color:var(--ink);font-family:Consolas,monospace}
.fc-hotas .fc-video{position:relative;aspect-ratio:16/9;background:#000;border:1px solid var(--line);max-width:960px}
.fc-hotas .fc-video img,.fc-hotas .fc-video iframe{position:absolute;inset:0;width:100%;height:100%;border:0;object-fit:cover}
.fc-hotas .fc-video button{position:absolute;inset:0;width:100%;height:100%;background:transparent;border:0;cursor:pointer;color:#000}
.fc-hotas .fc-video button span{position:absolute;left:50%;top:50%;transform:translate(-50%,-50%);background:var(--green);padding:.5em 1.4em;font:700 1.2rem Consolas,monospace}
.fc-hotas .fc-chapters{columns:2 280px;padding-left:1.2em}.fc-hotas .fc-chapters li{break-inside:avoid}
.fc-hotas details{border:1px solid var(--line);background:var(--panel);margin:.5em 0;padding:.5em 1em}.fc-hotas summary{cursor:pointer;font-weight:600;color:var(--ink)}
.fc-hotas mark.fc-todo{background:#ffe14d;color:#000;padding:0 .2em;font-weight:700}
.fc-hotas .fc-guide{border-top:2px solid var(--line);margin-top:2em;padding-top:1em}
.fc-hotas [hidden]{display:none!important}
@media (prefers-reduced-motion:reduce){.fc-hotas *{scroll-behavior:auto!important}}
</style>

<div class="fc-banner" role="note"><strong class="fc-tag">EXPERIMENTAL</strong>This is the <strong>HOTAS preview</strong>, built on a separate experimental branch (<code>${BRANCH}</code>). It is not part of the regular Flight Control release, and the regular version works without it. Fate of the Galaxy 2.0.2 only.</div>

<p class="fc-eyebrow">HOTAS &middot; EXPERIMENTAL PREVIEW</p>
<h1 id="hotas-title">Your stick, your throttle, your pedals.</h1>

<nav class="fc-nav" aria-label="HOTAS sections">
<a href="#hotas-overview" data-fc-panel="overview">Overview</a>
<a href="#hotas-video" data-fc-panel="video">Video tutorial</a>
<a href="#hotas-guide" data-fc-panel="guide">Guide (manual)</a>
<a href="#hotas-get" data-fc-panel="get">Download and report</a>
</nav>

<div id="hotas-overview" data-fc-section="overview">
<p>${inline(intro)}</p>
${fig("01-hotas-tab-overview.png", "The HOTAS tab in the Flight Control Console, showing a joystick with its axes, hat and buttons labelled with the actions assigned to them")}
<ul class="fc-cards">
${cards.map(c => `<li><b>${c.n}</b><strong>${inline(c.t)}.</strong> ${inline(c.d)}</li>`).join("\n")}
</ul>
<h2 id="hotas-tested">Tested so far</h2>
<p>${inline(tested)}</p>
<h2 id="hotas-whatsnew">What is in the experimental branch</h2>
<ul>
${changelog.map(l => `<li>${inline(l.slice(2))}</li>`).join("\n")}
</ul>
</div>

<div id="hotas-video" data-fc-section="video">
<h2>Video tutorial</h2>
<p>About four minutes, with captions: scanning, assigning, wheels, encoders, flight axes, backups and sharing.</p>
<div class="fc-video" data-fc-video="${VIDEO_ID}">
<img src="${ASSET}video/thumbnail.png" alt="HOTAS Setup, full tutorial: title card with the Flight Control Console" loading="lazy">
<button type="button" aria-label="Play the HOTAS tutorial video"><span>&#9654; PLAY</span></button>
<noscript><a href="${VIDEO_URL}">Watch the tutorial on YouTube</a></noscript>
</div>
<p><small>The video loads from YouTube only when you press play. <a href="${VIDEO_URL}" rel="noopener">Open it on YouTube</a>.</small></p>
<h3>Chapters</h3>
<ol class="fc-chapters">
${CHAPTERS.map(([t, s]) => `<li><a href="${VIDEO_URL}?t=${s}" rel="noopener" data-fc-seek="${s}">${String(Math.floor(s / 60))}:${String(s % 60).padStart(2, "0")} ${esc(t)}</a></li>`).join("\n")}
</ol>
</div>

<div id="hotas-guide" class="fc-guide" data-fc-section="guide">
<h2>HOTAS guide</h2>
<p>The manual for the experimental HOTAS preview. It is separate from the regular Flight Control documentation.</p>
${mdToHtml(manual, 1)}
</div>

<div id="hotas-get" data-fc-section="get">
<h2>Install the preview</h2>
<ol>
${steps.map(l => `<li>${inline(l.replace(/^\d+\. /, ""))}</li>`).join("\n")}
</ol>
<h2>Download</h2>
<p><mark class="fc-todo">[FotG-Flight-Control-HOTAS-experimental.zip]</mark> &middot; version <mark class="fc-todo">[1.3.0-hotas-experimental]</mark> &middot; <mark class="fc-todo">[date]</mark> &middot; size <mark class="fc-todo">[size]</mark> &middot; SHA-256 <mark class="fc-todo">[sha256]</mark></p>
<p>This download is separate from the regular Flight Control download and does not replace it. It changes the Console, the launcher and the wheel script. Setup backs up your pilot before it changes anything, and the HOTAS tab backs up again before each Apply. The programs are not code-signed, like the regular release; the usual security notes on this page apply. Source: github.com/wray2064/FotGFlightControl, branch <code>${BRANCH}</code>. MIT for the code.</p>
<h2>Questions</h2>
${faq.map(f => `<details><summary>${inline(f.q)}</summary><p>${inline(f.a)}</p></details>`).join("\n")}
<h2>Send a report</h2>
<p>Tell me what your hardware does: <a href="mailto:dev@manictrashpanda.dev">dev@manictrashpanda.dev</a>. A saved <code>.fchotas</code> profile contains no serial numbers and no file paths, so it is safe to attach.</p>
</div>

<script>
(function () {
  var root = document.getElementById("hotas"); if (!root) return;
  var secs = root.querySelectorAll("[data-fc-section]"), links = root.querySelectorAll("[data-fc-panel]");
  function show(name) {
    for (var i = 0; i < secs.length; i++) secs[i].hidden = secs[i].getAttribute("data-fc-section") !== name;
    for (var j = 0; j < links.length; j++) links[j].setAttribute("aria-current", links[j].getAttribute("data-fc-panel") === name ? "true" : "false");
  }
  for (var k = 0; k < links.length; k++) links[k].addEventListener("click", function (e) { e.preventDefault(); show(this.getAttribute("data-fc-panel")); });
  show("overview");
  // Without this script every section simply stacks, and the anchors still work.
  function play(box, start) {
    var f = document.createElement("iframe");
    f.src = "https://www.youtube-nocookie.com/embed/" + box.getAttribute("data-fc-video") + "?autoplay=1&rel=0" + (start ? "&start=" + start : "");
    f.title = "HOTAS tutorial"; f.allow = "autoplay; encrypted-media; picture-in-picture"; f.allowFullscreen = true;
    box.innerHTML = ""; box.appendChild(f);
  }
  var box = root.querySelector("[data-fc-video]");
  box.querySelector("button").addEventListener("click", function () { play(box, 0); });
  var seeks = root.querySelectorAll("[data-fc-seek]");
  for (var s = 0; s < seeks.length; s++) seeks[s].addEventListener("click", function (e) { e.preventDefault(); play(box, this.getAttribute("data-fc-seek")); box.scrollIntoView({ block: "center" }); });
})();
</script>
</section>
`;

const preview = `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>PREVIEW ONLY: HOTAS tab inside a stand-in Flight Control page</title>
<style>body{margin:0;background:#000;color:#ece6c4;font:16px "Segoe UI",sans-serif}@media (prefers-color-scheme: light){body{background:#fffdf2;color:#222}}
.stand-in{max-width:1000px;margin:0 auto;padding:16px}.tabs{display:flex;gap:.5rem;border-bottom:1px solid #957d13;margin-bottom:1rem}.tabs span{padding:.4em 1em;border:1px solid #957d13;font-family:Consolas,monospace}.tabs .on{background:#f7dc52;color:#000}
.note{background:#222;color:#ffe14d;padding:.5em 1em;font:14px Consolas,monospace}</style></head><body>
<div class="note">Stand-in page for checking only: the real tab bar belongs to the site. Only the section below is the deliverable (hotas-tab.html).</div>
<div class="stand-in"><div class="tabs"><span>Overview</span><span>Download</span><span>Docs</span><span class="on">HOTAS (experimental)</span></div>
${fragment}
</div></body></html>`;

fs.writeFileSync(path.join(base, "hotas-tab.html"), fragment);
fs.writeFileSync(path.join(base, "hotas-tab-preview.html"), preview);
console.log("hotas-tab.html " + fragment.length + " bytes; cards " + cards.length + ", faq " + faq.length + ", changelog " + changelog.length + ", steps " + steps.length);
console.log("placeholders still to fill: " + todos.length + " (plus 5 in the download block)");
