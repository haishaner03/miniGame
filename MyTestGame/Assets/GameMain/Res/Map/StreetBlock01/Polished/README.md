# 当前地图优化资源

当前场景 `ZombieLevel01` 已应用：

- `GroundTilemap` 的 249 个 `Pavement` 格替换为 16 种低对比混凝土路沿，按四方向的沥青邻居选择路沿形态。
- 当前路沿视觉已更新为 `GptImage2/Curb_StreetEdge_4x4_GptImage2.png`，包含 16 个 64×64 模块（直线、内外角、T 形连接、端头和泥土/草地破损过渡）；旧 `ConcreteCurb_*.png` 仍保留作备份。
- 279 个 `Asphalt` 格替换为 3 种可平铺的旧柏油，固定坐标随机分布，减少重复感。
- `ObstacleTilemap` 继续作为地面上的装饰层，使用透明轮廓的岩石、枯树根、残枝和杂草。
- `GameplayTilemap` 继续作为地面上的交互/障碍层，使用透明轮廓的废弃轿车、翻倒车辆、围栏和路障。
- PPU 按物件用途调整：汽车约 1.8–2.2 格，枯树根约 2.3 格，岩石约 1 格，小型杂物约 0.7–1.2 格。
- `GptImage2` 中的 `Building_RowShop_GptImage2`、`Building_LowApartment_GptImage2` 和 `Building_SafehouseFront_GptImage2` 已应用到 `BuildingVisuals`，采用正面俯视构图，适合房屋紧密排布。

没有改动 `CollisionTilemap`，也没有给视觉物件新增碰撞，避免改变当前路线和角色通行逻辑。需要正式阻挡树木或汽车时，建议在单独的碰撞层放置 1–2 格的碰撞形状。

当前 `03_RocksTrees_4x4.png` 里没有完整树冠，只有枯树根、岩石和杂草，所以本次优化的是现有枯树/岩石素材的透明边界与比例。要做高大的绿色树木，需要另做一组带透明背景的树冠素材。

原场景和路面 Tile 备份在：
`D:/UnityProject/miniGame/.codex_artifacts/map_polish/backup/`
