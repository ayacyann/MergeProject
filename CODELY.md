

## Codely Structured Memories

### User

### Feedback
- [2026-11-11 11:27:24] 本工程批量重命名图集切片时，禁止用 `TextureImporter.spritesheet` 赋值回写：会把 spriteID/internalID 清零并触发 "Identifier uniqueness violation ... FileId:0"，导致子资源引用歧义。应直接改写 .meta 文本（保留原 spriteID/internalID/nameFileIdTable）或走 SerializedObject 的 m_SpriteSheet.m_Sprites。
- [2026-11-11 17:44:49] 重命名 ScriptableObject 的序列化字段会静默丢数据（例如 produceCounts→produceInfos 后旧资源里的值全空，还引发 BaseProducer NRE）。改字段名/类型前应加 [FormerlySerializedAs("旧名")]，或改名后用 SerializedObject/文本解析迁移旧值并校验。

### Project
- [2026-11-11 17:44:34] 合成游戏数据结构约定（截至 2026-11-11）：每条链 6 级，切片名 <chain>_1.._6；资源在 Assets/Resources/ItemDefinition/{三位chainID}_{Type}/，含 Type1..6.asset 与 TypeChain.asset；itemId 为全局连续 int（XP/Coin/Energy 排最前）；tier/sellPrice=1..6。itemType 在链上：普通链 MergeChain、生产者链 ProducerMergeChain(isTimesProducer / produceChains 权重列表 ProduceChainEntry / produceLevel / produceInfos: List<ProduceInfo{tier,maxCount,timesInterval秒}>)。enum ItemType 在 Scripts/Managers/GlobalEnums.cs。

- [2026-11-11 17:44:42] 物品对象池架构（GridSpawnManager）：一个 ItemType 一个 ObjectPool（Dictionary<ItemType,ObjectPool<Item>>），每个池在 ObjectPool 下有独立空子物体 {ItemType}Pool 存放实例；预制体按 ItemPrefabEntry{itemType,prefab} 配置，泛型 CreateItem<T>() 配合 itemFactories 分发；静态映射表 scriptTypeToItemType 让 PushItem/GetPool 只凭物品实例即可定位池子。脚本：Scripts/Item/Base/{Item,BaseProducer,BaseResources}、Scripts/Item/{Producer,TimesProducer:Producer,Chest,ItemCooldown,ClickAndDragItem}，预制体在 Assets/Resources/Prefabs/Item/。
- [2026-11-11 23:07:08] 订单系统（截至 2026-11-11）：ItemOrder 用 OrderRequirement.referencedItems 缓存网格物品引用，创建时 ScanGrid 扫描并 SetCheck；合并/生成靠 GameManager.OnChestSlotItemsChangeEvent，交付靠 DeliveryOrderManager.OnOrderDeliveredEvent 广播并让其他订单裁剪引用后按 IsItemReferenced 重算 check；DeliveryOrderManager 按 Q 生成 1-2 种、每种 1-3 个的随机订单，奖励=等级*数量*5 金币 + 等级*数量经验；订单只从 itemType=Normal 的链随机（GetNormalOrderChains 已过滤 Producer/TimesProducer/Chest/Resource）；池化订单的需求视图按索引复用，不要 Destroy 后同帧 Instantiate。

- [2026-11-12 00:18:00] 基础数值与资源系统（截至 2026-11-12）：资源链 chainID 0/1/2 = XP/Coin/Energy（在 specialMergeChain 里），MergeItemDefinition.value 为点击获得数值（1/3/8/20/50/125）；GameManager 提供 AddExp（自动升级，ExpForLevel=level*25）/AddCoins/SpendCoins/AddEnergy/SpendEnergy/CollectResource 与 OnStatsChanged 事件，原 remainedTimes 已用 [FormerlySerializedAs] 改名为 energy；PlayerStatusUI 挂在 Panel/BaseProperty 上驱动经验条/等级/金币/能量；GridSpawnManager.mergeResources 配置合成掉落（mergeResourceChance=0.25，权重 XP50/Coin40/Energy10）。Producer 次数耗尽后按该级 ProduceInfo.timesInterval 进入 ItemCooldown 冷却、冷却结束补满 currentTimes（换定义/池复用时取消冷却）；TimesProducer 仍是消耗回收。

- [2026-11-11 23:04:12] 团结编辑器进入 Play 时会稳定抛 4 条 UnityEditor.GameObjectInspector.OnDisable / RectTransformEditor.OnEnable / ImageEditor.OnEnable 异常；清空选择、关闭 Inspector 窗口后仍复现，堆栈全在 UnityEditor 内部，属编辑器侧噪声，不要当项目报错排查（清 ActiveEditorTracker 可临时清屏）。
- [2026-11-12 00:18:00] 物品池化与合并的隐藏契约：GridSpawnManager.PushItem → Item.ResetItem 会立即清空该实例的 MID/MC，并重置 interactionBlocked/isItemMoving/hoveredSlot/lastSlot（残留的 interactionBlocked 会让 FollowSlot 不执行、池化复用的物品停在槽位外）。任何“先 PushItem 再读物品字段/悬停目标”的代码都会拿到 null（Item.OnEndDrag 曾因此在 SetDefinitionInfo 与 MC.GetMergeItemDefinitionFromTier 抛 NRE）。合并分支必须在 PushItem 之前缓存 MergeChain、MID.tier 和合并目标 Item。

- [2026-11-11 23:20:10] 生产与能量的约定（2026-11-11 起）：能量=点击生产的次数，BaseProducer.TryProduce 每次成功生产前调用 GameManager.SpendEnergy(1)，能量不足则直接返回、不生成也不扣 currentTimes。chainID 映射：specialMergeChain 0=XP/1=Coin/2=Energy，itemMergeChain 3=Bread/4=Flower/5=Gift(TimesProducer)/6=Chest(Chest)/7=Oven(Producer)/8=SeedBag/9=Toy；GridSpawnManager.Start 里 GetMergeChainFromId(6) 的“4级烘焙机”注释是链 ID 后移的历史遗留，实际取的是 ChestChain。
- [2026-11-12 00:18:05] 宝箱背包 UI（2026-11-12）：Panel/SideButtons 上挂 Scripts/UI/ChestMenuUI.cs，6 个 ChestInMenu 条目 mid=Chest1..6 / mc=ChestChain，角标用 Add 的 Image 切 addHasNumSprite（有数量）/addEmptySprite（0）并显隐 addText；进入游戏每箱送 1；点击 ChestMask/Image 走 ClaimChest（网格满/无空位只打日志），AddChest(tier,amount)/GetChestCount(tier) 是对外钩子，升级时按 OnStatsChanged 比较 level 随机发 ≤ 当前等级的箱子并打 Console 日志；背包开合用 DOTween DOLocalMoveX 在 openX=450 / closeX=630 间滑动（不是 SetActive），按钮的 onClick 全部在运行时绑定（场景里没有持久监听）。
- [2026-11-12 00:18:05] 生成入槽的健壮性约定：Item.ConnectSlot 在目标槽位已被占用时会静默 return，所以所有“CreateItemFromMID 后 ConnectSlot”的路径（GridSpawnManager.TrySpawnMergeResource、BaseProducer.TryProduce）都必须校验 item.slot == null 并回收该物品，否则会留下悬空、点不到的物品；另外合并 Resource 类型（XP/Coin/Energy）链时要跳过 TrySpawnMergeResource，避免合成本身就白送资源。

### Reference

