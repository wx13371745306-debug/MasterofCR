using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

public class PlayerItemInteractor : MonoBehaviour
{
    [Header("Refs")]
    public PlayerInteractionSensor sensor;
    public Transform holdPoint;
    public bool debugLog = true;

    private CarryableItem heldItem;
    private IInteractiveStation activeStation;

    // 【新增】用于追踪当前真正亮起的高亮目标
    private CarryableItem highlightedItem;
    private ItemPlacePoint highlightedPlacePoint;
    private IInteractiveStation highlightedStation;

    // 【新增】长短按追踪
    private float jKeyPressTime = 0f;
    private bool isJKeyHeld = false;
    private bool hasHandledJKeyThisPress = false;

    public bool IsHoldingItem() => heldItem != null;
    public CarryableItem GetHeldItem() => heldItem;

    void Update()
    {
        if (Keyboard.current == null) return;

        UpdateHighlights(); // 【新增】每帧计算并控制高亮

        // 记录按下时刻
        if (Keyboard.current.jKey.wasPressedThisFrame)
        {
            jKeyPressTime = Time.time;
            isJKeyHeld = true;
            hasHandledJKeyThisPress = false;
        }

        // 按住期间检测是否超过 0.25s，如果是且未处理过，触发长按
        if (isJKeyHeld && Keyboard.current.jKey.isPressed)
        {
            float holdDuration = Time.time - jKeyPressTime;
            if (holdDuration >= 0.25f && !hasHandledJKeyThisPress)
            {
                hasHandledJKeyThisPress = true; // 标记已处理
                if (heldItem == null && activeStation == null)
                    TryBeginHold(isLongPress: true); // 强制拿起整堆
            }
        }

        // 短按松开
        if (Keyboard.current.jKey.wasReleasedThisFrame)
        {
            isJKeyHeld = false;
            if (!hasHandledJKeyThisPress)
            {
                hasHandledJKeyThisPress = true;
                if (heldItem == null && activeStation == null)
                    TryBeginHold(isLongPress: false);
                else if (heldItem != null)
                {
                    if (!TrySpecialInteraction())
                        TryEndHold();
                }
            }
        }

        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            if (heldItem != null)
            {
                if (activeStation != null) TryEndStationInteract();

                if (!TrySpecialInteraction())
                    heldItem.TryUse(this, sensor);
            }
            else if (activeStation == null)
            {
                TryBeginStationInteract();
            }
            else if (debugLog)
            {
                // 空手但仍占着上一台交互：Update 不会再次 TryBegin，K 无反应易被误判为「台子坏了」
                Debug.Log($"<color=#FFAA00>[StationK]</color> 按下K但 activeStation 非空(仍占着交互)：{DescribeStation(activeStation)}。松K或先结束上一台交互。");
            }
        }

