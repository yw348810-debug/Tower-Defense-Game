"""Deterministic original fantasy-game audio; no samples or third-party music.

Run from the project root: python Tools/Audio/generate_audio.py
Requires Python 3.10+, NumPy and SciPy. Outputs PCM 44.1 kHz / 16 bit WAV.
"""
from __future__ import annotations

import json
import math
import wave
from pathlib import Path

import numpy as np
from scipy.signal import butter, sosfilt

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets" / "Game" / "Resources" / "Audio"
REPORT = ROOT / "Docs" / "Audio-Validation.json"
SR = 44100
RNG = np.random.default_rng(481516)


def time_axis(seconds):
    return np.arange(round(seconds * SR), dtype=np.float64) / SR


def frequency(midi):
    return 440.0 * 2.0 ** ((midi - 69) / 12.0)


def envelope(t, attack=0.008, release=0.12):
    if len(t) == 0:
        return t
    length = len(t) / SR
    return np.sin(np.minimum(1.0, t / max(attack, 1 / SR)) * np.pi / 2) ** 2 * np.sin(
        np.minimum(1.0, (length - t) / max(release, 1 / SR)) * np.pi / 2
    ) ** 2


def filtered_noise(t, cutoff=1800, highpass=False):
    signal = RNG.normal(0, 1, len(t))
    sos = butter(2, cutoff, fs=SR, btype="highpass" if highpass else "lowpass", output="sos")
    return sosfilt(sos, signal)


def pluck(midi, seconds=1.8, brightness=1.0):
    """Soft additive harp with faster decay of upper string harmonics."""
    t = time_axis(seconds)
    f = frequency(midi)
    y = np.zeros(len(t))
    for h in range(1, 9):
        if f * h > 10000:
            break
        y += np.sin(2 * np.pi * f * h * t + 0.05 * h) * np.exp(-t * (1.8 + h * 0.72)) / h ** (1.3 / brightness)
    return y * envelope(t, 0.003, 0.13) * 0.38


def mallet(midi, seconds=2.0, icy=False):
    t = time_axis(seconds)
    f = frequency(midi)
    y = np.sin(2 * np.pi * f * t) * np.exp(-t * 2.4)
    for ratio, weight, decay in [(2.0, 0.22, 3.7), (3.01 if icy else 3.0, 0.09, 5.5), (4.17, 0.032, 8.0)]:
        y += weight * np.sin(2 * np.pi * f * ratio * t) * np.exp(-t * decay)
    return y * envelope(t, 0.004, 0.16) * 0.45


def flute(midi, seconds):
    t = time_axis(seconds)
    f = frequency(midi)
    phase = 2 * np.pi * f * t + 0.018 * np.sin(2 * np.pi * 4.7 * t)
    y = np.sin(phase) + 0.19 * np.sin(2 * phase) + 0.08 * np.sin(3 * phase)
    y += filtered_noise(t, 2000) * 0.06
    return y * envelope(t, 0.055, 0.16) * 0.31


def strings(midi, seconds, cold=False):
    t = time_axis(seconds)
    f = frequency(midi)
    y = np.zeros(len(t))
    # Two slightly detuned bowed-string voices, rolled-off upper harmonics.
    for detune in [-0.0014, 0.0014]:
        phase = 2 * np.pi * f * (1 + detune) * t + 0.016 * np.sin(2 * np.pi * 4.3 * t)
        for h in range(1, 7):
            y += np.sin(h * phase + h * 0.3) * np.exp(-h * (0.38 if cold else 0.46)) / h ** 1.0
    return y * envelope(t, 0.45, 0.8) * 0.18


def bass(midi, seconds):
    t = time_axis(seconds)
    f = frequency(midi)
    y = np.sin(2 * np.pi * f * t) + 0.22 * np.sin(4 * np.pi * f * t) + 0.075 * np.sin(6 * np.pi * f * t)
    return y * envelope(t, 0.012, min(seconds * 0.25, 0.22)) * np.exp(-t * 0.7) * 0.40


