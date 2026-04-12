using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 挂在与 <see cref="FryPot"/> 同一物体（需有 NetworkIdentity）上，
/// 将服务端煎炸状态与锅内视觉同步到所有客户端。
/// </summary>
[RequireComponent(typeof(FryPot))]
[RequireComponent(typeof(NetworkIdentity))]
public class FryPotNetworkSync : NetworkBehaviour
{
    FryPot _pot;

    [SyncVar(hook = nameof(OnSyncProgress))]
    float syncCurrentProgress;

    [SyncVar(hook = nameof(OnSyncRequired))]
    float syncRequiredProgress;

    [SyncVar(hook = nameof(OnSyncFlags))]
    byte syncFlags; // bit0 cookingFinished, bit1 isBurnCountdown, bit2 receivesHeat

    [SyncVar(hook = nameof(OnSyncBurnElapsed))]
    float syncBurnElapsed;

    /// <summary>按下锅顺序，用 ';' 分隔的食材 id（与 FryIngredientTag 一致），用于客户端重建锅内散件视觉。</summary>
    [SyncVar(hook = nameof(OnSyncIngredientLine))]
    string syncIngredientVisualLine;

    /// <summary>当前成品锅内模型对应的菜谱名；空表示无成品视觉。</summary>
    [SyncVar(hook = nameof(OnSyncFinishedRecipe))]
    string syncFinishedRecipeName;

    const byte FlagCookingFinished = 1;
    const byte FlagBurnCountdown = 2;
    const byte FlagReceivesHeat = 4;

    void Awake()
    {
        _pot = GetComponent<FryPot>();
    }

    void LateUpdate()
    {
        if (!isServer || _pot == null) return;

        syncCurrentProgress = _pot.currentProgress;
        syncRequiredProgress = _pot.requiredProgress;

        byte f = 0;
        if (_pot.cookingFinished) f |= FlagCookingFinished;
        if (_pot.IsBurnCountdown) f |= FlagBurnCountdown;
        if (_pot.ReceivesStationHeat) f |= FlagReceivesHeat;
        syncFlags = f;

        syncBurnElapsed = _pot.BurnElapsedNetwork;

        syncIngredientVisualLine = _pot.GetNetworkIngredientVisualLine();
        syncFinishedRecipeName = _pot.GetNetworkFinishedRecipeName();
    }

    // ── Hooks：仅非服务端应用镜像（Host 仍跑服务端逻辑，不覆盖）──

    void OnSyncProgress(float oldV, float newV)
    {
        if (isServer) return;
        _pot.ApplyClientMirrorProgress(newV, syncRequiredProgress);
    }

    void OnSyncRequired(float oldV, float newV)
    {
        if (isServer) return;
        _pot.ApplyClientMirrorProgress(syncCurrentProgress, newV);
    }

    void OnSyncFlags(byte oldV, byte v)
    {
        if (isServer) return;
        _pot.ApplyClientMirrorFlags(
            (v & FlagCookingFinished) != 0,
            (v & FlagBurnCountdown) != 0,
            (v & FlagReceivesHeat) != 0);
    }

    void OnSyncBurnElapsed(float oldV, float newV)
    {
        if (isServer) return;
        _pot.ApplyClientMirrorBurnElapsed(newV);
    }

    void OnSyncIngredientLine(string oldV, string line)
    {
        if (isServer) return;
        _pot.RebuildClientIngredientVisualsFromNetwork(line);
    }

    void OnSyncFinishedRecipe(string oldV, string recipeName)
    {
        if (isServer) return;
        _pot.RebuildClientFinishedVisualFromNetwork(recipeName);
    }
}
