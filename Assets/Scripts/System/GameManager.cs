﻿using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Analytics;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System;

public class GameManager : MonoBehaviour {

    public static GameManager current;
    public static int m_NextAdTime = 3;

    public bool m_LoadFromDefault = true;
    public GameObject m_PlayerPrefab;
    public GameObject player;
    public GameObject m_CurrentTrail;

    public int m_CurrentCarIndex;
    public int m_CurrentSceneIndex;

    public float gameScore;
    public float scoreForDifficulty;
    public float scoreForDiffScale = 0.2f;
    public bool shouldUseScoreForDiff = true;
    public DiffScoreUsage DiffScoreOption;
    public bool m_StartBoosting = false;

	public int gameHighScore;
    public int gameCoins;
    public int gameStars;
    public int singleGameCoins = 0;
    public int colliNum;
	public int cldIndex;
	public int cIndex;
    public float m_BoostMultiplier;
    public float m_DiffMultiplier;
    public float m_ItemMultiplier;
    public float m_GlobalMultiplier;
    public GameObject[] m_InitSequence;
    public int m_GameCount;
    public int m_Level;

    [HideInInspector]
    public int m_ReviveCount;

    float m_OldTimescale;
    float m_SlowmotionFactor;
    [SerializeField]
    float m_AutoPilotSpeedUpFactor = 1.0f;

    public string highScoreLastDate;
    public int dayHighScore;
    const string LastDateName = "HSLastDate";
    const string DayHighScoreName = "DayHighScore";
    const string RemovedAD = "RemovedAD";

    public int m_DefaultCarIndex;
    public int m_DefaultSceneIndex;
    public string m_DefaultTrailName;
    
    public enum GameState
    {
        Login,
        Start,
        Menu,
        AssembleTrack,
        CutScene,
        Paused,
        ReviveMenu,
        Dead,
        Running,
		Tutorial
    }

    public enum DiffScoreUsage
    {
        Normal,
        ScaleOffHighest,
        HAHA
    }



    public GameState state;

    public static GameState GetGameState()
    {
        if (current) return current.state;
        return GameState.Start;
    }
    
    void Awake()
    {
        current = this;
       
        //Init App Manager
        if(AppManager.instance == null)
        {
            GameObject go = new GameObject();
            go.AddComponent<AppManager>();
            go.name = "AppManager";
        }

        //Init Game save
        LoadGameSave();
        LoadQuickstart();

        Application.targetFrameRate = 60;
        m_GameCount = 0;
        //QualitySettings.antiAliasing = 0;
        //QualitySettings.shadowCascades = 2;
        //QualitySettings.shadowDistance = 150;

        ResetSceneStats();

        //print(scoreForDifficulty);

        //ChallengeManager.current.currentFloorData = 0;

        foreach (GameObject obj in m_InitSequence)
        {
            obj.SetActive(true);
        }

        
		//DebugCoinStars ();
    }

	void DebugCoinStars()
	{
		AddCoin (100000);
		AddStar (100000);
	}

    void ResetSceneStats()
    {
        gameHighScore = PlayerPrefs.GetInt("High Score");
        gameScore = 0;
        if (DiffScoreOption == DiffScoreUsage.ScaleOffHighest)
        {
            scoreForDifficulty = 0;
            gameScore = 0;
        }
        else if (DiffScoreOption == DiffScoreUsage.Normal)
            scoreForDifficulty = 0;
        // else is haha

        Time.timeScale = 1.0f;
        m_SlowmotionFactor = 1.0f;
        m_OldTimescale = 1.0f;
        state = GameState.AssembleTrack;
        m_BoostMultiplier = 1.0f;
        m_DiffMultiplier = 1.0f;
        m_ItemMultiplier = 1.0f;
        m_Level = 0;
        m_StartBoosting = false;
        m_GlobalMultiplier = 1.0f;
        m_AutoPilotSpeedUpFactor = 1.0f;

         m_ReviveCount = 0;
    }

    private void Start()
    {
    }

    public void StartLoadCar()
    {
        if (m_LoadFromDefault || m_PlayerPrefab == null)
            LoadDefaultCarAndTrail();
        if (player == null) // shouldn't come to here
            player = Instantiate(m_PlayerPrefab);
    }

