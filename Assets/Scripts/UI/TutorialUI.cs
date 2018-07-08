﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class TutorialUI : MonoBehaviour {

	public static TutorialUI current;

	public GameObject m_TurnTutorial;
    public GameObject m_JumpTutorial;
    public GameObject m_BoostFramePrefab;
    public GameObject m_TutorialMaskPrefab;

    List<GameObject> frames;
    GameObject m_TutorialMask;

    const string TurnTutorialShowedKey = "TurnTutorialShowed";
	const string CubeTutorialShowedKey = "CubeTutorialShowed";
    const string JumpTutorialShowedKey = "JTS";

    void Awake()
	{
		current = this;
	}

	public void ShowTurnAndBoostTutorialIfFirstTime()
	{
		if(!RecordManager.HasRecord(TurnTutorialShowedKey))
		{
			GameManager.current.ShowTutorial (true);
			m_TurnTutorial.SetActive (true);
			RecordManager.Record (TurnTutorialShowedKey);
		}
	}

	public void FinishTurnAndBoostTutorial()
	{
		GameManager.current.ShowTutorial (false);
	}

	public void FinishShowCube()
	{
		GameManager.current.ShowTutorial (false);
	}

    public void ShowJumpTutorialIfFirstTime()
    {
        //if (!RecordManager.HasRecord(JumpTutorialShowedKey))
        /*{
            GameManager.current.ShowTutorial(true);
            m_JumpTutorial.SetActive(true);
            RecordManager.Record(JumpTutorialShowedKey);
        }*/
        GameManager.current.Pause(true, false);

        SetUpJumpMaskAndBoostButtons();
    }

    public void HideJumpTutorial()
    {
        GameManager.current.Pause(false, false);
        foreach(var frame in frames)
        {
            Destroy(frame);
        }
        Destroy(m_TutorialMask);
    }

    void SetUpJumpMaskAndBoostButtons()
    {
        var buttonList = AccelerateButton.accButtons;
        frames = new List<GameObject>();
        if (buttonList != null)
        {
            Transform buttonParent = null;
            foreach (var button in buttonList)
            {
                buttonParent = button.transform.parent;
                GameObject frame = Instantiate(m_BoostFramePrefab, button.transform);
                frames.Add(frame);
            }
            // Only when gravity control it is null
            if(buttonParent != null)
            {
                m_TutorialMask = Instantiate(m_TutorialMaskPrefab, buttonParent);
                int indexMinus = InputHandler.current.m_ControlScheme == ControlSchemes.BothHand ? 3 : 2;
                m_TutorialMask.transform.SetSiblingIndex(buttonParent.childCount - indexMinus);
            }
        }
    }
}
