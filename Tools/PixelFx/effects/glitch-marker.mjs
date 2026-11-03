// Glitch destination marker: corner brackets that breathe, an RGB-split shadow, tears, a scan line and pixel noise.
// One source, two looks: ready (teal, diamond core) and blocked (red, X core, harder jitter).
// Authored on the player art's grid (18 px per unit); (0, 0) is where the player's center will land.
import { fit } from '../core.mjs';

export const meta = {
  name: 'Glitch marker', size: [32, 32], origin: [16, 16],
  duration: 0.8, fps: 30, loop: true, bg: 'dark',
  options: { seed: 5, blocked: false,
    ready: ['#0d3a46', '#1b7f8c', '#38d6d0', '#79d9df', '#e6fffb'],
    stop: ['#4a0f1c', '#8c1f33', '#d84255', '#ff6b81', '#ffe3e8'] }
};

const ARM = 4;
// Stable per-frame noise: the same frame index always gives the same value, so the loop has no seam.
function hash(a, b) {
  let h = Math.imul(a ^ 0x9e3779b9, 0x85ebca6b) ^ Math.imul(b + 0x632be5ab, 0xc2b2ae35);
  h ^= h >>> 13; h = Math.imul(h, 0x27d4eb2f);
  return ((h ^ (h >>> 15)) >>> 0) / 4294967296;
}

function bracket(p, x, y, sx, sy, color, tip) {
  p.line(x, y, x + sx * (ARM - 1), y, color);
  p.line(x, y, x, y + sy * (ARM - 1), color);
  if (tip) p.px(x, y, tip);
}

export function draw(p, t, o) {
  const blocked = !!o.blocked;
  const C = fit(blocked ? o.stop : o.ready, 5), A = fit(blocked ? o.ready : o.stop, 5);
  const N = Math.round(meta.duration * meta.fps), s = Math.floor(t * meta.fps + 1e-6) % N, u = s / N;
  const seed = o.seed + (blocked ? 31 : 0), r = (k) => hash(s * 7 + seed, k);

  // The box breathes in by a pixel for a beat twice a loop; blocked also shakes sideways.
  const H = s % 12 < 3 ? 7 : 8;
  const jx = blocked && r(1) < 0.35 ? (r(2) < 0.5 ? -1 : 1) : 0;

  // A faint pane so the destination still reads over busy backgrounds.
  p.each(-H + 1 + jx, -H + 1, H - 1 + jx, H - 1, (x, y, sx, sy) => p.blendPx(sx, sy, C[1], 0.16));

  // Scan line sweeping the pane top to bottom once a loop (ready), or two stuck static rows (blocked).
  if (!blocked) {
    const y = -H + 1 + Math.floor(u * (2 * H - 1));
    p.each(-H + 1, y, H - 1, y, (x, yy, sx, sy) => p.blendPx(sx, sy, C[3], 0.4));
  } else {
    for (let k = 0; k < 2; k++) {
      const y = -H + 2 + Math.floor(r(10 + k) * (2 * H - 3));
      p.each(-H + 1 + jx, y, H - 1 + jx, y, (x, yy, sx, sy) => p.blendPx(sx, sy, C[2], 0.35));
    }
  }

  const corners = [[-H, -H, 1, 1], [H, -H, -1, 1], [-H, H, 1, -1], [H, H, -1, -1]];
  // One bracket at a time tears sideways for a frame.
  const torn = r(3) < (blocked ? 0.45 : 0.2) ? Math.floor(r(4) * 4) : -1, tear = r(5) < 0.5 ? -2 : 2;
  // The other channel slips a pixel off, more often when blocked.
  const split = r(6) < (blocked ? 0.7 : 0.45) ? (r(7) < 0.5 ? -1 : 1) : 0;
  corners.forEach(([x, y, sx, sy], i) => {
    const dx = jx + (i === torn ? tear : 0);
    if (split) bracket(p, x + dx + split, y, sx, sy, A[2]);
    bracket(p, x + dx, y, sx, sy, C[3], C[4]);
  });

  // Core: a diamond that pulses (ready) or an X that flickers (blocked).
  if (!blocked) {
    const big = s % 8 < 4;
    for (const [x, y] of big ? [[0, -2], [1, -1], [2, 0], [1, 1], [0, 2], [-1, 1], [-2, 0], [-1, -1]] : [[0, -1], [1, 0], [0, 1], [-1, 0]])
      p.px(x, y, C[3]);
    p.px(0, 0, C[4]);
    if (split) p.px(split * 3, 0, A[2]);
  } else if (r(8) > 0.12) {
    if (split) { p.line(-2 + split, -2, 2 + split, 2, A[2]); p.line(2 + split, -2, -2 + split, 2, A[2]); }
    p.line(-2 + jx, -2, 2 + jx, 2, C[3]);
    p.line(2 + jx, -2, -2 + jx, 2, C[3]);
    p.px(jx, 0, C[4]);
  }

  // Glitch slices: short displaced bars beside the box on some frames.
  if (r(9) < (blocked ? 0.5 : 0.35)) {
    const side = r(12) < 0.5 ? -1 : 1, y = -H + Math.floor(r(13) * (2 * H + 1)), w = 2 + Math.floor(r(14) * 3);
    p.line(side * (H + 2), y, side * (H + 1 + w), y, r(15) < 0.5 ? A[2] : C[3]);
  }

  // Loose pixels flicker round the box.
  for (let k = 0; k < (blocked ? 4 : 3); k++) {
    const a = r(20 + k) * Math.PI * 2, d = H + 2 + r(30 + k) * 4;
    p.blend(Math.round(Math.cos(a) * d), Math.round(Math.sin(a) * d), k % 2 ? A[2] : C[3], 0.85);
  }
}
