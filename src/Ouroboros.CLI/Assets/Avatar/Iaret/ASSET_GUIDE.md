# Iaret Avatar Assets

Place your prepared avatar images here with the following filenames.
Each file maps to a specific avatar state driven by the persona's mood and presence.

## Required Assets (5 core states)

| Filename              | State           | Which Image to Use                                      |
|-----------------------|-----------------|---------------------------------------------------------|
| `idle.png`            | Idle / Default  | Regal, composed — forward gaze, watchful eyes           |
| `listening.png`       | Listening       | Attentive gaze, slightly turned/tilted, open posture    |
| `thinking.png`        | Processing      | Contemplative, eyes softened, thoughtful expression     |
| `speaking.png`        | Speaking        | Animated, confident, commanding warmth, direct gaze     |
| `encouraging.png`     | Warm / Maternal | Gentle smile, soft golden glow, nurturing presence      |

## Mapping Your 10 Reference Images

From the two sets of reference images you prepared:

**Set 1 (first 5 images)**
- Image 1 (frontal, regal, cosmic hood)          -> `idle.png`
- Image 2 (profile, attentive)                   -> `listening.png`
- Image 3 (contemplative, softened eyes)          -> `thinking.png`
- Image 4 (animated, direct eye contact)          -> `speaking.png`
- Image 5 (gentle, warm expression)               -> `encouraging.png`

**Set 2 (second 5 images)** — use as higher-quality replacements or combine
artistically with Set 1 for best results. The viewer crossfades between states,
so consistency of art style, lighting, and color palette across all 5 files
is more important than resolution.

## Image Specifications

- **Format**: PNG (transparency optional — cosmic background is fine)
- **Resolution**: 512x512 minimum, 1024x1024 recommended
- **Aspect ratio**: 1:1 (square) for consistent circular-frame rendering
- **Style**: Consistent Iaret identity — violet/purple palette, gold accents,
  ankh motifs, cosmic/digital aesthetic, Egyptian serpent goddess

## How It Works

1. Run `ouroboros --avatar` to launch the interactive avatar viewer
2. A browser tab opens showing Iaret in a cosmic frame
3. The avatar crossfades between states as Iaret listens, thinks, and speaks
4. Ambient effects: breathing animation, aura pulse, eye shimmer, floating ankh particles
5. Consciousness shifts from the persona engine change the aura color and intensity

## Mood-to-State Mapping

The avatar system automatically selects the appropriate image based on:

1. **AgentPresenceState** (primary): Idle, Listening, Processing, Speaking
2. **MoodState** (secondary modifier): warm/encouraging/maternal moods use `encouraging.png`
3. **Consciousness shifts** (tertiary): emotional changes from the PersonalityEngine
   dynamically adjust the aura glow and energy level

See `AvatarState.cs` for the full mapping logic.

## Keyboard Shortcuts (in the viewer)

| Key | State         |
|-----|---------------|
| `1` | Idle          |
| `2` | Listening     |
| `3` | Thinking      |
| `4` | Speaking      |
| `5` | Encouraging   |
