// A barrage shot removed by a wall or the player (presentation only): 11 x 11, four frames spread over 0.12 s.
// A pink-white cross spark, a broken red ring, then two blinking bits. No teal.
import { fit } from '../core.mjs';

export const meta = {
  name: 'Enemy shot pop', size: [11, 11], origin: [5, 5],
  duration: 4 / 30, fps: 30, loop: true, bg: 'dark',
  options: { palette: ['#7a0a26', '#ff3b5c', '#ff9aae', '#fff0f3'], accent: '#ff3df0' }
};

export function draw(p, t, o) {
  const C = fit(o.palette, 4), s = Math.floor(t * meta.fps + 1e-6) % 4;
  if (s === 0) {
    p.line(-3, 0, 3, 0, C[2]); p.line(0, -3, 0, 3, C[2]); p.px(0, 0, C[3]);
  } else if (s === 1) {
    for (let a = 0; a < 8; a++) {
      if (a === 2 || a === 5) continue;                       // broken ring
      const ang = a / 8 * Math.PI * 2;
      p.px(Math.round(Math.cos(ang) * 4), Math.round(Math.sin(ang) * 4), a % 3 === 0 ? o.accent : C[1]);
    }
    p.px(0, 0, C[2]);
  } else if (s === 2) {
    p.px(-4, -2, C[1]); p.px(4, 3, C[1]); p.px(2, -4, C[0]); p.px(-3, 4, o.accent);
  } else {
    p.px(-5, -3, C[0]); p.px(5, 4, C[0]);
  }
}
