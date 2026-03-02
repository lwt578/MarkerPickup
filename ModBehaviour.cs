using Duckov.MiniMaps;
using Duckov.MiniMaps.UI;
using Duckov.Scenes;
using Duckov.UI;
using ItemStatsSystem;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace MarkerPickup
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private sealed class KeyMarker
        {
            public InteractablePickup? Pickup;
            public Item Item;
            public GameObject? MarkerObject;
            public SimplePointOfInterest? Poi;
            public string? DisplayName;
            public Color Color;
        }

        /// <summary>
        /// Map a character to its marker.
        /// </summary>
        private readonly HashSet<InteractablePickup> cachedPickups = new HashSet<InteractablePickup>();

        private readonly Dictionary<InteractablePickup, KeyMarker> _markers =
            new Dictionary<InteractablePickup, KeyMarker>();

        private bool _mapActive;
        private float _scanCooldown;
        private const float ScanIntervalSeconds = 1f;

        // Special preset names loaded from text file (one name per line). Comparisons are case-insensitive.
        private readonly static DateTime _specialPresetsLastWriteUtc = DateTime.MinValue;



        void OnEnable()
        {

            Debug.Log("Mod启用");

            View.OnActiveViewChanged += OnActiveViewChanged;
            SceneLoader.onStartedLoadingScene += OnSceneStartedLoading;
            SceneLoader.onFinishedLoadingScene += OnSceneFinishedLoading;

            if (IsMapOpen())
            {
                BeginTracking();
            }

        }


        void OnDisable()
        {

            View.OnActiveViewChanged -= OnActiveViewChanged;
            SceneLoader.onStartedLoadingScene -= OnSceneStartedLoading;
            SceneLoader.onFinishedLoadingScene -= OnSceneFinishedLoading;
            EndTracking();

        }

        private void OnSceneStartedLoading(SceneLoadingContext context)
        {
            // Clear markers when leaving the current scene
            ResetMarkers();
        }

        private void OnSceneFinishedLoading(SceneLoadingContext context)
        {
            // 延迟扫描，确保所有对象加载完成
            StartCoroutine(DelayedScan());
        }

        private System.Collections.IEnumerator DelayedScan()
        {
            yield return new WaitForSeconds(0.5f);
            if (_mapActive || IsMapOpen())
                ScanPickups();
        }

        private static bool IsMapOpen()
        {
            var view = MiniMapView.Instance;
            return view != null && View.ActiveView == view;
        }

        private void OnActiveViewChanged()
        {
            if (IsMapOpen())
            {
                BeginTracking();
            }

            else
            {
                EndTracking();
            }
        }
        private void BeginTracking()
        {
            // Don't reset markers on map open - preserve last known positions when Live is OFF
            // ResetMarkers();
            _mapActive = true;

            ScanPickups();
            Debug.Log("开始追踪散落物");
            _scanCooldown = ScanIntervalSeconds;
        }

        private void EndTracking()
        {
            if (!_mapActive)
                return;
            _mapActive = false;
            Debug.Log("停止追踪散落物");
            // Don't reset markers on map close - preserve last known positions when Live is OFF
            // ResetMarkers();

        }

        private static bool IsPickupValid(InteractablePickup pickup)
        {

            if (pickup == null || pickup.ItemAgent?.Item == null)
                return false;

            var go = pickup.gameObject;
            if (!go.scene.IsValid() || !go.scene.isLoaded)
                return false;

            return true;
        }

        private bool IsPickupPicked(InteractablePickup pickup)
        {

            var item = pickup.ItemAgent.Item;
            if (item.transform.parent == null) 
                return false;
            else
                return true;
        }

        private void ScanPickups()
        {
            var pickups = UnityEngine.Object.FindObjectsOfType<InteractablePickup>();
            Debug.Log($"扫描到 {pickups.Length} 个散落物");

            foreach (var pickup in pickups)
            {
                if (pickup == null|| pickup.ItemAgent?.Item == null) continue;
                
                var item = pickup.ItemAgent.Item;
                float value = item.GetTotalRawValue();

                /*
                // 找出门卡、钥匙、船票等重要物品
                if (item.name.Contains("JLAB", StringComparison.OrdinalIgnoreCase) ||
                    item.name.Contains("Key", StringComparison.OrdinalIgnoreCase) ||
                    item.name.Contains("Card", StringComparison.OrdinalIgnoreCase) ||
                    item.name.Contains("Ticket", StringComparison.OrdinalIgnoreCase))
 
                    AddOrUpdateMarker(pickup);
                if (value >= 1000f) // 价值超过500的物品
                */
                    AddOrUpdateMarker(pickup);

            }
        }

        private void AddOrUpdateMarker(InteractablePickup pickup)
        {
            //Debug.Log("添加或更新标记");

            if (!IsPickupValid(pickup))
                return;

            if (_markers.TryGetValue(pickup, out var marker))
            {
                if (IsPickupPicked(pickup))
                {
                    DestroyMarker(pickup);
                    return;
                }
                else
                {
                    UpdateMarker(marker);
                    return;
                }
            }


            var displayName = GetDisplayName(pickup);
            var item = pickup.ItemAgent.Item;
            var markerObject = new GameObject($"{displayName}");
            markerObject.transform.position = pickup.transform.position;

            if (MultiSceneCore.MainScene.HasValue)
            {
                SceneManager.MoveGameObjectToScene(markerObject, MultiSceneCore.MainScene.Value);
            }

            var poi = markerObject.AddComponent<SimplePointOfInterest>();
            var color = new Color(1f, 0.6f, 0f, 1f); //default color
            

            marker = new KeyMarker
            {
                Pickup = pickup, // 保存引用
                Item = item,
                MarkerObject = markerObject,
                Poi = poi,
                DisplayName = displayName,
                Color = color
            };

            _markers[pickup] = marker;

            if (marker.Poi == null) return;

            var icon = MapMarkerManager.Icons[0];

            if (icon != null)
            {

                marker.Poi.Color = marker.Color;
                marker.Poi.ShadowColor = Color.clear;

                marker.Poi.Setup(icon, displayName, followActiveScene: true);
                marker.Poi.HideIcon = false;

            }
            Debug.Log($"创建标记: {marker.DisplayName} 位置: {pickup.transform.position}");

        }

        private void UpdateMarker(KeyMarker marker)
        {

            if (marker?.MarkerObject == null || marker.Poi == null)
                return;         

            if (!IsPickupValid(marker.Pickup))
            {
                DestroyMarker(marker.Pickup);
                return;
            }

            if (IsPickupPicked(marker.Pickup))
            {
                DestroyMarker(marker.Pickup);
                return;
            }

            if (marker.MarkerObject.transform.position == marker.Pickup.transform.position)
                return;

            else 
            {
                PointsOfInterests.Unregister(marker.Poi);
                marker.MarkerObject.transform.position = marker.Pickup.transform.position;

                var poi = marker.MarkerObject.AddComponent<SimplePointOfInterest>();
                var icon = MapMarkerManager.Icons[0];

                if (icon != null)
                {

                    marker.Poi.Color = marker.Color;
                    marker.Poi.ShadowColor = Color.clear;

                    marker.Poi.Setup(icon, marker.DisplayName, followActiveScene: true);
                    marker.Poi.HideIcon = false;

                }


            }
                

            Debug.Log($"更新标记位置: {marker.DisplayName} 位置: {marker.MarkerObject.transform.position}");

        }

        private static string GetDisplayName(InteractablePickup pickup)
        {
            //Debug.Log("获取名字");
            var item = pickup.ItemAgent.Item;
            string name = item.DisplayName;
            return name;

        }


        private void Update()
        {
            if (!_mapActive)
            {
                return;
            }
            // 简单的计时器逻辑
            _scanCooldown -= Time.deltaTime;
            if (_scanCooldown <= 0)
            {
                ScanPickups();
                _scanCooldown = ScanIntervalSeconds;
            }
        }

        private void LateUpdate()
        {
            if (!_mapActive || _markers.Count == 0)
                return;

            List<InteractablePickup> stale = null;

            foreach (var kv in _markers)
            {
                var entry = kv.Value;
                var pickup = entry?.Pickup;

                // Use lightweight validation without GetComponent check
                if (IsPickupPicked(pickup))
                {
                    stale ??= new List<InteractablePickup>();
                    stale.Add(kv.Key);
                    continue;
                }
            }

            if (stale != null)
            {
                foreach (var pickup in stale)
                {
                    DestroyMarker(pickup);
                }
            }
        }

        //reset or destroy marker

        private void DestroyMarker(InteractablePickup pickup)
        {

            Debug.Log("执行DestroyMarker");
            if (!_markers.TryGetValue(pickup, out var marker))
                return;

            if (marker.Poi != null)
            {
                PointsOfInterests.Unregister(marker.Poi);
                marker.Poi = null;
            }

            _markers.Remove(pickup);
            cachedPickups.Remove(pickup);
            Debug.Log("移除标记");

        }

 


        private void ResetMarkers()
        {
            foreach (var marker in _markers.Values)
            {
                if (marker.Poi != null)
                {
                    PointsOfInterests.Unregister(marker.Poi);
                }
                DestroySafely(marker.MarkerObject);
            }
            _markers.Clear();
            cachedPickups.Clear();
            Debug.Log("重置所有标记");

        }

        private static void DestroySafely(GameObject go)
        {
            if (go != null)
            {
                UnityEngine.Object.Destroy(go);
            }
        }
    }
}
