﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MLAgents;
using MyBox;
using UnityEditor;
using UnityEngine;

namespace Gene
{
  [ExecuteInEditMode]
  public class NewbornManager : MonoBehaviour
  {
    [Header("Environment Mode")]
    public bool isTrainingMode;
    [Header("Environment parameters")]
    public int spawnerNumber;
    public int agentNumber;
    public GameObject TrainingPrefab;
    [ConditionalField("isTrainingMode")] public Vector3 agentScale;
    [ConditionalField("isTrainingMode")] public Vector3 groundScale;
    [ConditionalField("isTrainingMode")] public float floorHeight;
    [Header("Agent parameters")]
    public int minCellNb;
    public bool requestApiData;
    public string newbornId;
    [Header("Target parameters")]
    [ConditionalField("isTrainingMode")] public GameObject StaticTarget;
    [ConditionalField("isTrainingMode")] public bool isTargetDynamic;
    [Header("Academy parameters")]
    public Academy academy;
    public bool control;
    [Header("Brain parameters")]
    public Brain brainObject;
    public int vectorObservationSize;
    public int vectorActionSize;
    public TextAsset brainModel;
    [Header("Camera parameters")]

    [HideInInspector] public List<GameObject> Spawners = new List<GameObject>();
    public void DeleteSpawner()
    {
      Transform[] childs = transform.Cast<Transform>().ToArray();
      foreach (Transform child in childs)
      {
        DestroyImmediate(child.gameObject);
      }
      Spawners.Clear();
      academy.broadcastHub.broadcastingBrains.Clear();
    }

    public void BuildSpawners()
    {
      GameObject parent = transform.gameObject;
      int floor = 0;
      int squarePosition = 0;

      for (var i = 0; i < spawnerNumber; i++)
      {
        GameObject spawner;
        Brain brain = Instantiate(brainObject);

        if (isTrainingMode && i % 4 == 0)
        {
          floor++;
          squarePosition = 0;
          parent = CreateTrainingFloor(floor);
          NameFloor(parent, floor);
        }

        Spawners.Add(InstantiateSpawner(parent, floor, squarePosition, out spawner));

        if (isTrainingMode)
        {
          PositionTrainingSpawner(squarePosition, spawner);
        }
        else
        {
          // Randomly place the agents.
        }

        SetBrainParams(brain, Regex.Replace(System.Guid.NewGuid().ToString(), @"[^0-9]", ""));

        if (!isTargetDynamic && isTrainingMode)
        {
          Instantiate(StaticTarget, spawner.transform);
        }

        for (int y = 0; y < agentNumber; y++)
        {
          AgentTrainBehaviour atBehaviour;
          NewBornBuilder newBornBuilder;
          Newborn newborn;
          GameObject newBornAgent;
          spawner.GetComponent<NewbornSpawner>().Agents.Add(spawner.GetComponent<NewbornSpawner>().BuildAgent(spawner, out newBornAgent, out atBehaviour, out newBornBuilder, out newborn));
          AddBrainToAgentBehaviour(atBehaviour, brain);
          SetApiRequestParameter(newBornBuilder, atBehaviour, requestApiData);
          AddMinCellNb(newBornBuilder, minCellNb);
        }

        AssignTarget(spawner.GetComponent<NewbornSpawner>().Agents);

        squarePosition++;
      }
    }

    private void AssignTarget(List<GameObject> newBornAgents)
    {
      for (int y = 0; y < newBornAgents.Count; y++)
      {
        if (isTargetDynamic)
        {
          if (y != newBornAgents.Count - 1)
          {
            newBornAgents[y].GetComponent<AgentTrainBehaviour>().target = newBornAgents[y + 1].transform;
          }
          else
          {
            newBornAgents[y].GetComponent<AgentTrainBehaviour>().target = newBornAgents[0].transform;
          }
        }
        else
        {
          newBornAgents[y].GetComponent<AgentTrainBehaviour>().target = StaticTarget.transform;
        }
      }
    }

