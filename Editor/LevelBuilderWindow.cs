﻿using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace elZach.LevelEditor
{
    public class LevelBuilderWindow : EditorWindow
    {
        [MenuItem("Window/LevelBuilder")]
        static void Init()
        {
            LevelBuilderWindow window = (LevelBuilderWindow)EditorWindow.GetWindow(typeof(LevelBuilderWindow));
            window.titleContent = new GUIContent("Level Builder");
            window.minSize = new Vector2(100, 100);
            window.floorPlane = new Plane(Vector3.up, t.transform.position);
            window.Show();
        }

        static LevelBuilder t
        {
            get
            {
                if (!_t)
                {
                    _t = FindObjectOfType<LevelBuilder>();
                    if (!_t)
                    {
                        var go = new GameObject("Level Builder");
                        _t = go.AddComponent<LevelBuilder>();
                    }
                }
                return _t;
            }
        }
        static LevelBuilder _t;
        bool painting = true;
        TileObject selectedTile;
        Plane floorPlane;
        int3 tileMousePosition;
        int targetHeigth = 0;
        bool[] _layerVis;
        bool[] layerVisibility { get
            {
                if (_layerVis == null) _layerVis = new bool[t.layers.Count];
                if (t.layers.Count > _layerVis.Length)
                {
                    bool[] biggerBoolArray = new bool[t.layers.Count];
                    for (int i = 0; i < _layerVis.Length; i++)
                        biggerBoolArray[i] = _layerVis[i];
                    _layerVis = biggerBoolArray;
                }
                return _layerVis;
            }
        }

        public enum RasterVisibility { None, WhenPainting, Always }
        public RasterVisibility rasterVisibility = RasterVisibility.WhenPainting;

        private void OnGUI()
        {
            painting = GUILayout.Toggle(painting, "painting","Button");
            EditorGUILayout.LabelField(new GUIContent(layerIndex+":"+paletteIndex));
            //if (painting)
            {
                //if (layerIndex < -1 || layerIndex >= t.tileSet.layers.Count) return;
                layerIndex = Mathf.Clamp(layerIndex, -1, t.tileSet.layers.Count - 1);
                paletteIndex = Mathf.Clamp(paletteIndex, 0, layerIndex == -1 ? (t.tileSet.tiles.Count-1) : (t.tileSet.layers[layerIndex].layerObjects.Count-1));
                paletteIndex = Mathf.Max(0, paletteIndex);
                if (layerIndex == -1)
                    selectedTileGuid = t.tileSet.tiles[paletteIndex].guid;
                else if (t.tileSet.layers[layerIndex]?.layerObjects.Count>0)
                    selectedTileGuid = t.tileSet.layers[layerIndex]?.layerObjects[paletteIndex]?.guid;
                if(!string.IsNullOrEmpty(selectedTileGuid))
                    selectedTile = t.tileSet.TileFromGuid[selectedTileGuid];
                if(!selectedTile)
                {
                    selectedTile = t.tileSet.tiles[0];
                    selectedTileGuid = selectedTile.guid;
                }
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Grid Visibility ");
            rasterVisibility = (RasterVisibility) EditorGUILayout.EnumPopup(rasterVisibility);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            targetHeigth = EditorGUILayout.IntField("Heigth: ", targetHeigth);
            // activeLayer = EditorGUILayout.IntField("Layer:", activeLayer);
            EditorGUILayout.EndHorizontal();
            DrawPalette(t.tileSet, Event.current);
            if(GUILayout.Button("Clear Level"))t.ClearLevel(); 
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if(painting) sceneView.Repaint(); // <- is this neccessary? 2020.2.0a15 seems to call repaint just every few OnScenGUI
            DrawRaster();
            OnScreenUI(sceneView);
            floorPlane = new Plane(t.transform.up, t.transform.position + Vector3.up *t.rasterSize.y*targetHeigth);
            var e = Event.current;
            if (painting)
            {
                if (e.isMouse)
                {
                    Ray guiRay = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                    float d;
                    floorPlane.Raycast(guiRay, out d);
                    Vector3 worldPos = guiRay.GetPoint(d);
                    //Debug.Log("worldPos: " + worldPos+"; mousePos: "+tileMousePosition);
                    var newTileMousePosition = t.WorldPositionToTilePosition(worldPos);
                    if (t.TilePositionInFloorSize(newTileMousePosition))
                        tileMousePosition = newTileMousePosition;
                }
                //Handles.DrawWireDisc(t.TilePositionToLocalPosition(tileMousePosition, selectedTile.size), Vector3.up, 0.5f * selectedTile.size.x);
                Handles.color = Color.white;
                int3 brushSize = selectedTile ? selectedTile.size : new int3(1, 1, 1);
                Handles.DrawWireCube(t.TilePositionToLocalPosition(tileMousePosition, brushSize) + Vector3.up * brushSize.y * 0.5f, new Vector3(brushSize.x, brushSize.y, brushSize.z));
                if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && e.modifiers == EventModifiers.None)
                    DrawTiles(sceneView, e, tileMousePosition);
                else if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 1 && e.modifiers == EventModifiers.None)
                    EraseTiles(sceneView, e, tileMousePosition);
            }
        }

        void OnScreenUI(SceneView sceneView)
        {
            if (!painting) return;
            Handles.BeginGUI();
            var icon_eye = EditorGUIUtility.IconContent("VisibilityOn");
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            //targetHeigth = EditorGUILayout.IntSlider(targetHeigth, 0, t.floorSize.y);
            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            targetHeigth = Mathf.RoundToInt(GUILayout.VerticalSlider(targetHeigth, t.floorSize.y, -t.floorSize.y, GUILayout.Height(100), GUILayout.Width(12)));
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            Color guiColor = GUI.backgroundColor;
            for (int i = 0; i < t.layers.Count; i++)
            {
                GUILayout.BeginHorizontal();
                GUI.backgroundColor = t.tileSet.layers[i].color + (i == layerIndex ? Color.gray : Color.clear);
                if(GUILayout.Button((i+":"+t.tileSet.layers[i].name), "Button", GUILayout.Width(100)))
                {
                    layerIndex = i;
                    Repaint();
                }
                bool vis = !layerVisibility[i];
                vis = GUILayout.Toggle(vis, icon_eye, "Button", GUILayout.Width(30), GUILayout.Height(19));
                if(vis != !layerVisibility[i])
                {
                    layerVisibility[i] = !vis;
                    t.ToggleLayerActive(vis, i);
                }
                GUILayout.Label(t.layers[i].Keys.Count.ToString(), "HelpBox", GUILayout.Height(19));
                GUILayout.EndHorizontal();
            }
            GUI.backgroundColor = guiColor;
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            Handles.EndGUI();
        }

        void DrawRaster()
        {
            if (rasterVisibility == RasterVisibility.Always || (rasterVisibility == RasterVisibility.WhenPainting && painting))
            {
                Handles.matrix = t.transform.localToWorldMatrix;
                Handles.color = new Color(0f, 0.5f, 0.8f, 0.15f);
                float zOffset = t.rasterSize.z * t.floorSize.z * 0.5f;
                for (int x = -Mathf.FloorToInt(t.floorSize.x / 2); x < Mathf.CeilToInt(t.floorSize.x / 2); x++)
                {
                    Handles.DrawLine(new Vector3(x * t.rasterSize.x, targetHeigth * t.rasterSize.y, -zOffset), new Vector3(x * t.rasterSize.x, targetHeigth * t.rasterSize.y, zOffset));
                }
                float xOffset = t.rasterSize.x * t.floorSize.x * 0.5f;
                for (int z = -Mathf.FloorToInt(t.floorSize.z / 2); z < Mathf.CeilToInt(t.floorSize.z / 2); z++)
                {
                    Handles.DrawLine(new Vector3(-xOffset, targetHeigth * t.rasterSize.y, z * t.rasterSize.z), new Vector3(xOffset, targetHeigth * t.rasterSize.y, z * t.rasterSize.z));
                }
                if (painting)
                {
                    Vector3 handle = Handles.PositionHandle(new Vector3(-Mathf.FloorToInt(t.floorSize.x / 2), targetHeigth, -Mathf.FloorToInt(t.floorSize.z / 2)), Quaternion.identity);
                    t.floorSize = new int3(Mathf.FloorToInt(handle.x * -2), t.floorSize.y, Mathf.FloorToInt(handle.z * -2));
                    targetHeigth = Mathf.Clamp(Mathf.FloorToInt(handle.y), -t.floorSize.y, t.floorSize.y);
                }
                //Handles.FreeMoveHandle(new Vector3(-Mathf.FloorToInt(t.floorSize.x / 2), targetHeigth, -Mathf.FloorToInt(t.floorSize.z / 2)), Quaternion.identity, 1f, Vector3.one);
            }
        }

        string selectedTileGuid;
        int paletteIndex;
        int layerIndex = -1;
        Vector2 paletteScroll;
        void DrawPalette(TileAtlas atlas, Event e)
        {
            TileAtlas.TagLayer activeLayer = layerIndex == -1 ? atlas.defaultLayer : (layerIndex < atlas.layers.Count) ? atlas.layers[layerIndex] : atlas.defaultLayer;
            //DragTest
            //Rect myRect = GUILayoutUtility.GetRect(100, 40, GUILayout.ExpandWidth(true));
            //GUI.Box(myRect, "Drag and Drop Prefabs to this Box!");
            //if (myRect.Contains(e.mousePosition))
            //{
            //    if (e.type == EventType.DragUpdated)
            //    {
            //        DragAndDrop.visualMode = DragAndDropVisualMode.Move;
            //        //Debug.Log("Drag Updated!");
            //        e.Use();
            //    }
            //    else if (e.type == EventType.DragPerform)
            //    {
            //        DragAndDrop.AcceptDrag();
            //        Debug.Log("Drag Perform!");
            //        Debug.Log(DragAndDrop.objectReferences.Length);
            //        if (atlas.layers.Count > 0)
            //            for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
            //            {
            //                atlas.layers[0].layerObjects.Add(DragAndDrop.objectReferences[i] as TileObject);
            //            }
            //        e.Use();
            //    }
            //}
            //if (e.type == EventType.DragExited || e.type == EventType.MouseUp)
            //{
            //    //Debug.Log("Drag exited");
            //    DragAndDrop.PrepareStartDrag();
            //}
            //------
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            Color guiColor = GUI.color;
            if(GUILayout.Button("Unsorted", GUILayout.Width(18), GUILayout.Height(activeLayer == atlas.defaultLayer ? 60 : 30)))
                layerIndex = -1;
            for (int i = 0; i < atlas.layers.Count; i++)
            {
                GUI.color = atlas.layers[i].color;
                if(GUILayout.Button(("Layer " + i), GUILayout.Width(18), GUILayout.Height(activeLayer == atlas.layers[i] ? 60 : 30)))
                    layerIndex = i;
            }
            GUI.color = guiColor;
            if(GUILayout.Button("+", GUILayout.Width(18), GUILayout.Height(18)))
            {
                atlas.AddTagLayer();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical();
            if (activeLayer != atlas.defaultLayer)
            {
                bool changedBefore = GUI.changed;
                activeLayer.name = EditorGUILayout.TextField(activeLayer.name);
                if (!changedBefore && GUI.changed) UnityEditor.EditorUtility.SetDirty(atlas);
            }
            else
                EditorGUILayout.LabelField(activeLayer.name);

            if (layerIndex < atlas.layers.Count)
                activeLayer.rasterSize = EditorGUILayout.Vector3Field("raster ", activeLayer.rasterSize);

            if (activeLayer!= atlas.defaultLayer && activeLayer.layerObjects.Count == 0) { EditorGUILayout.LabelField("Drag and Drop Tiles here."); return; }

            //-----
            paletteScroll = EditorGUILayout.BeginScrollView(paletteScroll);
            List<GUIContent> paletteIcons = new List<GUIContent>();
            if (activeLayer==atlas.defaultLayer)
                foreach (var atlasTile in atlas.TileFromGuid.Values)
                {
                    // Get a preview for the prefab
                    if (!atlasTile) continue;
                    Texture2D texture = AssetPreview.GetAssetPreview(atlasTile.prefab);
                    paletteIcons.Add(new GUIContent(texture));
                }
            else
                foreach (var atlasTile in activeLayer.layerObjects)
                {
                    // Get a preview for the prefab
                    if (!atlasTile) continue;
                    Texture2D texture = AssetPreview.GetAssetPreview(atlasTile.prefab);
                    paletteIcons.Add(new GUIContent(texture));
                }

            if (activeLayer != atlas.defaultLayer) paletteIcons.Add(new GUIContent("-"));
            // Display the grid

            //paletteIndex = GUILayout.SelectionGrid(paletteIndex, paletteIcons.ToArray(), 4, GUILayout.Width(position.width-38));
            float columnCount = 4f;
            EditorGUILayout.BeginHorizontal();
            
            for(int i=0; i < paletteIcons.Count; i++)
            {
                Rect buttonRect = GUILayoutUtility.GetRect(paletteIcons[i],"Button", GUILayout.Width((position.width - 60) / columnCount));
                TileObject buttonTileObject = activeLayer == atlas.defaultLayer ? atlas.tiles[i] : i < activeLayer.layerObjects.Count ? activeLayer.layerObjects[i] : null;
                bool clickHere = false;
                if (buttonRect.Contains(e.mousePosition))
                {
                    switch (e.type)
                    {
                        //case EventType.MouseDrag:
                        //    DragAndDrop.PrepareStartDrag();

                        //    DragAndDrop.SetGenericData("TileObject", buttonTileObject);
                        //    DragAndDrop.objectReferences = new Object[] { buttonTileObject };
                        //    DragAndDrop.StartDrag("Drag");
                        //    break;
                        //case EventType.DragExited:
                        //    clickHere = true;
                        //    break;
                        case EventType.MouseDown:
                            clickHere = true;
                            break;
                    }
                }
                GUI.Toggle(buttonRect, paletteIndex == i, paletteIcons[i], "Button");
                if (clickHere)
                {
                    paletteIndex = i;
                    if (e.button == 1 || activeLayer == atlas.defaultLayer) // rightclick
                    {
                        GenericMenu menu = new GenericMenu();

                        menu.AddDisabledItem(new GUIContent("Move to Layer"));
                        for (int i2 = 0; i2 < atlas.layers.Count; i2++)
                        {
                            int weird = i2;
                            menu.AddItem(new GUIContent("Layer " + weird), false, () =>
                             {
                                 Debug.Log(weird + ":" + atlas.layers.Count + " obj: " + buttonTileObject.name);
                                 atlas.MoveTileToLayer(buttonTileObject, atlas.layers[weird]);
                             });
                        }
                        menu.AddItem(new GUIContent("Add New Layer"), false,()=> 
                        {
                            atlas.AddTagLayer();
                            atlas.MoveTileToLayer(buttonTileObject, atlas.layers[atlas.layers.Count - 1]);
                        });
                        menu.ShowAsContext();

                        e.Use();
                    }
                    else if (activeLayer != atlas.defaultLayer && i == paletteIcons.Count - 1)
                    {
                        atlas.layers.Remove(activeLayer);
                        paletteIndex = 0;
                        layerIndex--;
                    }

                }
                if (i % (int)(columnCount-1) == 0 && i != 0)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
            }
            EditorGUILayout.EndHorizontal();
            if(activeLayer==atlas.defaultLayer)
                selectedTileGuid = atlas.tiles[paletteIndex].guid;
            else
                selectedTileGuid = activeLayer.layerObjects[paletteIndex].guid;
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndHorizontal();
        }

        public void ChangeTileToLayer(TileAtlas atlas, int i2, TileObject buttonTileObject)
        {
            Debug.Log(i2 + ":" + atlas.layers.Count + " obj: " + buttonTileObject.name);
            atlas.layers[i2].layerObjects.Add(buttonTileObject);
        }

        private void DrawTiles(SceneView view, Event e, int3 tilePosition)
        {
            if (layerIndex == -1) return;
            GUIUtility.hotControl = 0;
            t.PlaceTile(selectedTileGuid, tilePosition, layerIndex);
            e.Use();
        }

        void EraseTiles(SceneView view, Event e, int3 tilePosition)
        {
            if (layerIndex == -1) return;
            GUIUtility.hotControl = 0;
            t.RemoveTile(tilePosition, layerIndex);
            e.Use();
        }

        void OnFocus()
        {
            SceneView.duringSceneGui -= this.OnSceneGUI; // Just in case
            SceneView.duringSceneGui += this.OnSceneGUI;
        }

        void OnDestroy()
        {
            SceneView.duringSceneGui -= this.OnSceneGUI;
        }

    }
}