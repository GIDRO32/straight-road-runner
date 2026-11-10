# Adding content

All characters, stages, enemies, bosses and obstacles are **ScriptableObject assets** in `Assets/Content/`.
Create one with **right-click ▸ Create ▸ SRR ▸ …**. It is registered in `Assets/Resources/ContentDatabase.asset`
automatically (or run **Tools ▸ Straight Road Runner ▸ Rebuild Content Database**; it also logs warnings for missing prefabs, duplicate ids, etc.).

Every asset has an `id` (auto-filled from the file name). Saves and leaderboards store this id, so **don't change it after release**.
Untick `includeInDemo` to hide something without deleting it.

## Character
1. Make the gameplay prefab (tag `Player`, with `Movement` + `PlayerData`), e.g. in `Assets/Prefabs/Players`.
   For a unique moveset, subclass `Movement` (e.g. `NinjaMovement : Movement`), override `Update`, `PerformSuperJump`, `OnPunchHit`, … and use it on the prefab instead.
2. Create ▸ SRR ▸ Character. Set prefab, icon (selection tile + HUD), select artwork (Character Select), menu art (animated, title screen only),
   bio, profile (Speed/Jump grade F–S, Size XS–XL, Weight), gameplay stats and moveset.
   The stats are applied to the prefab when it spawns.

## Enemy
1. Make the prefab (tag `Enemy`, Rigidbody2D, Collider2D). Put a script on it that derives from `EnemyBase`:
   ```csharp
   public class Slime : EnemyBase
   {
       void FixedUpdate()
       {
           if (player == null || isDefeated) return;
           // movement / attacks here, call PlayAttackSound() when attacking
       }
       protected override void OnHit(Collider2D source) { /* flash, knockback... */ }
   }
   ```
   Hits, health, score, sounds and the fade-out on death are handled by `EnemyBase`.
   Enemies that live on platforms (like the **Smoke Hound**) use a kinematic Rigidbody2D, a trigger **body** collider
   (takes punches, hurts the player) and a trigger **Feet** collider on a child (platform recognition); they find platforms
   with `FindObjectsOfType<Platform>()`. `SmokePuff.Spawn(position)` plays a code-built smoke burst (no asset needed).
2. Create ▸ SRR ▸ Enemy. Set prefab, stats (health, contact damage/stun, score), sounds, spawn weight and Y range.
3. Add it to the `enemies` list of the stage(s) it appears on.

## Boss
Same as an enemy, but the script derives from `BossBase` and the asset is Create ▸ SRR ▸ Boss.
Bosses don't go in stage lists: `BossDirector` picks a random one during any run (respecting `allowedStages`
and `minRunTimeSeconds`), pauses normal enemies and difficulty until it is defeated, then resumes.
Tune timing on the `BossDirector` component (added automatically to the StageControl object).

## Platform types
Assets in `Assets/Content/Platforms`. Create ▸ SRR ▸ Platform:
- **Look**: `sprite`, or `idleFrames` + fps for an animated platform.
- **Mechanic**: `Regular`, `Fragile` (breaks after `breakTime` seconds of standing on it) or
  `Lava` (after `heatUpTime`, hits like an enemy: `staminaDamage` + `stunDuration`, again every `hitInterval`;
  plays `hitFrames` once per hit).
- `allowObstacles`: untick to keep walls/signs off this type.
- New mechanic (ice, bouncy...): add a value to `PlatformMechanicType`, a `PlatformMechanic` subclass in
  `Assets/Codes/Platforms`, and a case in `Platform.AddMechanic`.

All types share the stage's base platform prefab (`Cloud.prefab`: collider, scrolling, particles); a stage can use its own
with `platformPrefab`.

## Obstacle
Assets in `Assets/Content/Obstacles`, prefabs in `Assets/Prefabs/Obstacles`. Create ▸ SRR ▸ Obstacle:
- `prefab`, `offset` (position on the platform, platform-local), `slot` (two obstacles in the same slot never share a platform).
- Walls: `hitsToBreak` x–y (random per wall, e.g. 1–2). 0 = the prefab's `WallShatter` min/max.
- The same prefab can back several obstacles (e.g. **Wall** = 1 hit, **Hardened Wall** = 1–2 hits).

## Stage
1. Make the scene (duplicate `SketchyRoad`, change backgrounds/decorations) and add it to **Build Settings**.
   Several stages may share one scene (Volcanic Desert uses `SketchyRoad` for now).
2. Create ▸ SRR ▸ Stage. Set scene name, artwork, description, intro title, music folder and enemies, then:
   - **Platforms**: `basePlatform` (the normal one) and `specialPlatforms`, each with a `chance` range.
     Each new platform rolls the special chances (random order, first hit wins), otherwise it's the base platform.
   - **Obstacles**: `obstacleSpawns`, each with a `chance` range, rolled per platform.
   - **Pacing & Difficulty**: platform/enemy intervals and chances, max enemies, endgame speed-up.
   - **Title Screen**: `menuBackgroundPrefab` (+ scroll speed and tile width) and `titleLogo`, shown on the main menu
     while this stage is selected. Empty = the menu's default background / logo.
