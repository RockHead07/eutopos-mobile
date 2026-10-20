using Eutopos.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;
using NumV2 = System.Numerics.Vector2;

namespace Eutopos.App
{
    public sealed partial class AppController
    {
        RouteView routeView;
        MiniMap miniMap;
        NavGraph graph;
        Route route;
        int targetIndex = -1;
        bool navigating;
        NumV2 lastPlanPos;
        float navStartT, lastMiniMapT;
        int acceptedAtStart;

        void AwakeNavigation(Transform root)
        {
            routeView = root.gameObject.AddComponent<RouteView>();
            miniMap = new MiniMap(ui.MiniMap);
            RefreshTargetLabel();
        }

        bool fetchingGraph;

        /// <summary>Ambil graf bila belum ada dan tidak sedang diambil (saat mulai dan tiap tombol Lokalisasi).</summary>
        void EnsureNavGraph()
        {
            if (graph != null || fetchingGraph || vps.Config == null) return;
            fetchingGraph = true;
            StartCoroutine(vps.FetchNavGraph(OnNavGraph));
        }

        void OnNavGraph(NavGraphResult r)
        {
            fetchingGraph = false;
            log.Write("navgraph", new JObject { ["status"] = r.Status.ToString(), ["detail"] = r.Detail, ["map_version"] = r.MapVersion });
            if (r.Status == VpsStatus.Ok)
            {
                graph = r.Graph;
                targetIndex = graph.Targets.Count > 0 ? 0 : -1;
            }
            else if (r.Status == VpsStatus.NotFound) lastMessage = "Graf rute belum dipasang; target nonaktif";
            else if (r.Status == VpsStatus.Unauthorized) lastMessage = "Token akses tidak ada atau salah";
            else if (r.Status == VpsStatus.NetworkError) lastMessage = "Server tidak terjangkau; graf rute dicoba lagi saat Lokalisasi";
            else lastMessage = "Graf rute gagal dimuat: " + r.Detail;
            RefreshTargetLabel();
        }

        public void CycleTarget(int step)
        {
            if (graph == null || graph.Targets.Count == 0 || navigating) return;
            targetIndex = (targetIndex + step + graph.Targets.Count) % graph.Targets.Count;
            RefreshTargetLabel();
        }

        void RefreshTargetLabel()
        {
            bool has = graph != null && targetIndex >= 0;
            ui.TargetLabel.text = has ? "Target: " + graph.Targets[targetIndex].Name : "Target: -";
            ui.PrevTarget.interactable = ui.NextTarget.interactable = ui.NavButton.interactable = has;
        }

        public void OnNavPressed()
        {
            if (navigating) { StopNavigation("berhenti"); return; }
            if (graph == null || targetIndex < 0) return;
            if (!rootCtl.HasAlignment || needsRelocalize) { lastMessage = "Tekan Lokalisasi dulu"; return; }
            navigating = true;
            scheduler.Navigating = true;
            scheduler.Resume(); // navigasi baru mencoba lagi; bila token masih salah, jawaban 401 pertama mendiamkannya lagi
            navStartT = Time.realtimeSinceStartup;
            acceptedAtStart = acceptedCount;
            ui.NavButton.GetComponentInChildren<UnityEngine.UI.Text>().text = "Berhenti";
            ui.ShowBanner(null);
            Replan();
        }

        void StopNavigation(string reason)
        {
            navigating = false;
            scheduler.Navigating = false;
            routeView.Clear();
            route = null;
            ui.NavButton.GetComponentInChildren<UnityEngine.UI.Text>().text = "Mulai";
            log.Write("nav_stop", new JObject { ["reason"] = reason });
        }

        void OnCorrectionAccepted()
        {
            if (navigating) Replan();
        }

        void Replan()
        {
            if (!rootCtl.TryUserPoseB(arCamera.transform, out var posB, out _)) return;
            var user = Conv.FloorG(posB);
            var target = graph.Targets[targetIndex];
            route = graph.Plan(user, target.Node);
            lastPlanPos = user;
            if (route == null)
            {
                routeView.Clear();
                lastMessage = "Target tidak terjangkau dari posisi ini";
                log.Write("route", new JObject { ["target"] = target.Node, ["reachable"] = false });
                return;
            }
            routeView.Show(route);
            log.Write("route", new JObject
            {
                ["target"] = target.Node,
                ["reachable"] = true,
                ["nodes"] = new JArray(route.NodeIds),
                ["length_m"] = route.LengthM,
            });
        }

