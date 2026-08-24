# GameMain Learning Notes

## Step 1: Procedure Loop

Open `Assets/GameMain/Scenes/Launch.unity` and press Play.

Expected Console flow:

```text
Launch -> Menu -> Battle -> Result -> Menu
```

This proves the UnityGameFramework `Base`, `Fsm`, and `Procedure` components are
working together. The current transitions are timer-based only; menu buttons and
real battle logic will be added in later steps.

## Step 2: Menu Button Drives Procedure

`ProcedureMenu` now loads `Assets/GameMain/Prefabs/UI/MainMenu.prefab`. The flow
pauses at Menu until the `Start Game` button is clicked.

Expected flow:

```text
Launch -> Menu -> click Start Game -> Battle -> Result -> Menu
```

`MainMenuView` owns the button and exposes a `StartClicked` event. `ProcedureMenu`
listens to that event and changes to `ProcedureBattle`.

## Step 3: Menu UI Uses UGF UIForm

`MainMenuView` now inherits from `UIFormLogic`, so the menu prefab is a UGF UI
form. `ProcedureMenu` opens it through `UIComponent.OpenUIForm` and closes it
through `UIComponent.CloseUIForm`.

Expected flow:

```text
ProcedureMenu -> UIComponent.OpenUIForm -> MainMenuView.OnOpen
click Start Game -> ProcedureMenu.ChangeState<ProcedureBattle>
ProcedureMenu.OnLeave -> UIComponent.CloseUIForm -> MainMenuView.OnClose
```

This is the first step from direct `Resources.Load + Instantiate` toward the
UnityGameFramework UI module.

## Step 4: Battle Procedure Loads Scene

`ProcedureBattle` now uses `SceneComponent` to load
`Assets/GameMain/Scenes/Battle.unity`. The battle scene is synced from the
imported WaveOfTheFist demo scene, so it includes the controllable player.

Expected flow:

```text
click Start Game
-> ProcedureBattle.OnEnter
-> SceneComponent.LoadScene
-> LoadSceneSuccessEventArgs
-> Battle scene becomes active
-> test the WaveOfTheFist player controls
```

This step introduces the UGF scene loading pattern: call the component first,
then listen for success/failure through `EventComponent`.

## Current Folder Layout

```text
GameMain
  Editor/Builders     editor-only scripts that generate demo scenes, prefabs, and animations
  Editor/Setup        editor-only bootstrap helpers
  LearningDemos       imported learning material kept separate from game code
  Prefabs             reusable GameMain prefabs
  Scenes              Launch and Battle scenes
  Scripts             runtime game code
```

`LearningDemos/WaveOfTheFist` is still treated as source learning material.
When a piece becomes part of our own game, copy or rebuild it into `Prefabs`,
`Scenes`, or `Scripts` instead of mixing new game code back into the demo folder.