3. Create a leaderboard in the Unity Cloud dashboard with the stage's id (or set `leaderboardId`).
4. Load it from the menu with `MenuControl.PlayStage("stage_id")` or `GameSession.LoadStage(stage)`.

**Chaos Mode** (`ChaosMode.asset`) automatically pulls enemies, special platforms and obstacles from every other stage
(plus other stages' base platforms at `otherBasePlatformChance`), so it needs no maintenance.
It uses the `SketchyRoad` scene; point it at any stage scene.

## Enemies per stage
List the stage's enemy types in `enemies`. After `pacing.firstEnemyDelay` the first enemy ALWAYS spawns (no chance roll).
Then every `enemyInterval` a wave: up to `maxEnemies` rolls of `enemyChance`, each spawn a random type from the list
(weighted by the enemy asset's `spawnWeight`, all 1 = equal odds), `enemyWaveSpawnDelay` seconds apart.
A wave stops once `maxEnemies` are alive. Interval, chance and max enemies are difficulty ranges.

## Difficulty
Every value that ramps up is a **range**: `start` → `limit`, moving by `step` (negative = goes down, 0 = constant).
Every `pacing.difficultyInterval` seconds, `DifficultyManager` picks ONE random range that hasn't hit its limit and steps it.
That covers the stage's pacing ranges and every special platform and obstacle chance.
Once all are at their limits, the endgame starts: platforms speed up by `endgameSpeedStep` every `endgameInterval` seconds.
Each nudge is logged in the Console (`DifficultyManager > Log Changes`).

## Leaderboards (Unity Gaming Services)
- Link the project: **Edit ▸ Project Settings ▸ Services**, then select or create a Unity Cloud project.
- In the dashboard (Leaderboards), create the board `global` plus one per stage id (`sketchy_road`, `chaos_mode`, …),
  sort descending, keep the best score.
- On game over, the score is sent to the stage board and `global`, along with the character, stage, run time and bosses defeated.
  Players sign in anonymously; scores earned offline are queued and uploaded later.
- To read boards for UI: `LeaderboardManager.Instance.GetStageTopAsync(stage)` / `GetGlobalTopAsync()` return
  `LeaderboardRecord`s with `rank`, `playerName`, `score`, `CharacterName`, `CharacterIcon` and `StageName`.

## Stage Select panel
Open `MainMenu` and run **Tools ▸ Straight Road Runner ▸ Build Stage Select Panel**, then save the scene.
It creates `PanelCollection/StageSelect` plus the **Select Stage** button (bottom right, above the version text),
styled with the existing menu sprites and font. Run it again to rebuild (it replaces the old one; Ctrl+Z undoes it).
- Left: a grid of stages from the ContentDatabase (Chaos Mode last), scrollable once there are many.
- Right: **Artwork** shows `artwork` + name; **Info** is a scrollable list with the stage `description`, its enemies and its obstacles (icon, name, description).
- The panel's **Play** button, and optionally the main menu Play button, load the selected stage. The choice is remembered.
Tweak sizes, colors and fonts freely after building; the `StageSelectPanel` component only needs its references.

## Character Select panel
Open `MainMenu` and run **Tools ▸ Straight Road Runner ▸ Build Character Select Panel**, then save the scene.
It creates `PanelCollection/CharacterSelect` plus the **Character** button left of the Stage button, and can delete the old `CharacterSelectionScreen`.
- Left: grid of character `icon`s (no text).
- Middle: the character's `selectArtwork` and name. (The animated `menuArt` is only used on the title screen.)
- Right: info wall with **Bio** (`bio` text), **Stats** and **Moveset** (`moveset`: name, input, description, icon).
- Stats list: Speed (F–S), Jump (F–S), Size (XS–XL), Weight (Light/Medium/Heavy) from `profile`, and Stamina from `stats.maxStamina`.
  Labels can be renamed on the `CharacterSelectPanel` component.
- Picking a character saves it immediately and updates the main menu art; **Select** just closes the panel.

## TextMeshPro
All UI text uses TextMeshPro (`TMP_Text` fields in code). To convert legacy `Text` in scenes/prefabs, run
**Tools ▸ Straight Road Runner ▸ Convert Legacy Text to TextMeshPro** (it asks you to import TMP Essential Resources first if needed).
It creates a TMP font asset next to each legacy font (`<Font> SDF.asset`), turns `Outline` effects into outline material presets,
and re-links every script field and button that pointed at the old Text. Use those font assets/materials for new text.