def drum(seconds=0.6, fundamental=68, weight=1):
    t = time_axis(seconds)
    freq = fundamental + 85 * np.exp(-t * 28)
    phase = 2 * np.pi * np.cumsum(freq) / SR
    y = np.sin(phase) * np.exp(-t * 9) + filtered_noise(t, 1300) * np.exp(-t * 35) * 0.20
    return y * envelope(t, 0.002, 0.12) * 0.56 * weight


def snare(seconds=0.27):
    t = time_axis(seconds)
    y = filtered_noise(t, 3300) * np.exp(-t * 17) * 0.32
    y += np.sin(2 * np.pi * 180 * t) * np.exp(-t * 27) * 0.21
    return y * envelope(t, 0.002, 0.07)


def shaker(seconds=0.13):
    t = time_axis(seconds)
    return filtered_noise(t, 4800, True) * np.exp(-t * 39) * envelope(t, 0.002, 0.03) * 0.12


def add_loop(stereo, signal, start_seconds, gain=1, pan=0):
    # Every note, including release/reverb tails, wraps rather than being cut.
    n = len(stereo)
    start = round(start_seconds * SR) % n
    left = math.cos((pan + 1) * math.pi / 4) * gain
    right = math.sin((pan + 1) * math.pi / 4) * gain
    for offset in range(0, len(signal), n):
        part = signal[offset:offset + n]
        first = min(len(part), n - start)
        stereo[start:start + first, 0] += part[:first] * left
        stereo[start:start + first, 1] += part[:first] * right
        if first < len(part):
            stereo[:len(part) - first, 0] += part[first:] * left
            stereo[:len(part) - first, 1] += part[first:] * right


def room(stereo, wet=0.18):
    dry = stereo.copy()
    taps = [(0.073, .38), (.119, .29), (.191, .24), (.277, .19), (.431, .16), (.617, .12), (.829, .08), (1.131, .05)]
    for index, (delay, gain) in enumerate(taps):
        delayed = np.roll(dry, round(delay * SR), axis=0)
        if index % 2:
            delayed = delayed[:, ::-1]
        stereo += delayed * wet * gain
    return stereo


def music(name, tempo, chords, melody, style):
    beat = 60 / tempo
    total = beat * 64  # 16 bars, four beats per bar.
    track = np.zeros((round(total * SR), 2), dtype=np.float64)
    for bar in range(16):
        chord = chords[bar % len(chords)]
        start = bar * beat * 4
        half = bar // 8
        for index, note in enumerate(chord):
            add_loop(track, strings(note, beat * 4 + 1.25, style == "snow"), start, .29 if style == "forest" else .40, (index - 1.5) * .21)
        # Bass walks to the fifth in the second half of each bar.
        bass_note = chord[0] - 24
        while bass_note < 31:
            bass_note += 12
        add_loop(track, bass(bass_note, beat * 1.8), start, .43 if style == "menu" else .63)
        add_loop(track, bass(bass_note + 7, beat * 1.7), start + beat * 2, .35 if style == "menu" else .49)
        if style in ("menu", "forest", "canyon"):
            sequence = [0, 2, 1, 3, 1, 2, 3, 2]
            for step, index in enumerate(sequence):
                note = chord[index] + (12 if style == "menu" and step in (3, 7) else 0)
                add_loop(track, pluck(note, beat * 2.5), start + beat * step / 2, .28 if style == "menu" else .39, (-1 if step % 2 else 1) * .33)
        else:
            for step in range(4):
                add_loop(track, mallet(chord[step] + 12, beat * 3.2, True), start + beat * step, .30, (-1 if step % 2 else 1) * .40)

        # Four-bar original motif; answering phrases shift octave or timbre.
        phrase = melody[bar % len(melody)]
        for position, note, duration in phrase:
            voice = flute(note, duration * beat) if style in ("menu", "forest") or half else mallet(note, duration * beat + .6, style == "snow")
            gain = .36 if style == "menu" else (.40 if style == "snow" else .48)
            add_loop(track, voice, start + position * beat, gain, -.10 if half else .12)
        if style == "menu":
            if bar % 2 == 0:
                add_loop(track, mallet(chord[2] + 12, 2.8), start + 3.5 * beat, .13, .46)
            continue
        # Light drums for forest; fuller tom rhythm for canyon and snow.
        for position in ([0, 2.5] if style == "forest" else [0, 1.5, 2, 3.5]):
            add_loop(track, drum(fundamental=70 if style == "forest" else 57), start + beat * position, .30 if style == "forest" else .43)
        for position in [1, 3]:
            add_loop(track, snare(), start + beat * position, .22 if style == "snow" else .29, -.17)
        if style != "snow":
            for step in range(8):
                add_loop(track, shaker(), start + beat * step / 2, .30 if style == "forest" else .36, .22)
        if style == "canyon" and bar % 4 == 3:
            for step in range(4):
                add_loop(track, drum(.35, 105 - step * 12), start + beat * (3 + step / 4), .22, (step - 1.5) * .16)
    room(track, .22 if style in ("menu", "snow") else .14)
    return name, track


