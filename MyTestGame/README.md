# MyTestGame —— 丧尸生存（幸存者类 Roguelite）

## 简介

这是一款基于开源框架 [GameFramework][1]（以下简称 GF）开发的 **2D 俯视角丧尸生存游戏**，玩法属于幸存者类（Survivor-like）+ Roguelite：在一局游戏中边打边成长，清剿街区丧尸、解锁安全门并推进到下一关，局外用积累的幸存者点数解锁新的武器与词条。

> 本仓库的前身是 `TowerDefense-GameFramework-Demo`（用 GF 重写 Unity 官方 Tower Defense Template 的塔防 Demo）。塔防玩法已整体移除，现为丧尸生存玩法；代码与资源均已完成相应清理与重构。

仓库名为 `miniGame`（git 仓库），Unity 工程位于仓库的 `MyTestGame/` 目录下。

## 版本信息

| 项 | 版本 / 说明 |
| :--- | :--- |
| Unity | **6000.3.25f1（Unity 6）** |
| GameFramework | 源码内嵌于 `Assets/GameFramework`，随工程一起版本管理 |
| spine-unity Runtime | 4.1（`Assets/Spine`） |
| 代码规模 | `Assets` 下 988 个 `.cs`，其中业务脚本 `Assets/GameMain/Scripts` 192 个 |
| 工程根 | 仓库 `miniGame`，Unity 工程位于 `MyTestGame/` |

## 工程结构

```text
MyTestGame/
├── Assets/
│   ├── GameFramework/          # GF 框架源码与 Unity 接入层（Runtime / Editor）
│   ├── GameMain/               # 游戏业务与资源
│   │   ├── Scripts/
│   │   │   ├── Framework/      # GF 接入：Procedure 流程、Base、Sound、Event 等
│   │   │   ├── Game/           # 玩法核心（45 个脚本）
│   │   │   │   ├── Player/     # SurvivorMovement / Dash / Health / MeleeAttack / WeaponVisual / ArrowPool
│   │   │   │   ├── Enemy/      # ZombieChaser / Champion / Spawner / GridPathfinder / DirectionalAnimator / WaveTrigger / RoomEncounterController
│   │   │   │   ├── Run/        # RunState、经验/废料/生命拾取、诅咒祭坛、房间规划、局外成长、无尽排行
│   │   │   │   ├── Level/      # LevelFlowController、EndlessModeController、SafeDoor、ZoneGate、RoomLayoutVariant
│   │   │   │   └── Audio / Camera / Map / Event
│   │   │   ├── UI/             # ZombieUiFlow、战斗 HUD、章节/武器/商店/排行榜等界面逻辑
│   │   │   ├── Data/           # DataTable（DR* 行类）、Enums、Configs、Runtime 数据访问
│   │   │   └── Editor/         # 编辑器工具链：数据表生成、枚举生成、大量 Builder / Verification
│   │   ├── DataTables/         # 9 张数据表（Excel 风格 .txt + 导出的 .bytes）
│   │   ├── Configs/            # BuildInfo、BuildSettings、ResourceCollection / Builder / Editor 等
│   │   ├── Prefabs/            # Characters / Enemies / Encounters / Drops / Effects / Gameplay / Interactables / LevelMarkers / UI / Weapons
│   │   ├── Res/                # Characters / Drops / Effects / Fonts / Interactables / Map / Weapons / UI
│   │   ├── Scenes/             # GameStart、Menu、Level（ZombieLevel01~06 + ZombieLevel99）
│   │   ├── Localization/       # 简体中文 / 繁体中文 / 英文
│   │   └── Sounds/             # Music / UI / ZombieSurvivor（音乐与战斗音效）
│   ├── Spine/                  # spine-unity 4.1 运行时
│   └── Editor/                 # 工程级编辑器脚本（MCPForUnityToolsMenu 等）
├── Packages/                   # manifest.json、packages-lock.json
├── ProjectSettings/
├── Tools/                      # 辅助脚本
└── Backups/                    # 关卡等资源的备份快照
```

游戏启动流程（`Assets/GameMain/Scripts/Framework/Procedure`）：
`ProcedureLaunch → ProcedureSplash → ProcedureInitResources → ProcedureCheckVersion → ProcedureUpdateVersion → ProcedureUpdateResources → ProcedureCheckResources → ProcedurePreload → ProcedureMenu → ProcedureChangeScene`

