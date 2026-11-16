import React from 'react';
import {Composition} from 'remotion';
import {Tutorial, totalFrames} from './Tutorial';
import {Thumbnail} from './Thumbnail';

export const Root: React.FC = () => (
  <>
    <Composition id="Tutorial" component={Tutorial} durationInFrames={totalFrames} fps={30} width={1920} height={1080} />
    <Composition id="Thumbnail" component={Thumbnail} durationInFrames={1} fps={30} width={1280} height={720} />
  </>
);