    public void ReloadCar()
    {
        if (player) Destroy(player);
        player = Instantiate(m_PlayerPrefab);
    }

    public void ReloadCar(GameObject carPrefab)
    {
        if (player) Destroy(player);
        player = Instantiate(carPrefab);
        Player.current = player.GetComponent<Player>();
    }

    public void ReloadCar(int carIndex, int sceneIndex)
    {
        m_CurrentCarIndex = carIndex;
        m_CurrentSceneIndex = sceneIndex;

        SingleCarSelectData data = CarSelectDataReader.Instance.GetCarData(carIndex, sceneIndex);
        if(SaveManager.instance.HasCar(data.name))
        {
            ReloadCar(data.CarInGamePrefab);
            CarClassData classData = CarSelectDataReader.Instance.GetCarClassData(data.carClass.ToString());
            if (classData != null)
            {
				Player.current.SetBoost(classData, data, SaveManager.instance.GetSavedCarDataForLevel(data.name));
                Player.current.physics.SetPhysicsByClassData(classData, data, SaveManager.instance.GetSavedCarDataForLevel(data.name));
            }

            SetDefaultCar(carIndex, sceneIndex);
        }
        else
        {
            Debug.LogError("Loading car that's not bought, loading default car");
            PlayerPrefs.SetInt("DefaultCar", 0);
            PlayerPrefs.SetInt("DefaultScene", 0);
            PlayerPrefs.SetString("DefaultTrail", "Line");
            PlayerPrefs.Save();
            LoadDefaultCarAndTrail();
        }
    }

	public void ResetCarData()
	{
		if (Player.current != null) {
			int carIndex = m_CurrentCarIndex;
			int sceneIndex = m_CurrentSceneIndex;

			SingleCarSelectData data = CarSelectDataReader.Instance.GetCarData(carIndex, sceneIndex);
			if(SaveManager.instance.HasCar(data.name))
			{
				CarClassData classData = CarSelectDataReader.Instance.GetCarClassData(data.carClass.ToString());
				if (classData != null)
				{
					Player.current.SetBoost(classData, data, SaveManager.instance.GetSavedCarDataForLevel(data.name));
					Player.current.physics.SetPhysicsByClassData(classData, data, SaveManager.instance.GetSavedCarDataForLevel(data.name));
				}
			}
		}
	}

    public void ReloadTrail(string name)
    {
        if(m_CurrentTrail != null)
        {
            Destroy(m_CurrentTrail);
        }

        var trails = CarSelectDataReader.Instance.GetCarData(m_CurrentCarIndex, m_CurrentSceneIndex).m_Trails;
        foreach (var trail in trails)
        {
            if (trail.name.Equals(name, StringComparison.InvariantCultureIgnoreCase)  && trail.TrailPrefab != null)
            {
                m_CurrentTrail = Instantiate(trail.TrailPrefab, player.GetComponent<Player>().vehicle.transform);
                m_CurrentTrail.transform.localPosition = Vector3.zero;
                //Player.current.vehicle.m_AutoPilotAndBoostTrails = m_CurrentTrail.transform.Find("Boost").gameObject;
                Player.current.vehicle.m_Trail = m_CurrentTrail.GetComponent<TrailComponent>();
            }
        }

		SetDefaultTrail(name);
    }

    public void SetDefaultCar(int carIndex, int sceneIndex)
    {
        PlayerPrefs.SetInt("DefaultCar", carIndex);
        PlayerPrefs.SetInt("DefaultScene", sceneIndex);
        PlayerPrefs.Save();
        m_DefaultCarIndex = PlayerPrefs.GetInt("DefaultCar", 0);
        m_DefaultSceneIndex = PlayerPrefs.GetInt("DefaultScene", 0);
    }

    public void SetDefaultTrail(string name)
    {
        PlayerPrefs.SetString("DefaultTrail", name);
        PlayerPrefs.Save();

        m_DefaultTrailName = PlayerPrefs.GetString("DefaultTrail", "Line");
    }

