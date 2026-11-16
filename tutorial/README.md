# HOTAS tutorial video

A Remotion project that renders the Flight Control HOTAS tutorial (about 4 minutes, 1080p, 30 fps).

- `script.json`: the narration, one entry per chapter, with the measured length (seconds) of its voice clip.
- `public/audio/*.mp3`: the narration, generated once with ElevenLabs (voice "Max - eLearning, Documentary, Friendly", model eleven_v4). Re-recording a chapter means editing its text in `script.json`, generating a new clip, and updating `dur`.
- `public/shots/`: Console screenshots (copies of `docs/web/screenshots`).
- `src/Tutorial.tsx`: scenes, callout boxes (`stages`), animated diagrams and captions.
- `make-srt.cjs`: writes `out/hotas-tutorial.srt` and `out/chapters.txt` with the same timing rules.

```
npm install
npm run studio     # preview and edit
npm run render     # out/hotas-tutorial.mp4
node make-srt.cjs  # captions and chapter list
```

The first render downloads a headless Chromium. `node_modules` and `out` are not committed.
