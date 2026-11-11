import { meta as base, draw as drawMarker } from './glitch-marker.mjs';
export const meta = { ...base, name: 'Glitch marker (ready)', options: { ...base.options, blocked: false } };
export function draw(p, t, o) { drawMarker(p, t, { ...o, blocked: false }); }
