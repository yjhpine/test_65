// Seamless loop shown while the next strike is the strong one. Drawn behind the character; no stock actor baked in.
// Authored on the player art's own grid (18 px per unit): (0, 0) is the Visual center, the body spans
// about x = -11..9 and y = -13 (head) .. 14 (feet). Only the band round the body and the flames above it show.
import { hex, fit, cycle, loopNoise, noise2, TAU } from '../core.mjs';

export const meta = {
  name: 'Finisher aura', size: [60, 64], origin: [30, 46],
  duration: 1.2, fps: 30, loop: true, bg: 'dark',
  options: { seed: 11, palette: ['#0d3a46', '#1b7f8c', '#38d6d0', '#79d9df', '#e6fffb'], accent: '#d84255' }
};

const CX = -1, HEAD = -13, FEET = 14;
const TIPS = [[-7, 8, 6], [0, 14, 7], [7, 8, 6]];

// Waves that travel up the hull's sides: whole cycles per loop, so they never seam. Each side has its own.
function ripple(y, side, phase, seed) {
  const s = side > 0 ? 1.7 : 0;
  return 1.6 * Math.sin(0.45 * y + TAU * 3 * phase + s) + 0.9 * Math.sin(1.05 * y + TAU * 5 * phase + 2 * s + 1)
    + 1.2 * (noise2(y * 0.3, side * 7 + Math.cos(phase * TAU) * 1.3, seed) - 0.5);
}

function halfWidth(y, side, phase, seed) {
  const v = Math.min(1, Math.max(0, (FEET - y) / (FEET - HEAD)));
  return 10 + 4 * Math.sin(Math.PI * (0.15 + 0.7 * v)) + ripple(y, side, phase, seed);
}

// Flickering envelope: a body-hugging hull plus three flame tips over the head. Loops with the phase.
function inside(x, y, phase, seed) {
  if (y > FEET + 1) return false;
  const side = x < CX ? -1 : 1;
  if (y >= HEAD - 1) return Math.abs(x - CX) < halfWidth(y, side, phase, seed);
  // Above the head only the tips stand up, never wider than the hull at the head.
  if (Math.abs(x - CX) >= halfWidth(HEAD - 1, side, phase, seed)) return false;
  let top = HEAD - 1;
  TIPS.forEach(([tx, h, tw], i) => {
    const height = h * (0.65 + 0.35 * Math.sin(TAU * (2 * phase + i * 0.29)) + 0.3 * loopNoise(phase + i * 0.37, 1.2, seed + i));
    const sway = 1.5 * Math.sin(phase * TAU * 2 + i * 2.1);
    const k = Math.max(0, 1 - Math.abs(x - CX - tx - sway) / tw);
    top = Math.min(top, HEAD - 1 - height * k ** 1.6);
  });
  return y > top;
}

export function draw(p, t, o) {
  const C = fit(o.palette, 5), red = hex(o.accent), T = meta.duration, phase = t / T;
  const pulse = 0.5 + 0.5 * Math.cos(phase * TAU * 2);

  // Soft cold light round the body, fading out well past the hull.
  p.disc(CX, 0, 24, C[1], (d) => (0.1 + 0.06 * pulse) * (1 - d) ** 2);

  // The hull: translucent inside, a dense lit rim, a band of white light climbing it twice a loop.
  const climb = FEET + 2 - ((phase * 2) % 1) * (FEET - HEAD + 26);
  p.each(-30, -46, 30, 18, (x, y, sx, sy) => {
    if (!inside(x, y, phase, o.seed)) {
      // The red channel slips a pixel off the left rim on some rows, like a misaligned scanline.
      const slip = Math.floor(phase * 12 + y * 0.5) % 5 === 0;
      if (slip && x < CX && inside(x + 1, y, phase, o.seed)) p.blendPx(sx, sy, red, 0.75);
      return;
    }
    let edge = 0;
    for (let r = 1; r <= 3 && !edge; r++)
      if (!inside(x - r, y, phase, o.seed) || !inside(x + r, y, phase, o.seed) || !inside(x, y - r, phase, o.seed)) edge = r;
    const band = Math.max(0, 1 - Math.abs(y - climb) / 4);
    // Crests of the side waves burn hotter than the troughs; the hull thins out towards the floor.
    const crest = ripple(y, x < CX ? -1 : 1, phase, o.seed) > 1.2;
    const rise = 0.3 + 0.7 * Math.min(1, Math.max(0, (FEET + 1 - y) / 8));
    if (edge === 1) p.blendPx(sx, sy, band > 0.4 || crest ? C[4] : C[3], 0.85 * rise);
    else if (edge === 2) p.blendPx(sx, sy, band > 0.4 ? C[3] : C[2], (0.38 + 0.35 * band) * rise);
    else if (edge === 3) p.blendPx(sx, sy, C[2], (0.2 + 0.2 * band) * rise);
    else p.blendPx(sx, sy, C[1], (0.08 + 0.05 * pulse) * rise);
  });

  // Tongues of light tear off the hull and rise, tapering and swaying.
  cycle(t, T, 30, o.seed, (r) => r.range(0.3, 0.55), (age, life, r) => {
    const u = age / life, side = r.sign(), base = r.next();
    const bx = CX + side * (12 + r.range(-1, 2)) * (1 - 0.55 * base * base);
    const by = FEET - base * (FEET - HEAD + 6);
    const x = bx * (1 - 0.35 * u) + Math.sin(u * 5 + r.range(0, TAU)) * 1.5 * u;
    const y = by - 20 * u - 10 * u * u;
    const len = Math.max(1, Math.round((4 + r.range(0, 4)) * (1 - u)));
    const k = (1 - u) ** 1.1;
    p.line(x, y, x, y + len, (v) => (v < 0.34 ? C[4] : C[2]), (v) => k * (1 - v * 0.5));
    if (r.chance(0.3)) p.line(x - side, y + 1, x - side, y + len, red, k * 0.6);
  });

  // Motes over the head that drift up and wink out.
  cycle(t, T, 10, o.seed + 3, (r) => r.range(0.4, 0.8), (age, life, r) => {
    const u = age / life;
    const x = CX + r.range(-10, 10) * (1 - 0.3 * u), y = HEAD - 6 - r.range(0, 8) - 18 * u;
    if (u < 0.8 || Math.floor(age * 30) % 2 === 0) p.dot(x, y, u < 0.3 ? C[4] : C[3], 1 - u);
  });

  // Glitch slices: short displaced bars beside the hull that jump to a new height four times a loop.
  const step = Math.floor(phase * 8);
  if (step % 2 === 0) {
    for (let j = 0; j < 2; j++) {
      const n = loopNoise((step + j * 3) / 8, 2.1, o.seed + j);
      const y = Math.round(HEAD + n * (FEET - HEAD)), side = j === 0 ? -1 : 1;
      const x0 = CX + side * 16, w = 3 + Math.round(n * 5);
      p.line(x0, y, x0 + side * w, y, j === 0 ? red : C[3], 0.9);
      p.line(x0 + side, y + 1, x0 + side * (w - 1), y + 1, C[4], 0.55);
    }
  }

  // A flat ring under the feet that breathes with the pulse.
  const rr = 12 + pulse * 2;
  for (let i = 0; i < 80; i++) {
    const a = (i / 80) * TAU, front = Math.sin(a) > 0;
    p.blend(CX + Math.cos(a) * rr, FEET + 1 + Math.sin(a) * 2.2, front ? C[3] : C[1], front ? 0.7 : 0.35);
  }
}
