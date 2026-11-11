import { meta as base, draw as drawMarker } from './glitch-marker.mjs';
export const meta = { ...base, name: 'Glitch marker (blocked)', options: { ...base.options, blocked: true } };
export function draw(p, t, o) { drawMarker(p, t, { ...o, blocked: true }); }
