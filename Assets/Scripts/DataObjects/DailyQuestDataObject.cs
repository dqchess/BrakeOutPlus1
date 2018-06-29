﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine; 

[System.Serializable]
public class DailyQuestSingleData
{
    public QuestAction action;
    public MinMaxDataInt actionCount;
    public MinMaxDataInt rewardCoin;
    public MinMaxDataInt rewardStar;
}

[CreateAssetMenu(fileName = "DailyQuestDataObject", menuName = "Custom/DailyQuestData", order = 1)]
public class DailyQuestDataObject : ScriptableObject {
    public DailyQuestSingleData[] randomQuests;
}
