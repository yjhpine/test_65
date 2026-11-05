// Enemy attack danger zone: one red "corrupted memory" tile that every monster lays over its attack area.
// A bug in the computer world, in reds only (no teal): moving scanlines, dead and missing pixels, torn rows,
// displaced data blocks with a crimson echo, and a 1 px rim that drops out in places.
// Authored on the player art's grid (18 px per unit). Unity imports each frame with a 1 px border and draws it
// Tiled, so the noise stays at art-pixel size however far a monster stretches the zone; the renderer's alpha
// (not this art) carries the warning ramp from faint to strong.
// Tiling: the 30 px center repeats, so every pattern here is periodic in 30 px or stays inside the tile.
import { fit } from '../core.mjs';

export const meta = {
  name: 'Attack zone', size: [32, 32], origin: [16, 16],
  duration: 8 / 12, fps: 12, loop: true, bg: 'dark',
  options: { seed: 11, palette: ['#2a050c', '#5c0f1a', '#9c1b2e', '#c42c42', '#ff6b7d', '#ffd7dd'] }
};

const L = -16, R = 15, T = -16, B = 15;
// Stable per-frame noise: the same frame index always gives the same value, so the loop has no seam.
function hash(a, b, c = 0) {
  let h = Math.imul(a ^ 0x9e3779b9, 0x85ebca6b) ^ Math.imul(b + 0x632be5ab, 0xc2b2ae35) ^ Math.imul(c + 0x165667b1, 0x27d4eb2f);
  h ^= h >>> 13; h = Math.imul(h, 0x27d4eb2f);
  return ((h ^ (h >>> 15)) >>> 0) / 4294967296;
}

export function draw(p, t, o) {
  const C = fit(o.palette, 6);
  const N = Math.round(meta.duration * meta.fps), s = Math.floor(t * meta.fps + 1e-6) % N;
  const seed = o.seed, r = (k) => hash(s * 13 + seed, k);
  const inside = (x, y) => x > L && x < R && y > T && y < B;

  // Pane: scanlines that crawl down one row per frame (period 3 divides the 30 px center, so tiles meet cleanly).
  for (let y = T + 1; y < B; y++) {
    const band = ((y - T - 1 + 30 - (s % 3)) % 3 === 0) ? C[3] : C[2];
    for (let x = L + 1; x < R; x++) {
      const n = hash(x - L, y - T, seed + s * 7);
      if (n < 0.025) continue;                     // missing pixel: a hole in the data
      p.px(x, y, n > 0.985 ? C[5] : n > 0.955 ? C[4] : band);  // dead pixels flicker bright
    }
  }

  // Displaced data blocks: a bright chunk with a dark crimson echo one pixel to the side.
  const blocks = 2 + Math.floor(r(1) * 3);
  for (let k = 0; k < blocks; k++) {
    const w = 3 + Math.floor(r(10 + k) * 6), h = 1 + Math.floor(r(20 + k) * 3);
    const x0 = L + 1 + Math.floor(r(30 + k) * (30 - w)), y0 = T + 1 + Math.floor(r(40 + k) * (30 - h));
    const echo = r(50 + k) < 0.5 ? -1 : 1, color = r(60 + k) < 0.35 ? C[1] : C[4];
    for (let y = y0; y < y0 + h; y++)
      for (let x = x0; x < x0 + w; x++) {
        if (inside(x + echo, y)) p.px(x + echo, y, C[1]);
        p.px(x, y, color);
      }
  }

  // A torn row on some frames: one scanline slides sideways and burns brighter.
  if (r(70) < 0.6) {
    const y = T + 1 + Math.floor(r(71) * 30), shift = 2 + Math.floor(r(72) * 4), from = L + 1 + Math.floor(r(73) * 12);
    for (let x = from; x < Math.min(R, from + 10 + Math.floor(r(74) * 8)); x++) p.px(x, y, (x + shift) % 2 ? C[4] : C[3]);
  }

  // Rim: lighter red with dropped segments and bright corners. Each edge tiles along its side in Unity.
  for (let i = L; i <= R; i++) {
    const top = hash(i - L, 1, seed + (s >> 1)) < 0.12, bottom = hash(i - L, 2, seed + (s >> 1)) < 0.12;
    const left = hash(3, i - T, seed + (s >> 1)) < 0.12, right = hash(4, i - T, seed + (s >> 1)) < 0.12;
    p.px(i, T, top ? C[2] : C[4]); p.px(i, B, bottom ? C[2] : C[4]);
    p.px(L, i, left ? C[2] : C[4]); p.px(R, i, right ? C[2] : C[4]);
  }
  for (const [x, y] of [[L, T], [R, T], [L, B], [R, B]]) p.px(x, y, C[5]);
}