    public void LoadDefaultCarAndTrail()
    {
        //meh, default car is 0,0
        m_DefaultCarIndex = PlayerPrefs.GetInt("DefaultCar", 0);
        m_DefaultSceneIndex = PlayerPrefs.GetInt("DefaultScene", 0);
        m_DefaultTrailName = PlayerPrefs.GetString("DefaultTrail", "Line");
        ReloadCar(m_DefaultCarIndex, m_DefaultSceneIndex);
        ReloadTrail(m_DefaultTrailName);
        ChangeBackground((BackgroundEnum)m_DefaultSceneIndex);
    }

    public void ReloadDefaultTrail()
    {
        m_DefaultTrailName = PlayerPrefs.GetString("DefaultTrail", "Line");
        ReloadTrail(m_DefaultTrailName);
    }

    public void ChangeBackground(BackgroundEnum back)
    {
        BackgroundManager.current.ChangeBackground(back);
    }

    public void AddCoin(int coinCount)
    {
        gameCoins += coinCount;
        ConsistantUI.UpdateCoinAndStar();

        if(state != GameState.Running)
        {
            SaveGame();
        }
    }
    
    public void AddCoinInGame(int coinCount)
    {
        singleGameCoins += coinCount;
        ConsistantUI.UpdateInGameCoins(singleGameCoins);
    }

    public void CollectInGameCoins(bool doubleCollect = false)
    {
        int mult = doubleCollect ? 2 : 1;
        AddCoin(singleGameCoins * mult);
        singleGameCoins = 0;
    }

    public void AddStar(int starCount)
    {
        gameStars += starCount;
        ConsistantUI.UpdateCoinAndStar();

        //if (state != GameState.Running)
        {
            SaveGame();
        }
    }

    public void AddCurrency(Currency currency, int count)
    {
        if(currency == Currency.Coin)
        {
            AddCoin(count);
        }
        else
        {
            AddStar(count);
        }
    }

    public int GetCoinCount()
    {
        return gameCoins;
    }

    public void StartGame()
    {
        if (GameManager.current.state == GameManager.GameState.Start)
        {
            Player.current.Launch();
            //Player.current.playerState = Player.PlayerState.Playing;
            print("Running");
            state = GameState.Running;
            Time.timeScale = 1.0f;
            ChallengeManager.current.startTime = Time.time;
            ChallengeManager.current.getHardTimeRemain = 15.0f;
            singleGameCoins = 0;
            m_GameCount++;
            UIManager.current.ChangeStateByGameState();
            LoadAdIfNeeded();

            //AnalyticsEvent.GameStart();

            AppManager.instance.LoginOrRegister();

			UIManager.current.m_Tutorial.ShowTurnAndBoostTutorialIfFirstTime ();
            UIManager.current.m_Ingame.ShowPhaseText();

            if(isQuickstart())
            {
                StartQuickStart();
            }
        }
    }

    void StartQuickStart()
    {
        player.gameObject.AddComponent<AutoPilot>().SetPilotModeAndCount(AutoPilot.PilotMode.Score, gameHighScore * scoreForDiffScale);
        AutoPilotSpeedChange(1.5f);
        m_GlobalMultiplier = 6.0f;
        m_StartBoosting = true;
    }

    void LoadAdIfNeeded()
    {
		if(!AdRemoved() && AdManager.Instance.InterstitialNeedsLoading())
        {
            AdManager.Instance.RequestInterstitial();
        }
    }

    public void ChangeScene()
    {

    }

    public void StartCutScene()
    {
        state = GameState.CutScene;
    }

    public void AddScore(int newCollidedCount)
    {
        float add = m_DiffMultiplier * (m_BoostMultiplier * (float)newCollidedCount) * m_ItemMultiplier * m_GlobalMultiplier;
        gameScore += add;
        scoreForDifficulty += add;

        AudioSystem.current.SetScore(scoreForDifficulty);
    }

	public void SetHighScore()
    {
		if (gameScore > gameHighScore) {
			gameHighScore = (int)gameScore;
			PlayerPrefs.SetInt ("High Score",gameHighScore);
            PlayerPrefs.Save();
		}
        if(gameScore > dayHighScore)
        {
            SetDayHighScore();
        }
        RecordManager.RecordTodaysHighscore(gameScore);
        float todaysHighScore = RecordManager.GetTodaysHighscore();
        AppManager.instance.UpdateDailyLeaderboardScore((int)todaysHighScore);
	}