def layer(*parts):
    n = max(len(signal) + round(start * SR) for signal, start, gain in parts)
    result = np.zeros(n)
    for signal, start, gain in parts:
        offset = round(start * SR)
        result[offset:offset + len(signal)] += signal * gain
    return result


def whoosh(seconds, descending=False, strength=1.0):
    t = time_axis(seconds)
    noise = filtered_noise(t, 2400) * .48 + filtered_noise(t, 650) * .6
    bend = 950 * (1 - t / seconds) + 130 if descending else 130 + 950 * (t / seconds)
    y = noise + .12 * np.sin(2 * np.pi * np.cumsum(bend) / SR)
    return y * np.sin(np.pi * t / seconds) ** 1.8 * strength


def impact(seconds=.25, heavy=False):
    t = time_axis(seconds)
    y = filtered_noise(t, 1900 if heavy else 3200) * np.exp(-t * (10 if heavy else 28))
    y += .45 * np.sin(2 * np.pi * (72 if heavy else 155) * t) * np.exp(-t * (8 if heavy else 32))
    return y * envelope(t, .002, .06)


def glass(seconds=.65, midi=88):
    t = time_axis(seconds)
    f = frequency(midi)
    y = np.zeros(len(t))
    for ratio, gain in [(1, .24), (1.49, .15), (2.11, .08), (2.57, .04)]:
        y += np.sin(2 * np.pi * f * ratio * t) * gain * np.exp(-t * (7 + ratio))
    y += filtered_noise(t, 4800) * np.exp(-t * 18) * .1
    return y * envelope(t, .0015, .11)


def brass(midi, seconds=.65):
    t = time_axis(seconds)
    phase = 2 * np.pi * frequency(midi) * t + .014 * np.sin(2 * np.pi * 5 * t)
    y = sum(np.sin(h * phase) / h ** 1.5 for h in range(1, 7))
    return y * envelope(t, .075, .23) * .37


def chime(notes, step=.085, seconds=.75):
    return layer(*[(mallet(note, seconds), i * step, .55) for i, note in enumerate(notes)])


