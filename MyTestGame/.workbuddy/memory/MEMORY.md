# MyTestGame 项目长期笔记

## 项目真实形态（重要）
- 仓库根：`D:/UnityProject/miniGame`（git 仓库），Unity 工程在 `MyTestGame/`，分支 `main`。
- **README.md 已严重过期**：它仍描述"基于 GameFramework 的塔防 Demo"，但塔防玩法代码已被完全移除（脚本中已无 tower/turret 相关内容）。
- 当前实际是一款 **2D/俯视角丧尸生存（幸存者类 roguelite）游戏**，仍以 GameFramework（GF）为底层框架（流程、资源、UI、事件、对象池等）。
- Unity 版本：6000.3.25f1（Unity 6）。Assets 下约 990+ 个 .cs。

## 目录结构（已重构）
- `Assets/GameMain/Scripts/`
  - `Framework/`（Procedure 流程，GF 接入）
  - `Game/`（玩法，45 个脚本）：`Player/`（SurvivorMovement/Dash/Health/MeleeAttack/WeaponVisual/ArrowPool）、`Enemy/`（ZombieChaser/Champion/Spawner/GridPathfinder/DirectionalAnimator/WaveTrigger/RoomEncounterController）、`Run/`（RunState、ExperiencePickup、ScrapPickup、HealthPickup、CurseAltar、RunRoomPlanner、SurvivorMetaProgress、EndlessLeaderboard）、`Level/`（LevelFlowController、EndlessModeController、SafeDoor、ZoneGate、RoomLayoutVariant）、`Audio/`、`Camera/`、`Map/`、`Event/`
  - `UI/`（`Battle/` ZombieRunUI、`Menu/` ZombieUiFlow/CommandRoomMenuView/StartingWeaponPicker/EndlessLeaderboardUI/SurvivorMetaShop、`Common/UiButtonSound`）
  - `Data/Configs/`（EndlessModeConfig 等）
- `Assets/GameMain/Res/`：Characters/Zombie（含 Crawler 四向动画）、Weapons、Drops、Effects、Interactables、Map、Materials、UI、Fonts(NotoSansSC SDF)
- `Assets/GameMain/Prefabs/`：Characters、Enemies、Encounters、Drops、Effects、Gameplay、Interactables、LevelMarkers、UI、Weapons
- `Assets/GameMain/Scenes/`：`GameStart/`(GameStart、BeforeStreetBlock)、`Menu/`、`Level/`（ZombieLevel01~06 + ZombieLevel99 无尽模式）
- 骨骼动画：`Assets/Spine`（spine-unity 4.1 运行时）

## 编辑器工具链
- 大量编辑器 Builder/Verification 脚本位于 `Assets/GameMain/Scripts/Editor/Game/`（按 UI/Progression/Audio 等分类），用于程序化生成 UI 预制体、关卡、HUD，并配套 Verifier 做校验。
- `Assets/Editor/MCPForUnityToolsMenu.cs`：Unity MCP 集成菜单。

## 待办 / 注意
- 工作区有大量未提交改动（约 400 个删除 + 27 修改 + 64 新增），是一次"脚本目录重构 + 塔防→丧尸生存玩法切换"，尚未 commit。
- `README.md` 需要按新玩法重写。
- `Assets/MyResCapity`（README 提到的 3GB 美术资源）已不在工程中。
