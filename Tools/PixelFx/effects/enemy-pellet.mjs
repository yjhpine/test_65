// Shotgun pellet (radius 0.21 u = 3.8 px): 9 x 9.
import { drawShot, shotMeta } from './enemy-shot.mjs';
export const meta = shotMeta('Enemy pellet', [9, 9], [4, 4], 4, 12, { radius: 4, core: 1.8, seed: 8 });
export function draw(p, t, o) { drawShot(p, t, o, meta.fps); }
