﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Funly.SkyStudio
{
  public class SkyBuilder : System.Object
  {
    public Material skyboxMaterial;
    public bool starLayer1Enabled;
    public bool starLayer2Enabled;
    public bool starLayer3Enabled;

    public float starLayer1Density;
    public float starLayer2Density;
    public float starLayer3Density;

    private int m_ImageSize = 512;
    private int m_BusyRenderingCount;
    private bool m_IsCancelled;

    public bool IsComplete
    {
      get { return m_BusyRenderingCount == 0; }
    }

    public delegate void SkyBuilderProgressCallback(SkyBuilder builder, float progress);
    public event SkyBuilderProgressCallback progressCallback;

    public delegate void SkyBuilderCompletionCallback(SkyBuilder builder, bool success);
    public event SkyBuilderCompletionCallback completionCallback;

    private List<BaseStarDataRenderer> m_Renderers = new List<BaseStarDataRenderer>();

    // Stars and async task to rebuild the skyColors system data.
    public void BuildSkySystem()
    {
      m_Renderers.Clear();

      if (skyboxMaterial == null) {
        Debug.LogError("Can't build skyColors system without skybox material.");
        return;
      }

      m_BusyRenderingCount = CountToRender();

      if (starLayer1Enabled) {
        RebuildStarLayer("1", starLayer1Density);
      }

      if (starLayer2Enabled) {
        RebuildStarLayer("2", starLayer2Density);
      }

      if (starLayer3Enabled) {
        RebuildStarLayer("3", starLayer3Density);
      }
    }

    public void CancelBuild()
    {
      m_IsCancelled = true;
      completionCallback = null;
      progressCallback = null;

      foreach (BaseStarDataRenderer r in m_Renderers)
      {
        r.Cancel();
      }

      m_Renderers.Clear();
    }

    int CountToRender()
    {
      int count = 0;
      if (starLayer1Enabled) {
        count += 1;
      }

      if (starLayer2Enabled) {
        count += 1;
      }

      if (starLayer3Enabled) {
        count += 1;
      }

      return count;
    }

    void RebuildStarLayer(string layerId, float density)
    {
      BaseStarDataRenderer renderer = GetBestRendererForPlatform();
      renderer.layerId = layerId;
      renderer.imageSize = m_ImageSize;
      renderer.density = density;
      renderer.completionCallback += OnStarRenderingComplete;
      renderer.progressCallback += OnStarRenderingProgress;

      m_Renderers.Add(renderer);

      EditorCoroutine.start(renderer.ComputeStarData());
    }

    private BaseStarDataRenderer GetBestRendererForPlatform()
    {
      return new GPUStarDataRenderer();
    }

    // Embed a texture into the material asset.
    void AddObjectToMaterial(UnityEngine.Object obj, Material mat)
    {
      if (obj == null)
      {
        return;
      }
      string assetPath = AssetDatabase.GetAssetPath(mat);
      AssetDatabase.AddObjectToAsset(obj, assetPath);
      AssetDatabase.ImportAsset(assetPath);
    }

    // Remove an texture associated with a material.
    private void RemoveAllObjectsWithName(string textureName, Material mat)
    {
      string assetPath = AssetDatabase.GetAssetPath(skyboxMaterial);
      UnityEngine.Object[] objs = AssetDatabase.LoadAllAssetsAtPath(assetPath);
      foreach (UnityEngine.Object obj in objs) {
        if (obj == null) {
          continue;
        }

        if (obj.name == textureName) {
          UnityEngine.Object.DestroyImmediate(obj, true);
        }
      }

      AssetDatabase.ImportAsset(assetPath);
    }

    private void OnStarRenderingProgress(BaseStarDataRenderer renderer, float progress)
    {
      if (progressCallback != null) {
        progressCallback(this, progress);
      }
    }

    private void OnStarRenderingComplete(BaseStarDataRenderer renderer, Texture2D texture, bool success)
    {
      if (m_IsCancelled)
      {
        m_BusyRenderingCount = 0;
        return;
      }

      RemoveAllObjectsWithName(renderer.layerId, skyboxMaterial);

      AssetDatabase.Refresh();

      AddObjectToMaterial(texture, skyboxMaterial);

      // Update the material with the new texture file.
      skyboxMaterial.SetTexture("_StarLayer" + renderer.layerId + "DataTex", texture);

      m_BusyRenderingCount -= 1;

      if (completionCallback != null) {
        completionCallback(this, true);
      }
    }
  }
}
