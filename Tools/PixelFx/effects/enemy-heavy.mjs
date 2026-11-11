// Slow heavy shot (radius 0.505 u = 9.1 px): 19 x 19, deeper reds.
import { drawShot, shotMeta } from './enemy-shot.mjs';
export const meta = shotMeta('Enemy heavy shot', [19, 19], [9, 9], 6, 10,
  { radius: 9, core: 4, seed: 13, palette: ['#2a030c', '#7a0a26', '#d61f45', '#ff7d96', '#ffe3ea'] });
export function draw(p, t, o) { drawShot(p, t, o, meta.fps); }