    public bool isQuickstart()
    {
        return DiffScoreOption == DiffScoreUsage.ScaleOffHighest;
    }

    public void SetQuickStart(bool start)
    {
        if(start && AdRemoved())
        {
            DiffScoreOption = DiffScoreUsage.ScaleOffHighest;
            RecordManager.RecordInt(GlobalKeys.m_QuickstartKey, 1);
        }
        else
        {
            DiffScoreOption = DiffScoreUsage.Normal;
            RecordManager.RecordInt(GlobalKeys.m_QuickstartKey, 0);
        }
    }

    void LoadQuickstart()
    {
        int isQS = PlayerPrefs.GetInt(GlobalKeys.m_QuickstartKey, 0);
        if (isQS != 0)
            DiffScoreOption = DiffScoreUsage.ScaleOffHighest;
        else
            DiffScoreOption = DiffScoreUsage.Normal;
    }

    void SetDayHighScore()
    {
        dayHighScore = (int)gameScore;
        PlayerPrefs.SetInt(DayHighScoreName, dayHighScore);
        PlayerPrefs.Save();

        //NetworkManager.current.SendDailyHighScore(dayHighScore);

    }

    public void SetItemMultiplier(float mult, float time)
    {
        m_ItemMultiplier = mult;
        Invoke("ResetItemMultiplier", time);
    }

    public void ResetItemMultiplier()
    {
        m_ItemMultiplier = 1.0f;
        InGameUI.Instance.EndPowerup(Powerups.DoubleScore);
    }

    public void EndGame()  //When player dies
    {
        SaveGame();
        if(m_ReviveCount < 3)
        {
            state = GameState.ReviveMenu;
            UIManager.current.ChangeStateByGameState();
        }
        else
        {
            DieAndGoToEndingUI();
        }
    }

    void DieAndGoToEndingUI()
    {
        state = GameState.Dead;
        QuestManager.UpdateQuestsStatic(QuestAction.Play, 1);
        QuestManager.UpdateQuestsStatic(QuestAction.ReachScore, (int)gameScore);
        UIManager.current.ChangeStateByGameState();
    }

    void HandleAdCountAndShowIfShould()
    {
        if (AdPlacementRecorder.ShouldPlayAd())
        {
            //AdManager.Instance.ShowBannerAd();
			if (!RateUsPanel.IsRated ()) {
				RandomShowRateUsOrInterstitial ();
			} else {
				ShowInterstitialAndResetAdTime ();
			}

        }
    }

	void RandomShowRateUsOrInterstitial()
	{
		float chance = UnityEngine.Random.value;
		if (chance < 0.5f) {
			UIManager.current.m_RateUsPanel.Show ();
			m_NextAdTime = UnityEngine.Random.Range (2, 5);
		} else {
			ShowInterstitialAndResetAdTime ();
		}
	}

	void ShowInterstitialAndResetAdTime()
	{
		if(AdManager.Instance.ShowInterstitial())
		{
			m_NextAdTime = UnityEngine.Random.Range(2, 5);
            AdPlacementRecorder.AdPlayed();
		}
	}

    public void LoadGameSave()
    {
        print(Application.persistentDataPath);
        SaveManager.GetInstance().Load();
        GameSave save = SaveManager.GetInstance().m_Data;

        gameHighScore = save.highScore;
        gameCoins = save.coin;
        gameStars = save.star;
    }

    public void SaveGame()
    {
        SaveManager.GetInstance().Save();
    }

    public void SkipRevive()
    {
        DieAndGoToEndingUI();
    }

    public void ShowReviveVideo()
    {
        if (!AdManager.Instance.ShowRewardReviveVideo())
        {
            //RevivePlayer();
        }
    }

    public void RevivePlayer()
    {
        state = GameState.Running;
        m_ReviveCount++;
        InputHandler.current.ResetControls();
        //FloorBuilder.current.RebuildFloor();
        Player.current.Revive();
        UIManager.current.ChangeStateByGameState();

        
        //UIManager.current.m_Ingame.StartReviveCountDown(3.0f);
    }

