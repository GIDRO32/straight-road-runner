# Adding content

All characters, stages, enemies, bosses and obstacles are **ScriptableObject assets** in `Assets/Content/`.
Create one with **right-click ▸ Create ▸ SRR ▸ …**. It is registered in `Assets/Resources/ContentDatabase.asset`
automatically (or run **Tools ▸ Straight Road Runner ▸ Rebuild Content Database**; it also logs warnings for missing prefabs, duplicate ids, etc.).

Every asset has an `id` (auto-filled from the file name). Saves and leaderboards store this id, so **don't change it after release**.
Untick `includeInDemo` to hide something without deleting it.

## Character
1. Make the gameplay prefab (tag `Player`, with `Movement` + `PlayerData`), e.g. in `Assets/Prefabs/Players`.
   For a unique moveset, subclass `Movement` (e.g. `NinjaMovement : Movement`), override `Update`, `PerformSuperJump`, `OnPunchHit`, … and use it on the prefab instead.
2. Create ▸ SRR ▸ Character. Set prefab, icon (selection button + HUD), portrait, menu art, bio, stats and moveset.
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
2. Create ▸ SRR ▸ Enemy. Set prefab, stats (health, contact damage/stun, score), sounds, spawn weight and Y range.
3. Add it to the `enemies` list of the stage(s) it appears on.

## Boss
Same as an enemy, but the script derives from `BossBase` and the asset is Create ▸ SRR ▸ Boss.
Bosses don't go in stage lists: `BossDirector` picks a random one during any run (respecting `allowedStages`
and `minRunTimeSeconds`), pauses normal enemies and difficulty until it is defeated, then resumes.
Tune timing on the `BossDirector` component (added automatically to the StageControl object).

## Obstacle
1. Make the prefab (e.g. a wall with `WallShatter`).
2. Create ▸ SRR ▸ Obstacle, then add it to a stage's `obstacles` list.
   It spawns on platforms at `Platform.obstacleAnchor` using the difficulty-scaled `Platform.wallSpawnChance`.
   Stages with no obstacles keep using the Platform prefab's built-in wall.

## Stage
1. Make the scene (duplicate `SketchyRoad`, change backgrounds/decorations) and add it to **Build Settings**.
2. Create ▸ SRR ▸ Stage. Set scene name, artwork, description, intro title, music folder, enemies and obstacles.
3. Create a leaderboard in the Unity Cloud dashboard with the stage's id (or set `leaderboardId`).
4. Load it from the menu with `MenuControl.PlayStage("stage_id")` or `GameSession.LoadStage(stage)`.

**Chaos Mode** (`ChaosMode.asset`) automatically pulls enemies and obstacles from every other stage, so it needs no maintenance.
It uses the `SketchyRoad` scene; point it at any stage scene.

## Leaderboards (Unity Gaming Services)
- Link the project: **Edit ▸ Project Settings ▸ Services**, then select or create a Unity Cloud project.
- In the dashboard (Leaderboards), create the board `global` plus one per stage id (`sketchy_road`, `chaos_mode`, …),
  sort descending, keep the best score.
- On game over, the score is sent to the stage board and `global`, along with the character, stage, run time and bosses defeated.
  Players sign in anonymously; scores earned offline are queued and uploaded later.
- To read boards for UI: `LeaderboardManager.Instance.GetStageTopAsync(stage)` / `GetGlobalTopAsync()` return
  `LeaderboardRecord`s with `rank`, `playerName`, `score`, `CharacterName`, `CharacterIcon` and `StageName`.