def make_sfx():
    result = {}
    result["UIClick"] = layer((pluck(78, .10), 0, .7), (impact(.065), 0, .065))
    result["UIConfirm"] = chime([72, 79], .075, .36)
    result["UICancel"] = chime([72, 67], .08, .27)
    result["Build"] = layer((impact(.27, True), 0, .55), (chime([60, 67, 72], .07, .5), .08, .65))
    result["Upgrade"] = layer((whoosh(.35), 0, .24), (chime([67, 72, 79, 84], .085, .8), .10, .75))
    result["AugmentRefresh"] = layer((whoosh(.26), 0, .23), (chime([84, 79, 81], .055, .42), .035, .55))
    result["AugmentPick"] = layer((chime([72, 76, 79, 84], .075, .9), 0, .7), (brass(60, .7), .22, .25))
    result["GunShot"] = layer((impact(.12), 0, .55), (whoosh(.075, True), .015, .11), (pluck(67, .10), 0, .1))
    result["FrostShot"] = layer((glass(.35, 82), 0, .7), (whoosh(.15), 0, .12))
    result["CannonShot"] = layer((drum(.55, 48), 0, .85), (impact(.22, True), .01, .55), (whoosh(.18, True), .045, .12))
    result["ProjectileHit"] = impact(.16)
    t = time_axis(.95)
    explosion = filtered_noise(t, 1300) * np.exp(-t * 5) + .6 * np.sin(2 * np.pi * np.cumsum(40 + 80 * np.exp(-t * 10)) / SR) * np.exp(-t * 6)
    result["Explosion"] = explosion * envelope(t, .004, .2)
    result["Shatter"] = layer((glass(.70, 89), 0, 1), (glass(.50, 93), .07, .35), (impact(.2), 0, .25))
    result["SoldierHit"] = layer((impact(.16), 0, .7), (glass(.13, 66), .006, .30))
    result["SoldierDeath"] = layer((impact(.24, True), 0, .65), (impact(.16), .09, .16), (bass(42, .36), .05, .6))
    result["Fireball"] = layer((whoosh(.48), 0, .43), (explosion * envelope(t, .004, .2), .30, .8), (drum(.5, 43), .30, .45))
    result["FrostNova"] = layer((whoosh(.43), 0, .22), (chime([74, 81, 86, 93], .05, 1), .15, .65), (glass(.7, 86), .20, .35))
    result["Reinforcements"] = layer((chime([62, 69, 74, 81], .085, .85), 0, .7), (drum(.4, 70), .28, .28), (drum(.4, 74), .43, .20))
    result["Battlecry"] = layer((brass(62, 1.25), 0, .6), (brass(69, 1.15), .1, .45), (brass(74, 1.05), .2, .40), (drum(.6, 62), 0, .40))
    result["WaveStart"] = layer((drum(.35, 79), 0, .40), (brass(62, .48), .02, .48), (brass(69, .48), .12, .32))
    result["EnemyLeak"] = layer((bass(48, .32), 0, .8), (bass(43, .35), .12, .7), (impact(.12), 0, .22))
    result["BaseDanger"] = layer((brass(55, .24), 0, .52), (brass(55, .24), .30, .52), (bass(31, .70), 0, .6))
    result["BossArrival"] = layer((drum(.8, 43), 0, .85), (brass(38, 1.7), .05, .65), (brass(45, 1.6), .12, .48), (brass(50, 1.5), .22, .23), (drum(.6, 47), .45, .6))
    result["Victory"] = layer((chime([62, 66, 69, 74, 78, 81, 86], .12, 1.65), 0, .6), (brass(62, 1.6), .52, .3), (brass(69, 1.6), .52, .25), (brass(74, 1.6), .52, .3))
    result["Defeat"] = layer((mallet(62, 1.5), 0, .65), (mallet(58, 1.5), .20, .6), (mallet(53, 1.65), .4, .6), (bass(38, 1.7), .20, .6))
    return result


