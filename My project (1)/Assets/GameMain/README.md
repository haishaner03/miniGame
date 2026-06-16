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

`ProcedureMenu` now creates a simple runtime menu. The flow pauses at Menu until
the `Start Game` button is clicked.

Expected flow:

```text
Launch -> Menu -> click Start Game -> Battle -> Result -> Menu
```
