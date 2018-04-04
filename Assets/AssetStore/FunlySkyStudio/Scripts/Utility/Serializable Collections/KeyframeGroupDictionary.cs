﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Funly.SkyStudio
{
  [Serializable]
  public class KeyframeGroupDictionary : System.Object, ISerializationCallbackReceiver, IEnumerable<string>
  {
    [NonSerialized]
    private Dictionary<string, IKeyframeGroup> m_Groups = new Dictionary<string, IKeyframeGroup>();

    [SerializeField]
    private ColorGroupDictionary m_ColorGroup = new ColorGroupDictionary();

    [SerializeField]
    private NumberGroupDictionary m_NumberGroup = new NumberGroupDictionary();

    [SerializeField]
    private TextureGroupDictionary m_TextureGroup = new TextureGroupDictionary();

    public IKeyframeGroup this[string aKey]
    {
      get { return m_Groups[aKey]; }
      set { m_Groups[aKey] = value; }
    }

    public bool ContainsKey(string key)
    {
      return m_Groups.ContainsKey(key);
    }

    public void Clear()
    {
      m_Groups.Clear();  
    }

    public T GetGroup<T>(string propertyName) where T : class {
      if (typeof(T) == typeof(ColorKeyframeGroup)) {
        return m_Groups[propertyName] as T;
      }

      if (typeof(T) == typeof(NumberKeyframeGroup)) {
        return m_Groups[propertyName] as T;
      }

      if (typeof(T) == typeof(TextureKeyframeGroup)) {
        return m_Groups[propertyName] as T;
      }

      return null;
    }
    
    // Unity doesn't supporty polymorphic serialization, so we split into type safe lists.
    public void OnBeforeSerialize()
    {
      m_ColorGroup.Clear();
      m_NumberGroup.Clear();
      m_TextureGroup.Clear();

      foreach (string key in m_Groups.Keys) {
        IKeyframeGroup obj = m_Groups[key];

        if (obj is ColorKeyframeGroup) {
          m_ColorGroup[key] = obj as ColorKeyframeGroup;
        } else if (obj is NumberKeyframeGroup) {
          m_NumberGroup[key] = obj as NumberKeyframeGroup;
        } else if (obj is TextureKeyframeGroup) {
          m_TextureGroup[key] = obj as TextureKeyframeGroup;
        }
      }
    }

    // Restore the generic list that uses an interface group.
    public void OnAfterDeserialize()
    {
      m_Groups.Clear();

      foreach (string key in m_ColorGroup.dict.Keys) {
        m_Groups[key] = m_ColorGroup[key];
      }

      foreach (string key in m_NumberGroup.dict.Keys) {
        m_Groups[key] = m_NumberGroup[key];
      }

      foreach (string key in m_TextureGroup.dict.Keys) {
        m_Groups[key] = m_TextureGroup[key];
      }
    }

    public IEnumerator<string> GetEnumerator()
    {
      return m_Groups.Keys.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }
  }
}
