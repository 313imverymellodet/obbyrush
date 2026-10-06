# OBBY RUSH

3D obstacle-course speedruns in the browser (Unity 6 WebGL), built with the KayKit Platformer, Adventurers and Skeletons packs.

**The hook: REWIND.** The last 3 seconds of your run are recorded. Hold REWIND (Shift / Z, or the << touch button) to scrub back at 2x speed with a VHS effect and undo a missed jump. The meter allows 1.5 s of rewinding and refills over about 14 s. The run clock never rewinds, so a rewind still costs time. When you're plunging off the course with rewind in the tank, solo runs drop into slow motion and flash HOLD REWIND! (live races keep real time). Results show your rewind count; a NO-REWIND run is the bragging right. Analytics events: `rewind_used`, `rewind_save`, `finish_no_rewind`, `finish_rewinds`.

- **3 courses**: Sky Garden (easy), Sunset Tower (medium), Storm Peak (hard), with checkpoints, springs, conveyors, crumbling tiles, sweepers and moving platforms.
- **Platformer feel**: coyote time, jump buffering, variable jump height, an air jump, moving-platform carry.
- **Ghosts**: race your personal best or the world record's replay (10 Hz, 8 bytes per sample).
- **Live races**: up to 8 real players per room (quick match or private room codes) over WebSocket.
- **Leaderboards** per course, validated server-side against a run token and the replay length.
- **10 skins**, unlocked with the 9 hidden stars (or a rewarded ad on portals that provide one).
- Touch (floating stick + jump + drag camera) and keyboard (WASD/arrows, Space, Shift/Z rewind, Q/E, R).

Backend: the shared `orbyt` server (`server/obby.js`).

## Build

```
"C:/Program Files/Unity/Hub/Editor/6000.6.3f1/Editor/Unity.exe" -batchmode -nographics -projectPath unity -executeMethod ObbyBuild.WebGL -quit -logFile build.log
node tools/serve.mjs 8098
```

`ObbyBuild.WebGLDev` makes a development build with stack traces in `dist-dev/`. Dev URL flags: `?dev=1`, `fresh=1`, `course=0..2`, `stars=9`.

Assets: KayKit by Kay Lousberg (CC0).
