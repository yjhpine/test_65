// Mortar ground blast (radius 1.26 u = 22.7 px, 0.12 s): 47 x 47 centered on the impact point. Four frames that the
// game spreads over the blast's own duration: a white-hot flash, an expanding torn red ring with data shards,
// then embers. Reds only (no teal). Authored on the player art's grid (18 px per unit).
import { fit } from '../core.mjs';

export const meta = {
  name: 'Enemy mortar blast', size: [47, 47], origin: [23, 23],
  duration: 4 / 30, fps: 30, loop: true, bg: 'dark',
  options: { seed: 17, palette: ['#2a050c', '#7a0a26', '#d61f45', '#ff6b7d', '#ffd7dd', '#ffffff'], accent: '#ff3df0' }
};

function hash(a, b, c = 0) {
  let h = Math.imul(a ^ 0x9e3779b9, 0x85ebca6b) ^ Math.imul(b + 0x632be5ab, 0xc2b2ae35) ^ Math.imul(c + 0x165667b1, 0x27d4eb2f);
  h ^= h >>> 13; h = Math.imul(h, 0x27d4eb2f);
  return ((h ^ (h >>> 15)) >>> 0) / 4294967296;
}

export function draw(p, t, o) {
  const C = fit(o.palette, 6), s = Math.floor(t * meta.fps + 1e-6) % 4, R = 22;
  const outer = [11, 18, 22, 22][s], inner = [0, 12, 18, 21][s];
  for (let y = -R; y <= R; y++) {
    // Torn rows: some rows slide sideways, the bug look of the shared attack zone.
    const tear = hash(y + 40, s, o.seed) < 0.12 ? (hash(y, s + 9, o.seed) < 0.5 ? -2 : 2) : 0;
    for (let x = -R; x <= R; x++) {
      const d = Math.hypot(x, y);
      if (d > outer + 0.4 || d < inner - 0.4) continue;
      const n = hash(x + 50, y + 50, o.seed + s * 13);
      if (s >= 2 && n < 0.35) continue;                    // the ring breaks up as it fades
      let c;
      if (s === 0) c = d < 5 ? C[5] : d < 8 ? C[4] : C[3];
      else {
        const u = (d - inner) / Math.max(1, outer - inner); // 0 inner edge .. 1 outer edge
        c = s === 1 ? (u > 0.75 ? C[1] : u > 0.3 ? C[3] : C[4]) : s === 2 ? (u > 0.6 ? C[0] : C[2]) : C[1];
      }
      p.px(x + tear, y, c);
    }
  }
  // Data shards thrown out of the ring.
  const shards = [0, 10, 12, 8][s];
  for (let k = 0; k < shards; k++) {
    const a = hash(k, 3, o.seed) * Math.PI * 2, d = outer - 3 + hash(k, s, o.seed + 5) * 5;
    const x = Math.round(Math.cos(a) * d), y = Math.round(Math.sin(a) * d);
    const w = 1 + Math.floor(hash(k, 7, o.seed) * 3);
    p.rect(x, y, w, 1, k % 4 === 0 ? o.accent : s === 3 ? C[1] : C[4]);
  }
}
