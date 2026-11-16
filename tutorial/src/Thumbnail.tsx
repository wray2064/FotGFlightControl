import React from 'react';
import {AbsoluteFill, Img, staticFile} from 'remotion';

const FONT = 'Consolas, "Courier New", monospace';

export const Thumbnail: React.FC = () => (
  <AbsoluteFill style={{background: '#050504', fontFamily: FONT, overflow: 'hidden'}}>
    <AbsoluteFill style={{background: 'radial-gradient(ellipse at 75% 50%, rgba(240,208,74,0.18), rgba(5,5,4,0) 60%)'}} />
    <div style={{position: 'absolute', right: -190, top: 150, width: 760, height: 517, transform: 'rotate(-4deg)', border: '4px solid #f0d04a', boxShadow: '0 0 60px rgba(240,208,74,0.35)'}}>
      <Img src={staticFile('shots/01-hotas-tab-overview.png')} style={{width: '100%', height: '100%', display: 'block'}} />
    </div>
    <div style={{position: 'absolute', left: 60, top: 70, color: '#9a8a3a', fontSize: 34, letterSpacing: 6}}>FATE OF THE GALAXY 2</div>
    <div style={{position: 'absolute', left: 60, top: 120, color: '#f0d04a', fontSize: 150, fontWeight: 700, lineHeight: 0.95, letterSpacing: 2}}>
      HOTAS<br />SETUP
    </div>
    <div style={{position: 'absolute', left: 60, top: 450, color: '#fff7cc', fontSize: 40, lineHeight: 1.25}}>
      Stick, throttle, pedals,<br />encoders. Bind anything.
    </div>
    <div style={{position: 'absolute', left: 60, bottom: 60, background: '#62e85a', color: '#021', fontSize: 44, fontWeight: 700, padding: '10px 28px', letterSpacing: 4}}>
      FULL TUTORIAL
    </div>
  </AbsoluteFill>
);
