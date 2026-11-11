// Mortar shell (radius 0.379 u = 6.8 px): a 13 px disc with a 6 px fall trail above it, centered on the disc.
import { drawShot, shotMeta } from './enemy-shot.mjs';
export const meta = shotMeta('Enemy mortar shell', [13, 19], [6, 12], 4, 12,
  { radius: 6, core: 2.5, seed: 21, trail: 6, palette: ['#1c040a', '#5c0c22', '#c41c3f', '#ff8aa0', '#fff0f3'] });
export function draw(p, t, o) { drawShot(p, t, o, meta.fps); }