def write_wav(name, source, music_clip=False):
    signal = np.asarray(source, dtype=np.float64)
    signal -= np.mean(signal, axis=0)
    if not music_clip:
        t = np.arange(len(signal)) / SR
        # Short quiet margins make non-looping playback transitions reliable.
        signal *= envelope(t, .0035, .030)
        signal = np.pad(signal, (round(.008 * SR), round(.018 * SR)))
    peak = float(np.max(np.abs(signal)))
    ceiling = .56 if music_clip else .68
    signal *= ceiling / max(peak, 1e-8)
    pcm = np.round(np.clip(signal, -1, 1) * 32767).astype("<i2")
    with wave.open(str(OUT / (name + ".wav")), "wb") as handle:
        handle.setnchannels(2 if music_clip else 1)
        handle.setsampwidth(2)
        handle.setframerate(SR)
        handle.writeframes(pcm.tobytes())
    reconstructed = pcm.astype(np.float64) / 32767
    rms = float(np.sqrt(np.mean(reconstructed ** 2)))
    differences = np.abs(np.diff(reconstructed, axis=0))
    edge = float(np.max(np.abs(reconstructed[0] - reconstructed[-1])))
    report = {
        "name": name,
        "seconds": round(len(pcm) / SR, 6),
        "channels": 2 if music_clip else 1,
        "sampleRate": SR,
        "bitDepth": 16,
        "peakDBFS": round(20 * np.log10(np.max(np.abs(reconstructed))), 2),
        "rmsDBFS": round(20 * np.log10(max(rms, 1e-9)), 2),
        "clippedSamples": int(np.count_nonzero(np.abs(pcm.astype(np.int32)) >= 32767)),
        "firstSampleMax": round(float(np.max(np.abs(reconstructed[0]))), 8),
        "lastSampleMax": round(float(np.max(np.abs(reconstructed[-1]))), 8),
        "loopBoundaryStep": round(edge, 8),
        "maxAdjacentStep": round(float(np.max(differences)), 8),
        "finite": bool(np.isfinite(source).all()),
    }
    assert report["clippedSamples"] == 0 and report["finite"], name
    if music_clip:
        assert 48 <= report["seconds"] <= 64, name
        # The wrap is an ordinary adjacent sample interval, not silence. Its
        # step must fit the track's natural derivative rather than equal zero.
        typical_large_step = float(np.quantile(differences, .999))
        report["loopStepWithinNaturalRange"] = edge <= typical_large_step * 1.25 + .001
        report["adjacentStep99_9Percentile"] = round(typical_large_step, 8)
        assert edge < .04 and report["loopStepWithinNaturalRange"], f"Loop discontinuity: {name} {edge}"
    else:
        assert report["firstSampleMax"] == 0 and report["lastSampleMax"] == 0, name
        assert .06 <= report["seconds"] <= 3.0, name
    return report


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    reports = []
    compositions = [
        ("Music_Menu", 70, [[62,65,69,76], [58,62,65,69], [53,57,60,65], [60,64,67,74], [62,65,69,72], [55,58,62,65], [58,62,65,69], [57,60,64,69]],
         [[(0,74,1.5),(2,77,1.5)],[(.5,81,1),(2,77,1),(3,74,.75)],[(0,72,1.5),(2,77,1.5)],[(.5,76,1),(2,74,1),(3,72,.75)],[(0,74,1),(1.5,77,1),(3,81,.8)],[(0,79,1.5),(2,77,1.5)],[(.5,74,1),(2,77,1)],[(0,76,1),(1.5,72,1),(3,69,.8)]], "menu"),
        ("Music_Forest", 80, [[55,59,62,69], [60,64,67,71], [52,55,59,62], [57,62,66,69]],
         [[(0,71,.65),(1,74,.65),(2,76,.65),(3,74,.7)],[(0,72,1),(1.5,76,.65),(2.5,79,1)],[(0,74,.8),(1,71,.8),(2.5,67,1)],[(0,69,.75),(1.5,74,1),(3,66,.8)]], "forest"),
        ("Music_Canyon", 80, [[52,55,59,66], [48,52,55,62], [55,59,62,69], [50,54,57,64]],
         [[(0,76,.6),(.75,79,.6),(1.5,83,.85),(3,79,.8)],[(0,79,.75),(1.5,76,.75),(2.5,74,1)],[(0,74,.6),(.75,79,.6),(2,83,1.3)],[(0,81,.65),(1,78,.65),(2,76,.65),(3,74,.8)]], "canyon"),
        ("Music_Snow", 64, [[50,53,57,64], [46,50,53,57], [43,46,50,57], [45,48,52,59]],
         [[(0,74,1.6),(2.5,77,1)],[(0,81,1.4),(2,77,1.5)],[(.5,79,1),(2,74,1.5)],[(0,76,1.3),(2,72,1.2),(3.5,69,.5)]], "snow"),
    ]
    for arguments in compositions:
        name, samples = music(*arguments)
        reports.append(write_wav(name, samples, True))
        print(f"{name}: {reports[-1]['seconds']:.2f}s, {reports[-1]['peakDBFS']} dBFS, boundary {reports[-1]['loopBoundaryStep']}")
    for name, samples in make_sfx().items():
        reports.append(write_wav("SFX_" + name, samples))
        print(f"SFX_{name}: {reports[-1]['seconds']:.2f}s")
    REPORT.write_text(json.dumps({"generator": "Tools/Audio/generate_audio.py", "seed": 481516, "assetCount": len(reports), "allPassed": True, "assets": reports}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Validated {len(reports)} original audio assets; report: {REPORT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
