// Active domain field: a looping violet "locked memory" tile laid over the area where a glitch start or arrival kills.
// Diagonal hatching that crawls one pixel per frame, a solid 1 px rim and a few displaced violet blocks; violet and
// magenta only (no teal, and distinct from the red attack zone). Unity imports it with a 1 px border and draws it
// Tiled, so the hatch keeps art-pixel size on any area; the renderer's alpha sets the strength.
// Tiling: the 30 px center repeats, so the hatch period (6) divides 30 and the loop is 6 frames (one period).
import { fit } from '../core.mjs';

export const meta = {
  name: 'Domain field', size: [32, 32], origin: [16, 16],
  duration: 6 / 12, fps: 12, loop: true, bg: 'dark',
  options: { seed: 23, palette: ['#14041f', '#33104d', '#5c1f8a', '#9440d0', '#d38cff', '#f6dcff'] }
};

const L = -16, R = 15, T = -16, B = 15;
function hash(a, b, c = 0) {
  let h = Math.imul(a ^ 0x9e3779b9, 0x85ebca6b) ^ Math.imul(b + 0x632be5ab, 0xc2b2ae35) ^ Math.imul(c + 0x165667b1, 0x27d4eb2f);
  h ^= h >>> 13; h = Math.imul(h, 0x27d4eb2f);
  return ((h ^ (h >>> 15)) >>> 0) / 4294967296;
}

export function draw(p, t, o) {
  const C = fit(o.palette, 6), N = 6, s = Math.floor(t * meta.fps + 1e-6) % N;
  for (let y = T + 1; y < B; y++)
    for (let x = L + 1; x < R; x++) {
      const band = ((x - L - 1) + (y - T - 1) + 30 * 2 - s) % 6;  // diagonal hatch, period 6
      p.px(x, y, band === 0 ? C[3] : band === 1 ? C[2] : C[1]);
    }
  // Displaced blocks (they also repeat with the tile).
  for (let k = 0; k < 2; k++) {
    const w = 3 + Math.floor(hash(k, s, o.seed) * 5), x0 = L + 2 + Math.floor(hash(k + 4, s, o.seed) * (26 - w));
    const y0 = T + 2 + Math.floor(hash(k + 8, s, o.seed) * 26);
    for (let x = x0; x < x0 + w; x++) p.px(x, y0, k ? C[4] : C[5]);
  }
  // Solid rim: the border slices draw the area's outline.
  for (let i = L; i <= R; i++) { p.px(i, T, C[4]); p.px(i, B, C[4]); p.px(L, i, C[4]); p.px(R, i, C[4]); }
}