        void UpdateNavigation(float now)
        {
            bool hasPose = rootCtl.TryUserPoseB(arCamera.transform, out var posB, out var yawB);
            NumV2? user = hasPose ? Conv.FloorG(posB) : (NumV2?)null;
            if (navigating && user.HasValue && !needsRelocalize)
            {
                if (NumV2.Distance(user.Value, lastPlanPos) > 1f) Replan();
                var target = graph.Targets[targetIndex];
                var goal = graph.Nodes[target.Node];
                if (NavGraph.Arrived(user.Value, goal))
                {
                    float d = NumV2.Distance(user.Value, goal);
                    ui.ShowBanner($"Sampai di {target.Name} ({d:0.00} m menurut aplikasi)");
                    log.Write("arrival", new JObject
                    {
                        ["target"] = target.Node,
                        ["app_distance_m"] = d,
                        ["duration_s"] = now - navStartT,
                        ["corrections_accepted"] = acceptedCount - acceptedAtStart,
                    });
                    StopNavigation("arrived");
                }
            }
            if (navigating && needsRelocalize) routeView.Clear();
            if (now - lastMiniMapT >= 0.2f)
            {
                lastMiniMapT = now;
                miniMap.Draw(graph, route, user, yawB);
            }
        }

        public void OnLogoTapped() => DebugPanel.OnLogoTapped(this); // Task 10

        int pointIndex;

        public void ToggleDebug()
        {
            if (ui.DebugRoot == null) return;
            ui.DebugRoot.SetActive(!ui.DebugRoot.activeSelf);
            RefreshDebug();
        }

        public void AdjustInterval(float deltaS)
        {
            scheduler.IntervalS = Mathf.Clamp(scheduler.IntervalS + deltaS, 2f, 30f);
            LogSettings();
            RefreshDebug();
        }

        public void AdjustJump(float deltaM)
        {
            guard.JumpThresholdM = Mathf.Clamp(guard.JumpThresholdM + deltaM, 1f, 10f);
            LogSettings();
            RefreshDebug();
        }

        public void CyclePoint(int step)
        {
            if (graph == null) return;
            pointIndex = (pointIndex + step + graph.Nodes.Count) % graph.Nodes.Count;
            RefreshDebug();
        }

        string SelectedPoint()
        {
            if (graph == null) return null;
            int i = 0;
            foreach (var id in graph.Nodes.Keys) { if (i++ == pointIndex) return id; }
            return null;
        }

        public void ExportLog()
        {
            try { ui.DebugInfo.text = "Log disalin ke " + LogExporter.Export(log.FilePath, LogFiles.ExportName(lastResult?.AreaId, log.FilePath)); }
            catch (System.Exception e) { ui.DebugInfo.text = "Ekspor gagal: " + e.Message; }
        }

        public void MarkGroundTruth()
        {
            var id = SelectedPoint();
            if (id == null || !rootCtl.TryUserPoseB(arCamera.transform, out var posB, out var yawB))
            {
                ui.DebugInfo.text = "Belum ada graf atau belum ada posisi";
                return;
            }
            var app = Conv.FloorG(posB);
            var truth = graph.Nodes[id];
            float err = NumV2.Distance(app, truth);
            log.Write("ground_truth", new JObject
            {
                ["point"] = id,
                ["app_xy"] = new JArray(app.X, app.Y),
                ["truth_xy"] = new JArray(truth.X, truth.Y),
                ["error_m"] = err,
                ["app_yaw_b"] = float.IsNaN(yawB) ? null : (JToken)yawB,
            });
            ui.DebugInfo.text = $"{id}: galat {err:0.00} m dicatat\n{log.FilePath}";
        }

        void RefreshDebug()
        {
            if (ui.DebugRoot == null || !ui.DebugRoot.activeSelf) return;
            ui.DebugInterval.text = $"{scheduler.IntervalS:0} s";
            ui.DebugJump.text = $"{guard.JumpThresholdM:0.0} m";
            ui.DebugPoint.text = SelectedPoint() ?? "-";
            ui.DebugInfo.text = log.FilePath;
        }

        void LogSettings() => log.Write("settings", new JObject
        {
            ["interval_s"] = scheduler.IntervalS,
            ["jump_m"] = guard.JumpThresholdM,
        });
    }
}
