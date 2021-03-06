﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace elZach.LevelEditor
{
    [System.Serializable]
    public class PlacedTile
    {
        public string guid;
        public int3 position;
        public TileObject tileObject;
        public GameObject placedObject;
        public TileAtlas.TagLayer layer;
        public PlacedTile(string guid, int3 position, TileObject tileObject, GameObject placedObject, TileAtlas.TagLayer layer)
        {
            this.guid = guid;
            this.position = position;
            this.tileObject = tileObject;
            this.placedObject = placedObject;
            this.layer = layer;
        }
    }
}