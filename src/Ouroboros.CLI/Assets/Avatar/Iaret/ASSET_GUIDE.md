# Iaret Avatar Assets

Place your prepared avatar images here with the following filenames.
The viewer has two modes: **Portrait** (circular bust) and **Full Body** (turnaround).

## Portrait Mode — 5 expression images (bust/close-up)

| Filename              | State           | Which Image to Use                                      |
|-----------------------|-----------------|---------------------------------------------------------|
| `idle.png`            | Idle / Default  | Regal, composed — forward gaze, watchful eyes           |
| `listening.png`       | Listening       | Attentive gaze, slightly turned/tilted, open posture    |
| `thinking.png`        | Processing      | Contemplative, eyes softened, thoughtful expression     |
| `speaking.png`        | Speaking        | Animated, confident, commanding warmth, direct gaze     |
| `encouraging.png`     | Warm / Maternal | Gentle smile, soft golden glow, nurturing presence      |

## Full-Body Mode — 5 turnaround angles

| Filename                      | Angle           | Which Image to Use                             |
|-------------------------------|-----------------|------------------------------------------------|
| `fullbody_front.png`          | Front           | Full standing pose, direct forward-facing      |
| `fullbody_threequarter.png`   | ¾ Right         | Three-quarter view, slight right turn          |
| `fullbody_side.png`           | Profile Right   | Side profile, facing right                     |
| `fullbody_back.png`           | Back            | Rear view showing costume/wing detail          |
| `fullbody_sideleft.png`       | Profile Left    | Side profile, facing left                      |

## Mapping Your 15 Reference Images

From the three sets of reference images you prepared:

**Set 1 (5 close-up bust images — varied expressions)**
- Image 1 (frontal, regal, cosmic hood)          -> `idle.png`
- Image 2 (profile, attentive)                   -> `listening.png`
- Image 3 (contemplative, softened eyes)          -> `thinking.png`
- Image 4 (animated, direct eye contact)          -> `speaking.png`
- Image 5 (gentle, warm expression)               -> `encouraging.png`

**Set 2 (5 close-up bust images — different angles/poses)**
Use as higher-quality replacements for Set 1 or pick the best from each set.
The viewer crossfades between states, so consistency of art style, lighting,
and color palette across all 5 portrait files matters more than resolution.

**Set 3 (5 full-body turnaround renders)**
- Image 1 (front, standing, direct)              -> `fullbody_front.png`
- Image 2 (¾ left view)                          -> `fullbody_threequarter.png`
- Image 3 (profile right)                        -> `fullbody_side.png`
- Image 4 (back view)                            -> `fullbody_back.png`
- Image 5 (profile left)                         -> `fullbody_sideleft.png`

## Image Specifications

- **Format**: PNG (transparency optional — cosmic background is fine)
- **Resolution**: 512x512 minimum, 1024x1024 recommended for portrait;
  portrait images can be any aspect ratio but 1:1 is ideal for the circular frame
- **Full-body**: taller aspect ratio preferred (e.g. 768x1024 or 512x768)
- **Style**: Consistent Iaret identity — violet/purple palette, gold accents,
  ankh motifs, cosmic/digital aesthetic, Egyptian serpent goddess

## How It Works

```bash
ouroboros --avatar                    # launch with default port 9471
ouroboros --avatar --avatar-port 8080 # custom port
```

1. A browser tab opens showing Iaret in a cosmic frame
2. **Portrait mode** (default): circular bust frame, crossfades between 5 expressions
3. **Full-body mode**: press `F` to toggle — tall rounded frame with auto-turnaround
4. The avatar state tracks the CLI in real-time via WebSocket

### Portrait mode effects
- Smooth crossfade transitions between states (idle/listening/thinking/speaking/encouraging)
- Breathing animation, aura pulse, eye shimmer, blink simulation
- Speaking glow indicator, gold ring frame with mood-responsive luminance
- Floating ankh/star particles on cosmic star-field background
- Consciousness shifts change aura color/intensity dynamically

### Full-body mode effects
- Auto-turnaround: cycles through 5 angles (4s per angle) when idle
- Snaps to front when speaking/thinking, ¾ angle when listening
- Ground glow beneath Iaret's feet, vertical aura ellipse
- Blink/eye-shimmer automatically disabled for back/side angles
- Arrow keys or Q/W/E/R/T for manual angle control

## Keyboard Shortcuts

| Key        | Portrait Mode         | Full-Body Mode            |
|------------|-----------------------|---------------------------|
| `1`–`5`    | Switch state          | Switch state              |
| `F`        | Toggle to Full Body   | Toggle to Portrait        |
| `Q/W/E/R/T`| —                    | Jump to angle 0–4         |
| `←` / `→` | —                     | Rotate angle              |
| `Space`    | —                     | Resume auto-turnaround    |