    public void PostTrainingNewborns()
    {
      Debug.Log("Posting training NewBorns to the server...");
      string generationId = GenerationService.generations[GenerationService.generations.Count - 1]; // Get the latest generation;
      GameObject[] agentList = GameObject.FindGameObjectsWithTag("agent");
      for (int agent = 0; agent < agentList.Length; agent++)
      {
        Newborn newborn = agentList[agent].transform.GetComponent<Newborn>();
        NewBornBuilder newBornBuilder = agentList[agent].transform.GetComponent<NewBornBuilder>();
        AgentTrainBehaviour agentTrainBehaviour = agentList[agent].transform.GetComponent<AgentTrainBehaviour>();
        string newbornId = agentTrainBehaviour.brain.name;
        string newbornName = newborn.title;
        string newbornSex = newborn.Sex;
        string newbornHex = "mock hex";
        // DO a generation check ? 
        NewBornPostData newBornPostData = new NewBornPostData(newbornName, newbornId, generationId, newbornSex, newbornHex);
        newBornBuilder.PostNewborn(newBornPostData, agentList[agent]);
      }
    }

    public IEnumerator PostGeneration(int generationIndex)
    {
      yield return StartCoroutine(GenerationService.PostGeneration(Regex.Replace(System.Guid.NewGuid().ToString(), @"[^0-9]", ""), generationIndex));
    }

    public IEnumerator RequestGenerations()
    {
      yield return StartCoroutine(GenerationService.GetGenerations());
    }

    public IEnumerator RequestNewbornAgentInfo()
    {
      Debug.Log("Request Agent info from server...");
      GameObject[] agentsObject = GameObject.FindGameObjectsWithTag("agent");
      for (int a = 0; a < agentsObject.Length; a++)
      {
        yield return StartCoroutine(NewbornService.GetNewborn(newbornId, agentsObject[a], false));
      }
      Debug.Log("Finished to build Agents");
      academy.InitializeEnvironment();
      academy.initialized = true;
    }

    public void RequestNewborn()
    {
      StartCoroutine(RequestNewbornAgentInfo());
    }

    public void RequestProductionAgentInfo()
    {
      foreach (GameObject agent in GameObject.FindGameObjectsWithTag("agent"))
      {
        Debug.Log(agent.name);
      }
    }

    private void SetBrainParams(Brain brain, string brainName)
    {
      brain.name = brainName;
      brain.brainParameters.vectorActionSize = new int[1] { vectorActionSize };
      academy.broadcastHub.broadcastingBrains.Add(brain);
      academy.broadcastHub.SetControlled(brain, control);
    }

    private GameObject CreateTrainingFloor(int floor)
    {
      GameObject trainingFloor = new GameObject();
      trainingFloor.name = "Floor" + floor;
      trainingFloor.transform.parent = transform;
      trainingFloor.transform.localPosition = new Vector3(0f, floorHeight * floor, 0f);
      return trainingFloor;
    }

    private static void PositionTrainingSpawner(int squarePosition, GameObject spawner)
    {
      Transform spawnerTransform = spawner.transform;
      Vector3 spawnerTransformGroundScale = spawnerTransform.Find("Ground").transform.localScale;
      switch (squarePosition)
      {
        case 0:
          spawnerTransform.localPosition = new Vector3(0f, 0f, 0f);
          break;
        case 1:
          spawnerTransform.localPosition = new Vector3(spawnerTransformGroundScale.x, 0f, 0f);
          break;
        case 2:
          spawnerTransform.localPosition = new Vector3(0f, 0f, spawnerTransformGroundScale.z);
          break;
        case 3:
          spawnerTransform.localPosition = new Vector3(spawnerTransformGroundScale.x, 0f, spawnerTransformGroundScale.z);
          break;
      }
    }

    private static void PositionProductionSpawner()
    {
      Debug.Log("TO-DO: Position the production spawner");
    }

    private GameObject InstantiateSpawner(GameObject parent, int floor, int squarePosition, out GameObject spawner)
    {
      spawner = Instantiate(TrainingPrefab, parent.transform);
      spawner.name = ("Spawner" + squarePosition);
      spawner.transform.localScale = groundScale;
      return spawner;
    }

    private void NameFloor(GameObject trainingFloor, int floor)
    {
      trainingFloor.name = "Floor" + floor;
      trainingFloor.transform.parent = transform;
    }

    private void AddMinCellNb(NewBornBuilder newBornBuilder, int minCellNb)
    {
      newBornBuilder.minCellNb = minCellNb;
    }

    private void SetApiRequestParameter(NewBornBuilder newBornBuilder, AgentTrainBehaviour atBehaviour, bool requestApiData)
    {
      atBehaviour.requestApiData = requestApiData;
      newBornBuilder.requestApiData = requestApiData;
    }

    private void AddBrainToAgentBehaviour(AgentTrainBehaviour atBehaviour, Brain brain)
    {
      atBehaviour.brain = brain;
    }

    public void ClearBroadCastingBrains()
    {
      academy.broadcastHub.broadcastingBrains.Clear();
    }
  }
}