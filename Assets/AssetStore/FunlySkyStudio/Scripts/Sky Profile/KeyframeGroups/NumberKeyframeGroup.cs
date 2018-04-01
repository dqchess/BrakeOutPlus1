﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Funly.SkyStudio
{
  [Serializable]
  public class NumberKeyframeGroup : KeyframeGroup<NumberKeyframe>
  {
    public float minValue;
    public float maxValue;

    public NumberKeyframeGroup(string name, float min, float max) : base(name)
    {
      minValue = min;
      maxValue = max;
    }

    public NumberKeyframeGroup(string name, float min, float max, NumberKeyframe frame) : base(name)
    {
      minValue = min;
      maxValue = max;
      AddKeyFrame(frame);
    }

    public float GetFirstValue()
    {
      return GetKeyframe(0).value;
    }

    // Get a percent for a given value in this range.
    public float ValueToPercent(float value)
    {
      return Mathf.Abs((value - minValue) / (maxValue - minValue));
    }

    // Get a normalized 0-1 value for the value at a given time position.
    public float ValuePercentAtTime(float time)
    {
      return ValueToPercent(NumericValueAtTime(time));
    }

    // Convert percent to a value.
    public float PercentToValue(float percent)
    {
      float value = minValue + ((maxValue - minValue) * percent);
      return Mathf.Clamp(value, minValue, maxValue);
    }

    // Get the value for a point in time
    public float NumericValueAtTime(float time, bool isOrbitCycle = false)
    {
      time = time - (int)time;

      if (keyframes.Count == 0)
      {
        Debug.LogError("Keyframe group has no keyframes: " + name);
        return minValue;
      }

      if (keyframes.Count == 1)
      {
        return GetKeyframe(0).value;
      }

      int beforeKeyIndex, afterKeyIndex;
      GetSurroundingKeyFrames(time, out beforeKeyIndex, out afterKeyIndex);

      NumberKeyframe before = GetKeyframe(beforeKeyIndex);
      NumberKeyframe after = GetKeyframe(afterKeyIndex);

      float blendPercent = ProgressBetweenSurroundingKeyframes(time, before, after);

      // For orbit cycles we need to force them to interpolate in positive direction.
      if (before.curveType == CurveType.OrbitBodyForwardComplete && after.value < before.value) {
        float interpolatedValue = Mathf.Lerp(before.value, 1.0f + after.value, blendPercent);
        return interpolatedValue - ((int)interpolatedValue);
      } else
      {
        float curveAdjustedTime = CurveAdjustedBlendingTime(before.curveType, blendPercent);
				return Mathf.Lerp(before.value, after.value, curveAdjustedTime);
			}
    }
  }
}

