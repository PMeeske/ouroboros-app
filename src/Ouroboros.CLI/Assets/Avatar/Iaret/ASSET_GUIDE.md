# Iaret Avatar Assets

Place your prepared avatar images here with the following filenames.
The viewer has three visual layers: **Portrait**, **Full Body**, and **Holographic Wireframe**.

---

## Portrait Mode — 5 expression images (bust/close-up)

| Filename              | State           | Description                                    |
|-----------------------|-----------------|------------------------------------------------|
| `idle.png`            | Idle / Default  | Regal, composed — forward gaze, watchful eyes  |
| `listening.png`       | Listening       | Attentive, slightly turned/tilted              |
| `thinking.png`        | Processing      | Contemplative, softened eyes                   |
| `speaking.png`        | Speaking        | Animated, direct gaze, commanding warmth       |
| `encouraging.png`     | Warm / Maternal | Gentle smile, nurturing presence               |

## Full-Body Mode — 5 turnaround angles

| Filename                      | Angle         | Description                           |
|-------------------------------|---------------|---------------------------------------|
| `fullbody_front.png`          | Front         | Full standing pose, forward-facing    |
| `fullbody_threequarter.png`   | ¾ Right       | Three-quarter view, slight right turn |
| `fullbody_side.png`           | Profile Right | Side profile, facing right            |
| `fullbody_back.png`           | Back          | Rear view, costume detail             |
| `fullbody_sideleft.png`       | Profile Left  | Side profile, facing left             |

## Holographic Wireframe — 7 overlay images

These overlay on top of the solid renders when Iaret is **thinking/processing**,
creating a deconstructed digital consciousness effect with scanlines and cyan glow.

| Filename                  | Layer           | Description                           |
|---------------------------|-----------------|---------------------------------------|
| `holo_portrait.png`       | Portrait bust   | Wireframe head/bust overlay           |
| `holo_front.png`          | Full-body front | Wireframe front full body             |
| `holo_threequarter.png`   | Full-body ¾     | Wireframe ¾ angle                     |
| `holo_side.png`           | Full-body side  | Wireframe profile right               |
| `holo_back.png`           | Full-body back  | Wireframe rear view                   |
| `holo_sideleft.png`       | Full-body left  | Wireframe profile left                |

**Total: 17 image slots** (5 portrait + 5 full-body + 7 holographic)

---

## Mapping Your Reference Images

### Sets 1 & 2 (10 close-up bust renders)
Pick the best expression from each set for the 5 portrait files.

### Set 3 (5 full-body turnaround)
- Front standing pose -> `fullbody_front.png`
- ¾ view             -> `fullbody_threequarter.png`
- Side profile right  -> `fullbody_side.png`
- Back view           -> `fullbody_back.png`
- Side profile left   -> `fullbody_sideleft.png`

### Set 4 (5 full-body turnaround — alternate)
Use as replacements for Set 3 if the angles/quality are better.

### Set 5 (character sheet — 3x3 grid with wireframes)
Extract the **holographic wireframe** columns from the character sheet:
- Top row wireframes (full body)  -> `holo_front.png`, `holo_threequarter.png`, `holo_side.png`
- Middle row wireframes (torso)   -> (alternate `holo_portrait.png` if preferred)
- Bottom row wireframes (head)    -> `holo_portrait.png`
- Back/left wireframes from the rear angles -> `holo_back.png`, `holo_sideleft.png`

### Additional full-body renders (rear ¾, front ¾, direct front)
Use the sharpest versions for `fullbody_front.png` and `fullbody_threequarter.png`.

---

## Image Specifications

- **Format**: PNG (transparency works well for holographic overlays)
- **Portrait**: 1:1 recommended (512x512 min, 1024x1024 ideal)
- **Full-body**: tall aspect ratio (768x1024 or 512x768)
- **Holographic**: same dimensions as corresponding solid render;
  transparent/dark background preferred (uses `mix-blend-mode: screen`)
- **Style**: Consistent Iaret identity — violet/purple, gold accents, cosmic aesthetic

---

## How It Works

```bash
ouroboros --avatar                    # launch on port 9471
ouroboros --avatar --avatar-port 8080 # custom port
```

### Automatic behavior
- **Idle**: regal portrait crossfade (portrait) or auto-turnaround (full-body)
- **Listening**: attentive expression / ¾ angle
- **Thinking**: holographic wireframe fades in over the solid render with scanlines,
  glitch flicker, and cyan data stream text — she deconstructs into digital consciousness
- **Speaking**: animated expression with golden mouth glow pulse
- **Encouraging**: warm smile, triggered by maternal/warm moods

### Holographic thinking mode effects
- Wireframe overlay fades in at 85% opacity with `screen` blend mode
- Scanline overlay drifts downward (4px cyan stripes)
- Periodic glitch flicker (2px horizontal shift, cyan/violet flash)
- Data stream text cycles consciousness-themed phrases
- Aura shifts from violet to cyan
- Gold ring, name text, and mood bar shift to cyan
- "HOLOGRAPHIC" badge pulses in top-left corner

---

## Keyboard Shortcuts

| Key        | Portrait Mode            | Full-Body Mode            |
|------------|--------------------------|---------------------------|
| `1`–`5`    | Switch expression state  | Switch expression state   |
| `F`        | Toggle to Full Body      | Toggle to Portrait        |
| `H`        | Toggle holographic       | Toggle holographic        |
| `Q/W/E/R/T`| —                       | Jump to angle 0–4         |
| `←` / `→` | —                        | Rotate angle              |
| `Space`    | —                        | Resume auto-turnaround    |
