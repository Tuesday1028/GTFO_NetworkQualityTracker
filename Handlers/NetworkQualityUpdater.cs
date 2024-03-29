﻿using BepInEx.Unity.IL2CPP.Utils;
using GTFO.API;
using Hikaria.NetworkQualityTracker.Managers;
using SNetwork;
using System.Collections;
using System.Text;
using UnityEngine;
using static Hikaria.NetworkQualityTracker.Features.NetworkQualityTracker;

namespace Hikaria.NetworkQualityTracker.Handlers;

public class NetworkQualityUpdater : MonoBehaviour
{
    public static NetworkQualityUpdater Instance { get; private set; }

    private const float TextUpdateInterval = 0.5f;

    private const float HeartbeatSendInterval = 0.5f;

    private const float ToMasterQualityReportSendInterval = 0.5f;

    private static Coroutine _broadcastCoroutine;

    private void Awake()
    {
        Instance = this;
    }

    public static void StartCoroutine()
    {
        Instance.StartCoroutine(SendHeartbeatCoroutine());
        Instance.StartCoroutine(SendToMasterQualityCoroutine());
        Instance.StartCoroutine(TextUpdateCoroutine());
        Instance.StartCoroutine(WatchdogCoroutine());
    }

    private static IEnumerator WatchdogCoroutine()
    {
        var yielder = new WaitForSecondsRealtime(1f);
        while (true)
        {
            foreach (var data in NetworkQualityManager.NetworkQualityDataLookup.Values)
            {
                data.CheckConnection();
            }
            yield return yielder;
        }
    }

    public static void StartBroadcast()
    {
        if (_broadcastCoroutine != null)
        {
            Instance.StopCoroutine(_broadcastCoroutine);
        }
        _broadcastCoroutine = Instance.StartCoroutine(BroadcastCoroutine());
    }

    private static IEnumerator BroadcastCoroutine()
    {
        var yielder = new WaitForSecondsRealtime(1f);
        int second = 60;
        while (second-- > 0)
        {
            BroadcastListenHeartbeat();
            second--;
            yield return yielder;
        }
    }

    private static IEnumerator SendHeartbeatCoroutine()
    {
        var yielder = new WaitForSecondsRealtime(HeartbeatSendInterval);
        while (true)
        {
            NetworkQualityManager.SendHeartbeats();
            yield return yielder;
        }
    }

    private static IEnumerator TextUpdateCoroutine()
    {
        var yielder = new WaitForSecondsRealtime(TextUpdateInterval);

        StringBuilder sb = new(300);

        while (true)
        {
            foreach (var data in NetworkQualityManager.NetworkQualityDataLookup.Values)
            {
                data.GetToMasterReportText(out var toMasterLatencyText, out var toMasterJitterText, out var toMasterPacketLossRateText);
                if (s_ShowInWatermark && data.Owner.IsLocal)
                {
                    if (NetworkQualityManager.WatermarkQualityTextMesh != null)
                    {
                        NetworkQualityManager.WatermarkQualityTextMesh.SetText($"{toMasterLatencyText}, {toMasterJitterText}, {toMasterPacketLossRateText}");
                        NetworkQualityManager.WatermarkQualityTextMesh.ForceMeshUpdate();
                    }
                }
                if (s_ShowInPageLoadout && NetworkQualityManager.PageLoadoutQualityTextMeshes.TryGetValue(data.Owner.PlayerSlotIndex(), out var textMesh))
                {
                    if (!data.Owner.IsLocal && AnyShowToLocal)
                    {
                        data.GetToLocalReportText(out var toLocalLatencyText, out var toLocalJitterText, out var toLocalPacketLossRateText);

                        sb.Append($"{Settings.InfoSettings.ToLocalHint}\n");

                        if (ShowToLocalLatency)
                            sb.Append($"{toLocalLatencyText}\n");
                        if (ShowToLocalNetworkJitter)
                            sb.Append($"{toLocalJitterText}\n");
                        if (ShowToLocalPacketLoss)
                            sb.Append($"{toLocalPacketLossRateText}\n");
                    }

                    if (NetworkQualityManager.IsMasterHasHeartbeat && !SNet.IsMaster && !data.Owner.IsMaster && AnyShowToMaster)
                    {
                        sb.Append($"{Settings.InfoSettings.ToMasterHint}\n");

                        if (ShowToMasterLatency)
                            sb.Append($"{toMasterLatencyText}\n");
                        if (ShowToMasterNetworkJitter)
                            sb.Append($"{toMasterJitterText}\n");
                        if (ShowToMasterPacketLoss)
                            sb.Append($"{toMasterPacketLossRateText}\n");
                    }

                    textMesh.SetText(sb.ToString());
                    textMesh.ForceMeshUpdate();
                    sb.Clear();
                }
            }
            yield return yielder;
        }
    }

    public static bool ShowToLocalLatency = true;
    public static bool ShowToLocalNetworkJitter = true;
    public static bool ShowToLocalPacketLoss = true;
    public static bool ShowToMasterLatency = true;
    public static bool ShowToMasterNetworkJitter = true;
    public static bool ShowToMasterPacketLoss = true;
    private static bool AnyShowToLocal => ShowToLocalLatency || ShowToLocalNetworkJitter || ShowToLocalPacketLoss;
    private static bool AnyShowToMaster => ShowToMasterLatency || ShowToMasterNetworkJitter || ShowToMasterPacketLoss;

    private static IEnumerator SendToMasterQualityCoroutine()
    {
        var yielder = new WaitForSecondsRealtime(ToMasterQualityReportSendInterval);
        while (true)
        {
            if (!SNet.IsMaster && NetworkQualityManager.NetworkQualityDataLookup.TryGetValue(SNet.LocalPlayer.Lookup, out var quality))
            {
                NetworkAPI.InvokeEvent(typeof(pToMasterNetworkQualityReport).FullName, quality.GetToMasterReportData(), NetworkQualityManager.HeartbeatListeners.Values.ToList(), SNet_ChannelType.GameNonCritical);
            }
            if (NetworkQualityManager.IsMasterHasHeartbeat && NetworkQualityManager.NetworkQualityDataLookup.TryGetValue(SNet.Master.Lookup, out var masterQuality))
            {
                masterQuality.UpdateToMasterQuality();
            }
            yield return yielder;
        }
    }
}