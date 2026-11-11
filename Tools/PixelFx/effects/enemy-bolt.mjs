// Straight shot (radius 0.295 u = 5.3 px): 11 x 11.
import { drawShot, shotMeta } from './enemy-shot.mjs';
export const meta = shotMeta('Enemy bolt', [11, 11], [5, 5], 4, 12, { radius: 5, core: 2.5, seed: 5 });
export function draw(p, t, o) { drawShot(p, t, o, meta.fps); }
