﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

[System.Serializable]
public class CarSave
{
    public List<CarSaveData> m_Cars;
    
    public CarSave()
    {
        m_Cars = new List<CarSaveData>();
    }
}

[System.Serializable]
public class TrailSave
{
    public List<string> m_TrailNames;

    public TrailSave()
    {
        m_TrailNames = new List<string>();
    }
}

[System.Serializable]
public class GameSave
{
    public int coin;
    public int star;
    public int highScore;
    public CarSave m_Cars;
    public TrailSave m_Trails;
    public string version;

    public GameSave()
    {
        coin = 0;
        star = 0;
        highScore = 0;

        // init first car save
        m_Cars = new CarSave();
        m_Trails = new TrailSave();
    }
}

public class SaveManager {
    public static SaveManager instance;

    public static SaveManager GetInstance()
    {
        if(instance == null)
        {
            instance = new SaveManager();
        }
        return instance;
    }
    public GameSave m_Data;

    // Purchased cars
    Dictionary<string, CarSaveData> m_CarDict;

    public CarSelectData CarStorer
    {
        get
        {
            if (m_CarStorer == null)
            {
                m_CarStorer = (CarSelectData)Resources.Load(m_DataPath);
            }
            return m_CarStorer;
        }
        set
        { }
    }
    CarSelectData m_CarStorer;

    const string m_DataPath = "ScriptableObjects/CarSelectData";

    public SaveManager()
    {
        m_CarDict = new Dictionary<string, CarSaveData>();
    }

    public void Save()
    {
        Debug.Log("save");
        BinaryFormatter bf = new BinaryFormatter();
        
        System.IO.FileInfo filepath = new System.IO.FileInfo(Application.persistentDataPath);
        filepath.Directory.Create();

		string path = Application.persistentDataPath + "/savedGames.gd";
		FileStream file = File.Create(Application.persistentDataPath + "/savedGames.gd"); //you can call it anything you want

		m_Data.coin = GameManager.current.GetCoinCount();
		m_Data.star = GameManager.current.gameStars;
		m_Data.highScore = GameManager.current.gameHighScore;
		bf.Serialize(file, m_Data);
		file.Close();
    }

    public void Load()
    {
        if (File.Exists(Application.persistentDataPath + "/savedGames.gd"))
		{
			BinaryFormatter bf = new BinaryFormatter();
			string path = Application.persistentDataPath + "/savedGames.gd";
			FileStream file = File.Open(path, FileMode.Open);
            try
			{
                m_Data = (GameSave)bf.Deserialize(file);
            }
            catch (System.Exception e)
            {
                Debug.Log(e);
                m_Data = new GameSave();
			}
			file.Close();

            if(m_Data.m_Cars == null)
            {
                m_Data.m_Cars = new CarSave();
                AcquireCar(0, 0);
            }
            else
            {
                foreach (var car in m_Data.m_Cars.m_Cars)
                {
                    if(!m_CarDict.ContainsKey(car.m_Name))
                    {
                        m_CarDict.Add(car.m_Name, car);
                    }
                }
            }

            if (m_Data.m_Trails == null)
            {
                m_Data.m_Trails = new TrailSave();
                AcquireTrail("Line");
                AcquireTrail("LineSky");
            }
        }
        else
        {
            Debug.Log("Load new ");
            m_Data = new GameSave();
            GetDefaultCarAndTrails();
        }
    }


    public void GetDefaultCarAndTrails()
    {
        AcquireCar(0, 0);
        AcquireCar(0, 1);
        AcquireTrail("LINE");
        AcquireTrail("CLOUD");
        AcquireTrail("NONE");
    }

    public bool BuyCarWithCoin(int carIndex, int sceneIndex)
    {
        int price = CarSelectDataReader.Instance.GetCarData(carIndex, sceneIndex).coinPrice;
        if (GameManager.current.gameCoins >= price)
        {
            GameManager.current.AddCoin(-price);
            AcquireCar(carIndex, sceneIndex);

            return true;
        }
        return false;
    }


    public bool BuyCarWithStar(int carIndex, int sceneIndex)
    {
        int price = CarSelectDataReader.Instance.GetCarData(carIndex, sceneIndex).starPrice;
        if (GameManager.current.gameStars >= price)
        {
            GameManager.current.AddStar(-price);
            AcquireCar(carIndex, sceneIndex);

            return true;
        }
        return false;
    }


    public void AcquireCar(int carIndex, int sceneIndex)
    {
        CarSaveData first = new CarSaveData();
        first.m_CarIndex = carIndex;
        first.m_SceneIndex = sceneIndex;
        first.m_Name = CarStorer.GetCarData(carIndex, sceneIndex).name;

        if (HasCar(first.m_Name)) return;

        m_Data.m_Cars.m_Cars.Add(first);
        m_CarDict.Add(first.m_Name, first);

        Save();
    }
    
    public bool BuyTrail(string name)
    {
        int price = CarSelectDataReader.Instance.GetTrailSelectData(name).price;
        if (GameManager.current.gameStars >= price)
        {
            if (!m_Data.m_Trails.m_TrailNames.Contains(name))
            {
                GameManager.current.AddStar(-price);
                m_Data.m_Trails.m_TrailNames.Add(name);

                Save();
            }
            return true;
        }
        return false;
    }

    public void AcquireTrail(string name)
    {
        if (!m_Data.m_Trails.m_TrailNames.Contains(name))
        {
            m_Data.m_Trails.m_TrailNames.Add(name);

            Save();
        }
    }

    public bool BuyCarUpgrade(string carName, CarUpgradeCatagory type)
    {
        if(HasCar(carName))
        {
            CarSaveData data = GetSavedCarData(carName);
            SingleCarSelectData carData = CarSelectDataReader.Instance.GetCarData(carName);
            bool success = false;

            switch (type)
            {
                case CarUpgradeCatagory.Accelerate:
                    if (data.m_AccLevel < carData.maxUpgradeLevel && 
                        GameManager.current.gameCoins >= carData.GetUpgradePrice(data.m_AccLevel, type))
                    {
                        GameManager.current.AddCoin(-carData.GetUpgradePrice(data.m_AccLevel, type));
                        data.m_AccLevel++;
                        success = true;
                    }
                    break;
                case CarUpgradeCatagory.Boost:
                    if (data.m_BoostLevel < carData.maxUpgradeLevel &&
                        GameManager.current.gameCoins >= carData.GetUpgradePrice(data.m_BoostLevel, type))
                    {
                        GameManager.current.AddCoin(-carData.GetUpgradePrice(data.m_BoostLevel, type));
                        data.m_BoostLevel++;
                        success = true;
                    }
                    break;
                case CarUpgradeCatagory.Handling:
                    if (data.m_HandlingLevel < carData.maxUpgradeLevel &&
                        GameManager.current.gameCoins >= carData.GetUpgradePrice(data.m_HandlingLevel, type))
                    {
                        GameManager.current.AddCoin(-carData.GetUpgradePrice(data.m_HandlingLevel, type));
                        data.m_HandlingLevel++;
                        success = true;
                    }
                    break;
            }
            if(success)
            {
                Save();
                return true;
            }
        }
        return false;
    }

    public bool HasCar(string name)
    {
        return m_CarDict.ContainsKey(name);
    }

    public bool HasTrail(string name)
    {
        return m_Data.m_Trails.m_TrailNames.Contains(name);
    }

    public CarSaveData GetSavedCarData(string name)
    {
        if (m_CarDict.ContainsKey(name))
            return m_CarDict[name];
        else
            return null;
    }
}