        if (Keyboard.current.kKey.wasReleasedThisFrame && activeStation != null)
            TryEndStationInteract();
    }

    // 【核心新增模块】统一管理所有高亮
    void UpdateHighlights()
    {
        // 1. 清理上一帧的高亮
        if (highlightedItem != null) { highlightedItem.SetSensorHighlight(false); highlightedItem = null; }
        if (highlightedPlacePoint != null) { highlightedPlacePoint.SetSensorHighlight(false); highlightedPlacePoint = null; }
        if (highlightedStation != null) { highlightedStation.SetSensorHighlight(false); highlightedStation = null; }

        if (sensor == null) return;

        // 2. 根据玩家状态重新计算合法的高亮目标
        if (!IsHoldingItem())
        {
            // 空手状态：可以捡起物体 (J)，可以互动台子 (K)
            CarryableItem item = sensor.GetCurrentItem();
            if (item != null && (item.CanBePickedUp(holdPoint) || item is DirtyPlateStack))
            {
                highlightedItem = item;
                highlightedItem.SetSensorHighlight(true);
            }

            IInteractiveStation station = sensor.GetCurrentStation();
            if (station != null && station.CanInteract(this))
            {
                highlightedStation = station;
                highlightedStation.SetSensorHighlight(true);
            }
        }
        else
        {
            // 持物状态：可以放下物体 (J)
            ItemPlacePoint point = sensor.GetCurrentPlacePoint();
            if (point != null && point.CanPlace(heldItem))
            {
                highlightedPlacePoint = point;
                highlightedPlacePoint.SetSensorHighlight(true);
            }

            // 持物状态下也检查堆叠目标（另一个同类物体 或 已有的 Stack）
            CarryableItem stackTarget = sensor.GetCurrentStackTarget();
            if (stackTarget != null)
            {
                highlightedItem = stackTarget;
                highlightedItem.SetSensorHighlight(true);
            }

            // 持物状态下高亮特定 Station（ BinStation / FridgeStation）也能放置/交互
            IInteractiveStation station = sensor.GetCurrentStation();
            if (station is BinStation)
            {
                highlightedStation = station;
                highlightedStation.SetSensorHighlight(true);
            }
            else if (station is FridgeStation fridge && fridge.CanAcceptItem())
            {
                highlightedStation = station;
                highlightedStation.SetSensorHighlight(true);
            }

            if (highlightedItem == null)
            {
                FryPot heldPot = heldItem.GetComponent<FryPot>();
                if (heldPot != null && heldPot.CanServe())
                {
                    CarryableItem plateTarget = sensor != null ? sensor.GetCurrentServablePlateTarget() : null;
                    if (plateTarget != null)
                    {
                        highlightedItem = plateTarget;
                        highlightedItem.SetSensorHighlight(true);
                    }
                }
            }
        }
    }

    bool TryBeginHold(bool isLongPress = false)
    {
        if (sensor == null)
        {
            if (debugLog) Debug.Log("[PlayerItemInteractor] Pick failed: sensor is null.");
            return false;
        }

        if (holdPoint == null)
        {
            if (debugLog) Debug.Log("[PlayerItemInteractor] Pick failed: holdPoint is null.");
            return false;
        }

        PlayerNetworkController networkController = GetComponent<PlayerNetworkController>();

        // 【优先级0】如果面对的是一个打开的 SupplyBox，通过网络中枢请求提取
        IInteractiveStation currentStation = sensor.GetCurrentStation();
        if (currentStation is SupplyBox supplyBox)
        {
            if (supplyBox.IsOpened && supplyBox.currentCount > 0)
            {
                if (networkController != null && NetworkClient.active)
                {
                    networkController.CmdRequestDispenseBox(supplyBox.gameObject, isLongPress);
                    if (debugLog) Debug.Log($"<color=#FFA500>[大脑 拿取]</color> 发起箱子提货网络请求: {supplyBox.name}");
                    return true;
                }
            }
            // 如果箱子关闭或为空，拿取动作应该失败
            // 注意：我们直接返回 true 或 false，避免它去拿身后的其他东西而导致穿模误操作
            if (supplyBox.IsOpened) return false;
        }
        else if (currentStation is FridgeStation fridge)
        {
            if (fridge.TryTakeItem(this))
            {
                if (debugLog) Debug.Log($"<color=#00FFFF>[大脑 拿取]</color> 从冰箱 {fridge.name} 中空手拿出了存货");
                return true;
            }
        }
        else if (currentStation is OrderResponse orderResponse)
        {
            if (orderResponse.currentState == OrderResponse.TableState.WaitingForCleanup && orderResponse.CanInteract(this))
            {
                if (networkController != null && NetworkClient.active)
                {
                    NetworkIdentity oni = orderResponse.GetComponent<NetworkIdentity>();
                    if (oni != null)
                        networkController.CmdRequestTakeAllDirtyPlatesFromTable(oni.netId);
                    else if (debugLog)
                        Debug.LogWarning("[大脑 拿取] OrderResponse 缺少 NetworkIdentity，无法联机收盘。");
                }
                else
                    orderResponse.TryTakeAllDirtyPlatesOffline(this);

                if (debugLog) Debug.Log($"<color=#FFA500>[大脑 拿取]</color> 按下J从餐桌 {orderResponse.name} 端起脏盘子");
                return true;
            }
        }
        else if (currentStation is CleanPlateDispenserStation cleanDispenser)
        {
            if (cleanDispenser.CanDispense() && networkController != null && NetworkClient.active)
            {
                NetworkIdentity anchorNi = cleanDispenser.GetComponentInParent<NetworkIdentity>();
                if (anchorNi == null)
                {
                    if (debugLog)
                        Debug.LogWarning("[大脑 拿取] 干净盘发放台不在带 NetworkIdentity 的物体下，无法发 Command。请把 NI 挂在洗碗机预制体根上。");
                    return false;
                }

                networkController.CmdRequestDispenseCleanPlate(anchorNi.netId, isLongPress);
                if (debugLog) Debug.Log("<color=#00FFFF>[大脑 拿取]</color> 请求从干净盘出口发放一盘");
                return true;
            }
        }

        CarryableItem target = sensor.GetCurrentItem();

        if (target == null)
            return false;

        // 【优先级1】如果目标是 DirtyPlateStack，走联机分发逻辑
        if (target is DirtyPlateStack dirtyStack)
        {
            if (networkController != null && NetworkClient.active)
            {
                networkController.CmdRequestDispenseBox(dirtyStack.gameObject, isLongPress);
                if (debugLog) Debug.Log($"<color=#8B4513>[大脑 拿取]</color> 发起脏盘堆拿取请求");
                return true;
            }
            return false;
        }

        if (!target.CanBePickedUp(holdPoint))
            return false;

        // 【动态堆逻辑改造】：判断目标是不是 DynamicItemStack 堆
        DynamicItemStack targetStack = target as DynamicItemStack;
        
        if (targetStack != null && !isLongPress)
        {
            // 点按(Tap)：弹出堆内的一个物体
            CarryableItem singleItem = targetStack.PopItem();
            if (singleItem != null)
            {
                singleItem.BeginHold(holdPoint);
                heldItem = singleItem;
                if (debugLog) Debug.Log($"<color=#FF00FF>[大脑 拿取]</color> (单点抽出) 拿起了 {singleItem.name}");
                return true;
            }
            return false; // 如果堆空了就失败
        }

        // 网络隔离改造 (Step 4)：判断是否有主控干涉
        if (networkController != null && NetworkClient.active)
        {
            // 走权威服务器判决，本地先按兵不动，等待 Rpc 确认后赋值 heldItem
            networkController.CmdRequestPickUp(target.gameObject, isLongPress);
            if (debugLog) Debug.Log($"<color=#00FFFF>[大脑 拿取]</color> 发起网络拾取请求: {target.name}");
            return true; 
        }

        // 正常单机长按整锅端、或者不是堆的普通物体
        target.BeginHold(holdPoint);
        heldItem = target;

        if (debugLog)
            Debug.Log($"[PlayerItemInteractor] Begin hold: {target.name} | isLongPress: {isLongPress}");

        if (debugLog)
            Debug.Log($"<color=#FF00FF>[大脑 拿取]</color> 按下了J键{(isLongPress?"(长按版)":"(点按)")} 拿起了: {target.name}。它的父级变为了 -> <b>{target.transform.parent?.name}</b>");

        return true;
    }

    void TryEndHold()
    {
        if (heldItem == null) return;

        CarryableItem item = heldItem;
        PlayerNetworkController net = GetComponent<PlayerNetworkController>();

        if (net != null && NetworkClient.active && item.GetComponent<NetworkIdentity>() != null)
        {
            if (TryResolveNetworkRelease(item, out byte kind, out GameObject auxNetObj, out Vector3 auxWorldPos))
            {
                net.CmdRequestReleaseHeld(kind, item.gameObject, auxNetObj, auxWorldPos);
                return;
            }
        }

        TryEndHoldLocal();
    }

    /// <summary>
    /// 联机时解析应发送的放下类型；返回 false 时走本地 TryEndHoldLocal（如放置点被占需拒绝且不落回网络丢地）。
    /// </summary>
    /// <summary>
    /// 联机时解析放下类型。
    /// <para>有 NetworkIdentity 的目标（DynamicItemStack / CarryableItem 堆叠）→ auxNetObj。</para>
    /// <para>无 NetworkIdentity 的目标（ItemPlacePoint / FridgeStation）→ auxWorldPos，服务端按坐标匹配。</para>
    /// </summary>
    bool TryResolveNetworkRelease(CarryableItem item, out byte kind, out GameObject auxNetObj, out Vector3 auxWorldPos)
    {
        kind = PlayerNetworkController.ReleaseDrop;
        auxNetObj = null;
        auxWorldPos = Vector3.zero;

        // 脏盘整堆不可与其它 DynamicItemStack 合并/推入
        if (item is DirtyPlateStack)
        {
            IInteractiveStation dirtyStation = sensor != null ? sensor.GetCurrentStation() : null;
            if (dirtyStation is FridgeStation dirtyFridge && dirtyFridge.CanAcceptItem())
            {
                kind = PlayerNetworkController.ReleaseFridge;
                auxWorldPos = ((Component)dirtyFridge).transform.position;
                return true;
            }

            ItemPlacePoint dirtyPlacePoint = sensor != null ? sensor.GetCurrentPlacePoint() : null;
            if (dirtyPlacePoint != null)
            {
                if (dirtyPlacePoint.CanPlace(item))
                {
                    kind = PlayerNetworkController.ReleasePlace;
                    auxWorldPos = dirtyPlacePoint.transform.position;
                    return true;
                }
                kind = PlayerNetworkController.ReleaseDrop;
                return true;
            }

            kind = PlayerNetworkController.ReleaseDrop;
            return true;
        }

        // 【优先级1】堆叠（目标继承自 CarryableItem → NetworkBehaviour，有 NetworkIdentity）
        CarryableItem stackTarget = sensor != null ? sensor.GetCurrentStackTarget() : null;
        if (stackTarget != null && item.GetComponent<StackableProp>() != null)
        {
            DynamicItemStack existingStack = stackTarget as DynamicItemStack;
            if (existingStack != null && existingStack.CanAccept(item))
            {
                kind = PlayerNetworkController.ReleasePushStack;
                auxNetObj = existingStack.gameObject;
                return true;
            }

            StackableProp targetProp = stackTarget.GetComponent<StackableProp>();
            if (targetProp != null && targetProp.CanStackWith(item))
            {
                kind = PlayerNetworkController.ReleaseMergeStack;
                auxNetObj = stackTarget.gameObject;
                return true;
            }
        }

        // 【优先级1.5】冰箱（无 NetworkIdentity → 传世界坐标）
        IInteractiveStation station = sensor != null ? sensor.GetCurrentStation() : null;
        if (station is FridgeStation fridge && fridge.CanAcceptItem())
        {
            kind = PlayerNetworkController.ReleaseFridge;
            auxWorldPos = ((Component)fridge).transform.position;
            return true;
        }

        // 【优先级2】放置点（无 NetworkIdentity → 传世界坐标）
        ItemPlacePoint targetPoint = sensor != null ? sensor.GetCurrentPlacePoint() : null;
        if (targetPoint != null)
        {
            if (targetPoint.CanPlace(item))
            {
                kind = PlayerNetworkController.ReleasePlace;
                auxWorldPos = targetPoint.transform.position;
                return true;
            }
            // 传感器指着某放置点但当前物品不允许放上：仍走网络丢地，避免仅本地 TryEndHoldLocal 与权威不同步
            kind = PlayerNetworkController.ReleaseDrop;
            return true;
        }

        // 丢地
        kind = PlayerNetworkController.ReleaseDrop;
        return true;
    }

    /// <summary>单机或非网络同步路径下的完整放下逻辑。</summary>
    void TryEndHoldLocal()
    {
        if (heldItem == null) return;

        CarryableItem item = heldItem;

        CarryableItem stackTarget = sensor != null ? sensor.GetCurrentStackTarget() : null;
        if (stackTarget != null && item.GetComponent<StackableProp>() != null && !(item is DirtyPlateStack))
        {
            DynamicItemStack existingStack = stackTarget as DynamicItemStack;
            if (existingStack != null)
            {
                if (existingStack.PushItem(item, sensor))
                {
                    heldItem = null;
                    if (debugLog) Debug.Log($"<color=#00FF00>[大脑 放下]</color> 将 {item.name} 推入已有的 Stack: {existingStack.name}");
                    return;
                }
            }
            else
            {
                StackableProp targetProp = stackTarget.GetComponent<StackableProp>();
                if (targetProp != null)
                {
                    DynamicItemStack newStack = targetProp.MergeIntoStack(item, sensor);
                    if (newStack != null)
                    {
                        heldItem = null;
                        if (debugLog) Debug.Log($"<color=#00FF00>[大脑 放下]</color> {item.name} 和 {stackTarget.name} 合并成了新的 Stack: {newStack.name}");
                        return;
                    }
                }
            }
        }

        IInteractiveStation station = sensor != null ? sensor.GetCurrentStation() : null;
        if (station is FridgeStation fridge)
        {
            if (fridge.TryPutHeldItem(this, item))
            {
                heldItem = null;
                if (debugLog) Debug.Log($"<color=#00FFFF>[大脑 放下]</color> 成功将 {item.name} 放入进 {fridge.name}");
                return;
            }
        }

        ItemPlacePoint targetPoint = sensor != null ? sensor.GetCurrentPlacePoint() : null;
        if (targetPoint != null)
        {
            if (targetPoint.CanPlace(item) && targetPoint.TryAcceptItem(item))
            {
                heldItem = null;
                if (debugLog) Debug.Log($"<color=#00FF00>[大脑 放下]</color> 成功放置 {item.name} → {targetPoint.name}");
                return;
            }

            if (debugLog) Debug.Log($"<color=#FF0000>[大脑 放下]</color> 拒绝放置: {targetPoint.name} 已占用 (占用者: {targetPoint.CurrentItem?.name ?? "N/A"})，物品留在手中");
            return;
        }

        if (debugLog) Debug.Log($"<color=#FFFF00>[大脑 放下]</color> 前方无放置点，{item.name} 丢弃到地面");
        item.DropToGround();
        heldItem = null;
    }

    bool TryBeginStationInteract()
    {
        if (sensor == null)
        {
            if (debugLog) Debug.Log("<color=#FFAA00>[StationK]</color> TryBeginStationInteract: sensor 为 null");
            return false;
        }

        IInteractiveStation station = sensor.GetCurrentStation();
        if (station == null)
        {
            if (debugLog) Debug.Log("<color=#FFAA00>[StationK]</color> TryBeginStationInteract: GetCurrentStation()=null（传感器未指向任何 IInteractiveStation）");
            return false;
        }

        if (!station.CanInteract(this))
        {
            if (debugLog)
            {
                string extra = "";
                if (station is DishWashingStation dws)
                    extra = $" | DishWashing: dirtyInSink={dws.dirtyPlatesInSink} outputFull={dws.IsOutputFull()}";
                Debug.Log($"<color=#FFAA00>[StationK]</color> TryBeginStationInteract: CanInteract=false | 类型={station.GetType().Name}{extra}");
            }
            return false;
        }

        // 已与当前传感器指向的同一台子交互中（如长按洗碗）：勿重复 Begin
        if (activeStation != null && ReferenceEquals(activeStation, station))
        {
            if (debugLog) Debug.Log($"<color=#FFAA00>[StationK]</color> TryBeginStationInteract: 已与同一台子交互中，短路 return true | {DescribeStation(station)}");
            return true;
        }

        // 本地仍占着旧台子但传感器已指向新台 → 先 End，否则 Update 里 activeStation!=null 会阻止再次 TryBegin
        if (activeStation != null && !ReferenceEquals(activeStation, station))
            TryEndStationInteract();

        activeStation = station;

        PlayerNetworkController networkController = GetComponent<PlayerNetworkController>();
        bool isNetworked = networkController != null && Mirror.NetworkClient.active;

        if (debugLog)
        {
            GameObject netRoot = activeStation is Component compForNet ? FindNetworkedGameObject(compForNet) : null;
            string niName = netRoot != null ? netRoot.name : "?";
            Debug.Log($"<color=#FFAA00>[StationK]</color> TryBeginStationInteract: 将发起交互 | 类型={station.GetType().Name} | NetworkClient={isNetworked} | NI根={niName}");
        }

        // 电脑等纯本地 UI：必须在发起交互的客户端执行，不可走 Command（否则会在 Host 服务端打开界面）。
        if (isNetworked && activeStation is ComputerStation)
        {
            activeStation.BeginInteract(this);
        }
        else if (isNetworked && activeStation is Component comp)
        {
            GameObject stationRoot = FindNetworkedGameObject(comp);
            string stationTypeName = GetInteractiveStationTypeName(activeStation);
            if (debugLog)
            {
                NetworkIdentity ni = stationRoot.GetComponent<NetworkIdentity>();
                string netIdStr = ni != null ? ni.netId.ToString() : "无NI";
                Debug.Log($"<color=#FFAA00>[StationK]</color> CmdSetStationInteractState(true) → stationRoot={stationRoot.name} netId={netIdStr} stationTypeName={stationTypeName}");
            }
            networkController.CmdSetStationInteractState(stationRoot, true, stationTypeName);
        }
        else
        {
            if (debugLog) Debug.Log("<color=#FFAA00>[StationK]</color> 非联机或 NetworkClient 未激活：本地 BeginInteract");
            activeStation.BeginInteract(this);
        }

        return true;
    }

    static string DescribeStation(IInteractiveStation s)
    {
        if (s == null) return "null";
        if (s is Component c) return $"{c.GetType().Name} on {c.gameObject.name}";
        return s.GetType().Name;
    }

    /// <summary>供 Cmd 在服务端从同一 NI 根下多个 <see cref="BaseStation"/> 中解析目标脚本（传 <see cref="System.Type.Name"/>）。</summary>
    static string GetInteractiveStationTypeName(IInteractiveStation station)
    {
        if (station is MonoBehaviour mb)
            return mb.GetType().Name;
        return "";
    }

    void TryEndStationInteract()
    {
        if (activeStation == null) return;
        PlayerNetworkController networkController = GetComponent<PlayerNetworkController>();
        bool isNetworked = networkController != null && Mirror.NetworkClient.active;

        if (isNetworked && activeStation is ComputerStation)
        {
            activeStation.EndInteract(this);
        }
        else if (isNetworked && activeStation is Component comp)
        {
            string stationTypeName = GetInteractiveStationTypeName(activeStation);
            networkController.CmdSetStationInteractState(FindNetworkedGameObject(comp), false, stationTypeName);
        }
        else
        {
            activeStation.EndInteract(this);
        }

        activeStation = null;
    }

    /// <summary>
    /// 向上查找挂有 NetworkIdentity 的 GameObject，
    /// 解决 Station 脚本挂在子物体上而 NetworkIdentity 挂在根物体上的序列化问题。
    /// </summary>
    private GameObject FindNetworkedGameObject(Component comp)
    {
        Transform t = comp.transform;
        while (t != null)
        {
            if (t.GetComponent<Mirror.NetworkIdentity>() != null)
                return t.gameObject;
            t = t.parent;
        }
        return comp.gameObject;
    }

    bool TrySpecialInteraction()
    {
        if (heldItem == null || sensor == null) return false;

        IInteractiveStation station = sensor.GetCurrentStation();
        if (station is BinStation bin)
        {
            if (bin.TryDumpHeldItem(this, heldItem))
                return true;
        }

        PlateTool plateTool = heldItem.GetComponent<PlateTool>();
        if (plateTool != null)
        {
            if (plateTool.TryUse(this, sensor, heldItem))
                return true;
        }

        FryPot heldPot = heldItem.GetComponent<FryPot>();
        if (heldPot != null && heldPot.CanServe())
        {
            CarryableItem targetPlate = sensor != null ? sensor.GetCurrentServablePlateTarget() : null;
            if (targetPlate != null)
            {
                ExecutePotToPlateServe(heldPot, targetPlate);
                return true;
            }
        }

        return false;
    }

    void ExecutePotToPlateServe(FryPot pot, CarryableItem plate)
    {
        PlayerNetworkController net = GetComponent<PlayerNetworkController>();
        if (net != null && NetworkClient.active)
        {
            CarryableItem potItem = pot.GetComponent<CarryableItem>();
            if (potItem != null)
            {
                net.CmdRequestPotServe(potItem.gameObject, plate.gameObject);
                return;
            }
        }

        ExecutePotToPlateServeLocal(pot, plate);
    }

    /// <summary>仅由 Server(Host) 或单机执行的盛菜实际逻辑。联机时品质表现由 <see cref="PlayerNetworkController.RpcApplyDishQuality"/> 统一下发。</summary>
    public GameObject ExecutePotToPlateServeLocal(FryPot pot, CarryableItem plate)
    {
        GameObject dishPrefab = pot.Serve();
        if (dishPrefab == null)
        {
            if (debugLog) Debug.Log("<color=#FF0000>[盛菜]</color> pot.Serve() 返回 null");
            return null;
        }
        DishQuality servedQuality = pot.LastServedQuality;

        ItemPlacePoint platePoint = plate.CurrentPlacePoint;
        Vector3 fallbackPos = plate.transform.position;
        Quaternion fallbackRot = plate.transform.rotation;

        if (debugLog) Debug.Log($"<color=#00FF00>[盛菜]</color> platePoint={platePoint?.name ?? "null"} pos={fallbackPos}");

        if (platePoint != null)
            platePoint.ClearOccupant();

        if (NetworkServer.active)
            NetworkServer.Destroy(plate.gameObject);
        else
            Destroy(plate.gameObject);

        GameObject dishObj = Instantiate(dishPrefab, fallbackPos, fallbackRot);
        CarryableItem dishItem = dishObj.GetComponent<CarryableItem>();

        // 先 Spawn 再放置，避免 NT 关闭状态下 Spawn 导致远端位姿为原点
        if (NetworkServer.active)
            NetworkServer.Spawn(dishObj);

        bool placed = false;
        if (platePoint != null && dishItem != null)
        {
            dishItem.SetNetworkTransformSync(false);
            placed = platePoint.TryAcceptItem(dishItem);
        }

        if (!placed && dishItem != null)
        {
            dishItem.SetNetworkTransformSync(true);
            dishObj.transform.position = fallbackPos;
            dishObj.transform.rotation = fallbackRot;
        }

        // 单机/无 NetworkManager：本地直接应用品质；联机由 Rpc 统一（避免仅服务端显隐）
        DishQualityTag qualityTag = dishObj.GetComponent<DishQualityTag>();
        if (qualityTag != null && !NetworkServer.active)
            qualityTag.ApplyQuality(servedQuality);

        if (debugLog) Debug.Log($"<color=#00FF00>[盛菜]</color> 完成 dish={dishObj.name} pos={dishObj.transform.position} placed={placed}");
        return dishObj;
    }

    public void ReplaceHeldItem(CarryableItem newItem) { heldItem = newItem; }
    public Transform GetHoldPoint() { return holdPoint; }
}