## 玩法说明

### 核心循环

1. 从主菜单进入 **闯关模式**，选择章节；
2. 在 **起始武器选择** 界面确认本局武器后进入关卡；
3. 关卡内清剿区域丧尸（含精英与 Boss 遭遇），沿途拾取 **经验 / 废料 / 生命**；
4. 达成击杀目标与区域波次后 **安全门解锁**，进入出口即加载下一关；
5. 局内每次升级从若干词条中挑选，逐步成型流派；
6. 结算按清关数、击杀数、是否通关折算 **幸存者点数**，用于局外永久解锁。

### 操作

| 操作 | 按键 |
| :--- | :--- |
| 移动 | `WASD` / 方向键 |
| 冲刺 | `空格`（可配置；有无敌、冲刺减伤等词条可强化） |
| 攻击 | 鼠标左键（或配置的攻击键） |

### 武器

| 武器 | 说明 |
| :--- | :--- |
| 砍刀 Machete | 初始武器，近战 |
| 铁棍 IronBar | 初始武器，近战，范围/手感不同 |
| 重锤 HeavyHammer | 需在幸存者成长商店解锁 |
| 猎弓 HuntingBow | 需解锁，发射箭矢（含穿透、多箭、暴击等专属词条） |

近战武器配置见 `Assets/GameMain/Res/Weapons/Melee`，解锁类武器见 `Res/Weapons/SurvivorUnlocks`。

### 局内成长（Roguelite 词条）

词条数据表为 `Assets/GameMain/DataTables/RunUpgrade.txt`，共 **62 条**，按分支划分：

| 分支 | 条数 | 特点 |
| :--- | ---: | :--- |
| Common（通用） | 18 | 伤害/生命/移速/击退、低血量增益、契约类高风险高收益 |
| Quick（迅捷） | 10 | 攻速、冲刺冷却与距离、连击、残影、双倍伤害 |
| Frost（冰霜） | 11 | 冰冻概率/时长、对冰冻目标增伤、碎冰、霜冻传导、极寒领域 |
| Fire（烈焰） | 11 | 燃烧概率/时长、燃烧增伤、殉爆、火种扩散、焚尽斩杀 |
| Bow（弓） | 12 | 箭矢伤害/速度/射程、穿透、散射、鹰眼暴击、远猎 |

词条带有 `RequiredEffect` / `RequiredUnlock` 依赖：部分高阶词条需要先拿到前置词条（例如冰霜/烈焰系需要先有对应的触发概率，弓系卡牌需要先解锁弓卡牌）。

### 局外成长（幸存者点数）

`SurvivorMetaProgress` 使用 `PlayerPrefs` 持久化，商店共 4 项永久解锁：

| 项目 | 价格（幸存者点数） |
| :--- | ---: |
| 猎弓 | 120 |
| 重锤 | 90 |
| 弓卡牌 | 140 |
| 体魄（初始生命 +5%） | 80 |

### 敌人

| 预制体 | 说明 |
| :--- | :--- |
| `Zombie` | 基础丧尸 |
| `Zombie02_Maintenance` / `Zombie03_Civilian` / `Zombie04_Crawler` / `Zombie05_Scavenger` | 变体外观（含 Crawler 四向动画） |
| `ZombieFast` / `ZombieTank` / `ZombieExploder` | 快速 / 高血量 / 自爆变体 |
| `ZombieEliteHunter` | 精英遭遇 |
| `ZombieBossTyrant` | Boss 遭遇 |

敌人行为由 `ZombieChaser`（追击、寻路、攻击、爆炸）与 `ZombieChampion`（精英/Boss 的范围预警、击飞、弹幕）驱动；`ZombieSpawner` + `ZombieWaveTrigger` + `RoomEncounterController` 负责房间遭遇与波次编排，`ZombieGridPathfinder` 提供网格寻路。

### 关卡与章节