    public void ReloadAfterDelay(float delay)
    {
        StartCoroutine(ReloadAfterDelayIE(delay));
    }

    IEnumerator ReloadAfterDelayIE(float delay)
    {
        yield return new WaitForSeconds(delay);
        Reload();
    }

    public void Reload()
	{
        IDictionary<string, object> dic = new Dictionary<string, object>();
        dic.Add("Score", (int)gameScore);
        AnalyticsResult result = UnityEngine.Analytics.Analytics.CustomEvent("Player Score", dic);

        print(result);

        SaveGame();
        SetHighScore();
        QuestManager.GameFinished();


        state = GameState.AssembleTrack;
        ConsistantUI.current.EndGame();
		ResetSceneStats();
        LoadDefaultCarAndTrail();
        InputHandler.current.ResetControls();
        ItemManager.current.Reset();
        ChallengeManager.current.Reset();
        FloorBuilder.current.RebuildFloor();
        UIManager.current.ChangeStateByGameState();
		CameraFollow.current.SnapBack ();
        ObstacleBuilder.current.DestroyAllGlideCoins();
        //Scene scene = SceneManager.GetActiveScene();
        //SceneManager.LoadScene(scene.name);

        if (!AdRemoved())
        {
            AdPlacementRecorder.GamePlayed();
            HandleAdCountAndShowIfShould();
        }
    }

    public void SetNormalTimeScale(float timeScale)
    {
        m_OldTimescale = timeScale;
        // Change it when it's not slow motion
        if (Time.timeScale >= 1.0f)
        {
            SetTimeScaleBasedOnFactors();
            //Time.timeScale = m_OldTimescale;
        }
    }

    public void AddNormalTimeScale(float addedTimeScale)
    {
        m_OldTimescale += addedTimeScale;
        // Change it when it's not slow motion
        if(Time.timeScale >= 1.0f)
        {
            SetTimeScaleBasedOnFactors();
            //Time.timeScale = m_OldTimescale;
        }
    }

    public void TokiyoTomare(float slowdownFactor = 0.5f)
    {
        m_SlowmotionFactor = slowdownFactor;
        SetTimeScaleBasedOnFactors();
    }

    public void AutoPilotSpeedChange(float speedUpFactor)
    {
        m_AutoPilotSpeedUpFactor = speedUpFactor;
        SetTimeScaleBasedOnFactors();
        if(speedUpFactor == 1.0f)
            m_GlobalMultiplier = 1.0f;
    }

    void SetTimeScaleBasedOnFactors()
    {
        Time.timeScale = m_OldTimescale * m_SlowmotionFactor * m_AutoPilotSpeedUpFactor;
    }


    public bool IsPaused()
    {
        return Time.timeScale == 0;
    }

	public void Pause(bool pause, bool showPauseUI = true)
    {
        if(pause == true)
        {
            Time.timeScale = 0;
            state = GameState.Paused;
        }
        else
        {
            Time.timeScale = m_OldTimescale * m_SlowmotionFactor;
            state = GameState.Running;
        }
        print(state);
		if(showPauseUI)
        	UIManager.current.ChangeStateByGameState();
    }

	public void ShowTutorial(bool pause)
	{
		if(pause == true)
		{
			Time.timeScale = 0;
			state = GameState.Tutorial;
		}
		else
		{
			Time.timeScale = m_OldTimescale * m_SlowmotionFactor;
			state = GameState.Running;
		}
		UIManager.current.ChangeStateByGameState();
	}
	
    public void RemoveAds()
    {
        PlayerPrefs.SetInt(RemovedAD, 1);
        PlayerPrefs.Save();
    }

    public bool AdRemoved()
    {
		bool adRemoved = PlayerPrefs.HasKey (RemovedAD);
		return adRemoved;
    }

    public void LevelIncreased()
    {
        m_Level++;
        UIManager.current.m_Ingame.ShowPhaseText();
    }

    private void OnApplicationPause(bool pause)
    {
        if(state == GameState.Running && pause)
        {
            Pause(true);
        }
    }
    // Update is called once per frame
    void Update () {
	}
}
