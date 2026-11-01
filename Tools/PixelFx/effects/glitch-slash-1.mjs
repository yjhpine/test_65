import { meta as base, cues, draw as drawCombo } from './glitch-triple-slash.mjs';
const index = 0;
export const meta = { ...base, name: 'Glitch slash '+(index+1), duration: cues[index].duration };
export function draw(p,t,o) { if(t >= meta.duration) return; drawCombo(p, t+cues[index].start, {...o, singleStrike:index}); }