- 关卡场景：`Assets/GameMain/Scenes/Level/ZombieLevel01` ~ `ZombieLevel06`，外加 `ZombieLevel99`（无尽模式）；
- 每关由 `LevelFlowController` 驱动"清剿 → 解锁安全门 → 进入下一关"的最小可玩流程，支持"是否必须完成区域波次"等开关；
- 场景内分区由 `ZoneGate` / `RoomLayoutVariant` / `SafeDoor` 组织；
- 章节面板（`ZombieChapterSelect`）当前配置了 4 个章节入口。

### 无尽模式

`EndlessModeController` + `EndlessModeConfig`（`Assets/GameMain/Resources/EndlessModeConfig.asset`）控制难度曲线：

- 人口从 32 起步，每分钟 +12，上限 220；
- 生命/移速/伤害随分钟增长（生命另含二次项），精英与变体概率随时间提高；
- 首批 Boss 在 5 分钟出现，之后每 2 分钟一批，Boss 基础生命 15000 且随时间成长；
- 小批次持续增援 + 周期性大波（Horde）。

成绩记录在 **无尽生存榜**（`EndlessLeaderboard`），字段为：排名 / 初始武器 / 生存时间 / 本局击杀。

## 相关实现

### 数据配置

游戏内数据以 Excel 风格的 `.txt` 配置，导出为二进制 `.bytes` 在运行时加载。当前 9 张表：

`AssetsPath`、`PoolParam`、`RunUpgrade`、`Scene`、`Sound`、`SoundGroup`、`SoundPlayParam`、`UIForm`、`UIGroup`

生成代码位于 `Assets/GameMain/Scripts/Editor/DataTableGenerator`，导出表位于 `Assets/GameMain/DataTables`（每张表附带同名 `.txt` 便于查看）。

编辑器菜单：

- `Tools/Generate DataTables`：由 `.txt` 重新导出 `.bytes`；
- `Tools/Generate DataTable Code`：重新生成 `DR*` 行类；
- `Tools/Generate DataTable Enum`：重新生成 `EnumSound` / `EnumUIForm`；
- `Tools/Generate Localization Dictionary`：从预制体重新生成本地化字典。

### 本地化

利用 GF 的本地化模块 + 资源模块变体实现三语文案（简体中文 / 繁体中文 / 英文）。字典由 `Assets/GameMain/Scripts/Editor/LocalizationDictonaryGenerator` 扫描预制体上的 `LocalizeText.dictionaryKey` 生成；当前仍在使用的词条集中在设置界面（音量、语言、确认/取消等）。

### Spine 骨骼动画

`Assets/Spine` 为 spine-unity 4.1 运行时。Spine 资源遵循 Esoteric Software 的授权条款，Spine Editor 需另行购买。

### 编辑器工具链

项目大量依赖 **脚本化生成** 而非手工拖拽：`Assets/GameMain/Scripts/Editor/Game/` 下按 `Audio`、`Combat`、`Levels`、`Map`、`Progression`、`UI`、`Verification` 分类，成对存在 `*Builder.cs`（生成 UI 预制体、关卡、HUD 等）与 `*Verification.cs`（自动校验），便于重建与回归检查。

### 资源打包与热更新

`Assets/GameMain/Configs` 下为 GF 的资源打包配置（`ResourceCollection.xml` / `ResourceBuilder.xml` / `ResourceEditor.xml`）。启动时检测版本并更新基础资源；关卡资源按分组分包，进入关卡前按需下载。版本地址等配置见 `Configs/BuildInfo.txt`。

> 注意：`ResourceCollection.xml` 是 GF Resource Editor 的生成物。工程经历过大幅目录重构，**建议在 Unity 内用 Resource Editor 重新生成一次**，以纳入 `Prefabs/UI/Screens` 下新增的界面预制体与新图标。

## 注意事项

游戏在 Editor 下默认以 Editor 模式启动，即读取工程内资源运行，不会读取 AB 包也不会进行更新。若要测试更新模式，需要在 Base 组件取消 Editor Resource Mode，并确保 Resource 组件的 Resource Mode 为 Updatable 模式；打包资源并正确部署后即可运行更新模式（借助 HFS 等工具可在本地部署测试）。

## 结语

感谢 [GameFramework][1] 作者 [Ellan Jiang][3] 提供的优秀框架。

  [1]: https://github.com/EllanJiang/GameFramework "GF link"
  [3]: https://github.com/EllanJiang "Ellan Jiang link"
