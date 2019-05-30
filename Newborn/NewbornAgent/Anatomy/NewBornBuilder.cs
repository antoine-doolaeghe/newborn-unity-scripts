﻿using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MLAgents;
using UnityEngine;

namespace Gene
{
  [ExecuteInEditMode]
  public class NewBornBuilder : MonoBehaviour
  {
    private Newborn newborn;
    [Header("Connection to API Service")]
    public NewbornService newbornService;
    public bool requestApiData;
    public int cellNb = 0;
    public int minCellNb;
    private int cellInfoIndex = 0;
    private bool Initialised = false;
    private AgentTrainBehaviour aTBehaviour;
    private NewbornManager trainingManager;
    [HideInInspector] public float threshold;
    [HideInInspector] public int partNb;
    public List<GeneInformation> GeneInformations;

    void Awake()
    {
      newborn = transform.GetComponent<Newborn>();
    }
    public void DeleteCells()
    {
      newborn.Cells.Clear();
      newborn.NewBornGenerations.Clear();
      newborn.CellPositions.Clear();
      newborn.CelllocalPositions.Clear();
      cellInfoIndex = 0;
      Initialised = false;
      GeneInformations.Clear();
      cellNb = 0;
    }

    public void BuildNewBorn(int generationNumber, float threshold)
    {
      SetAgentNameFromBrainName();
      InitaliseNewbornInformation();

      if (GeneInformations.Count == 0)
      {
        GeneInformations.Add(new GeneInformation(new List<float>()));
      }

      newborn.NewBornGenerations.Add(new List<GameObject>());
      GameObject initCell = InitBaseShape(newborn.NewBornGenerations[0], 0);
      initCell.transform.parent = transform;
      AnatomyHelpers.InitRigidBody(initCell);
      StoreNewbornCell(initCell, initCell.transform.position, initCell.transform.position);
      for (int y = 1; y < generationNumber; y++)
      {
        int previousGenerationCellNumber = newborn.NewBornGenerations[y - 1].Count;
        newborn.NewBornGenerations.Add(new List<GameObject>());
        for (int i = 0; i < previousGenerationCellNumber; i++)
        {
          for (int z = 0; z < AnatomyHelpers.Sides.Count; z++)
          {
            if (!requestApiData || cellInfoIndex < GeneInformations[0].info.Count)
            {
              bool IsValidPosition = true;
              float cellInfo = 0f;
              Vector3 cellPosition = newborn.NewBornGenerations[y - 1][i].transform.position + AnatomyHelpers.Sides[z];
              IsValidPosition = AnatomyHelpers.IsValidPosition(newborn, IsValidPosition, cellPosition);
              cellInfo = HandleCellInfos(0, cellInfoIndex);
              cellInfoIndex++;
              if (IsValidPosition)
              {
                if (cellInfo > threshold)
                {
                  BuildCell(y, i, z, cellPosition);
                }
              }
            }
          }
        }
      }


      foreach (var cell in newborn.Cells)
      {
        cell.transform.parent = transform;
        cell.GetComponent<SphereCollider>().radius /= 2f;
      }

      cellNb = newborn.Cells.Count;
    }

    public void BuildNewGeneration(int generationInfo, bool isAfterRequest)
    {
      int indexInfo = 0;
      int previousGenerationCellNumber = 0;
      int germNb = 0;
      partNb += 1;


      for (int i = 0; i < newborn.NewBornGenerations.Count; i++)
      {
        if (newborn.NewBornGenerations[i].Count > 0)
        {
          previousGenerationCellNumber = newborn.NewBornGenerations[i].Count;
          germNb = i;
        }
        else
        {
          newborn.NewBornGenerations.RemoveAt(i);
        }
      }

      if (!isAfterRequest)
      {
        GeneInformations.Add(new GeneInformation(new List<float>()));
      }

      newborn.NewBornGenerations.Add(new List<GameObject>());

      for (int i = 0; i < previousGenerationCellNumber; i++)
      {
        for (int z = 0; z < AnatomyHelpers.Sides.Count; z++)
        {
          bool IsValidPosition = true;
          float cellInfo = 0f;
          Vector3 cellPosition = newborn.NewBornGenerations[germNb][i].transform.position + AnatomyHelpers.Sides[z];
          IsValidPosition = AnatomyHelpers.IsValidPosition(newborn, IsValidPosition, cellPosition);
          cellInfo = HandleCellInfos(GeneInformations.Count - 1, indexInfo);
          indexInfo++;
          if (IsValidPosition)
          {
            if (cellInfo > threshold)
            {
              GameObject cell = InitBaseShape(newborn.NewBornGenerations[germNb + 1], germNb + 1);
              AnatomyHelpers.InitPosition(AnatomyHelpers.Sides, germNb + 1, i, z, cell, newborn);
              AnatomyHelpers.InitRigidBody(cell);
              AnatomyHelpers.InitJoint(cell, newborn.NewBornGenerations[germNb][i], AnatomyHelpers.Sides[z], germNb + 1, z);
              cell.transform.parent = transform;
              StoreNewbornCell(cell, cellPosition, cellPosition);
            }
          }
        }
      }
      cellNb = newborn.Cells.Count;
      AddBodyPart(false);
    }

    public void BuildNewBornFromFetch()
    {
      Debug.Log("Building Newborn From Fetch");
      if (partNb == 0 && threshold == 0f)
      {
        partNb = AgentConfig.layerNumber;
        threshold = AgentConfig.threshold;
      }

      requestApiData = true;
      handleCellInfoResponse();
    }

    public IEnumerator BuildAgentRandomNewBorn()
    {
      yield return StartCoroutine(GenerationService.GetGenerations()); /// This check should be made as you build the AGENT and not as you post the agents.
      if (GenerationService.generations.Count == 0)
      {
        yield return StartCoroutine(GenerationService.PostGeneration(Regex.Replace(System.Guid.NewGuid().ToString(), @"[^0-9]", ""), 1));
      }
      // Handle starting/communication with api data
      AgentTrainBehaviour atBehaviour = transform.GetComponent<AgentTrainBehaviour>();
      Newborn newborn = transform.GetComponent<Newborn>();
      newborn.GenerationIndex = GenerationService.generations.Count;
      newborn.GenerationId = GenerationService.generations[newborn.GenerationIndex - 1];
      requestApiData = false;
      BuildNewBorn(AgentConfig.layerNumber, AgentConfig.threshold);
      checkMinCellNb();
      AddBodyPart(true);
      NewbornBrain.SetBrainParameters(atBehaviour, cellNb);
    }

    public void BuildAgentRandomGeneration(Transform agent)
    {
      NewBornBuilder newBornBuilder = agent.GetComponent<NewBornBuilder>();
      AgentTrainBehaviour atBehaviour = agent.GetComponent<AgentTrainBehaviour>();
      newBornBuilder.threshold = AgentConfig.threshold;
      // SetApiRequestParameter(newBornBuilder, atBehaviour, false);
      newBornBuilder.BuildNewGeneration(newBornBuilder.GeneInformations.Count, false);
      Brain brain = Resources.Load<Brain>("Brains/agentBrain0");
      // NewbornBrain.SetBrainParams(brain, brain.name);
      agent.gameObject.name = brain + "";
      brain.brainParameters.vectorActionSpaceType = SpaceType.continuous;
      brain.brainParameters.vectorActionSize = new int[1] { agent.transform.GetComponent<NewBornBuilder>().cellNb * 3 };
      brain.brainParameters.vectorObservationSize = agent.transform.GetComponent<NewBornBuilder>().cellNb * 13 - 4;
      atBehaviour.brain = brain;
    }

    public void handleCellInfoResponse()
    {
      List<float> cellInfoResponse = NewbornService.cellInfoResponse;
      if (cellInfoResponse.Count != 0 && !Initialised)
      {
        GeneInformations.Add(new GeneInformation(new List<float>()));
        for (int i = 0; i < cellInfoResponse.Count; i++)
        {
          GeneInformations[0].info.Add(cellInfoResponse[i]);
        }
        BuildNewBorn(partNb, threshold);
        checkMinCellNb();
        AddBodyPart(true);
        Initialised = true;
      }
    }
    public List<float> ReturnGeneInformations(int modelIndex)
    {
      List<float> ModelInfos = new List<float>();

      for (int i = 0; i < GeneInformations[modelIndex].info.Count; i++)
      {
        ModelInfos.Add(GeneInformations[modelIndex].info[i]);
      }

      return ModelInfos;
    }

    public void PostNewborn(NewBornPostData newBornPostData, GameObject agent)
    {
      StartCoroutine(NewbornService.PostNewborn(newBornPostData, agent));
    }

    public IEnumerator PostNewbornModel(string newbornId, int modelIndex, GameObject agent, NewbornService.BuildAgentCallback responseCallback)
    {
      List<float> modelInfos = ReturnGeneInformations(modelIndex);
      List<PositionPostData> cellPositions = AnatomyHelpers.ReturnModelPositions(newborn);
      string id = Regex.Replace(System.Guid.NewGuid().ToString(), @"[^0-9]", "");
      GenerationPostData generationPostData = new GenerationPostData(newbornId, cellPositions, modelInfos);
      yield return NewbornService.PostNewbornModel(transform, generationPostData, newbornId, agent, responseCallback);
    }

    private void StoreNewbornCell(GameObject cell, Vector3 cellPosition, Vector3 cellLocalPosition)
    {
      newborn.Cells.Add(cell);
      newborn.CellPositions.Add(cellPosition);
      newborn.CelllocalPositions.Add(cellLocalPosition);
    }

    private float HandleCellInfos(int generationIndex, int cellIndex)
    {
      if (requestApiData)
      {
        float cellInfo = GeneInformations[generationIndex].info[cellIndex];
        return cellInfo;
      }
      else
      {
        float cellInfo = Random.Range(0f, 1f);
        GeneInformations[generationIndex].info.Add(cellInfo);
        return cellInfo;
      }
    }

    private GameObject InitBaseShape(List<GameObject> NewBornGeneration, int y)
    {
      NewBornGeneration.Add(Instantiate(newborn.CellPrefab));
      GameObject cell = newborn.NewBornGenerations[y][newborn.NewBornGenerations[y].Count - 1];
      cell.transform.position = transform.position;
      return cell;
    }

    private void AddBodyPart(bool init)
    {
      aTBehaviour = transform.gameObject.GetComponent<AgentTrainBehaviour>();
      aTBehaviour.initPart = newborn.Cells[0].transform;
      for (int i = 1; i < cellNb; i++)
      {
        if (aTBehaviour.parts.Count < i)
        {
          aTBehaviour.parts.Add(newborn.Cells[i].transform);
        }
      }
      if (init)
      {
        aTBehaviour.initBodyParts();
      }
    }

    private void InitaliseNewbornInformation()
    {
      newborn.title = transform.gameObject.name;
      newborn.NewBornGenerations = new List<List<GameObject>>();
      newborn.Cells = new List<GameObject>();
      newborn.CellPositions = new List<Vector3>();
      newborn.CelllocalPositions = new List<Vector3>();
    }
    private void BuildCell(int y, int i, int z, Vector3 cellPosition)
    {
      GameObject cell = InitBaseShape(newborn.NewBornGenerations[y], y);
      AnatomyHelpers.InitPosition(AnatomyHelpers.Sides, y, i, z, cell, newborn);
      AnatomyHelpers.InitRigidBody(cell);
      AnatomyHelpers.InitJoint(cell, newborn.NewBornGenerations[y - 1][i], AnatomyHelpers.Sides[z], y, z);
      cell.transform.parent = transform;
      StoreNewbornCell(cell, cellPosition, cellPosition);
    }

    private void SetAgentNameFromBrainName()
    {
      transform.gameObject.name = transform.GetComponent<AgentTrainBehaviour>().brain + "";
    }

    private void checkMinCellNb()
    {
      if (cellNb < minCellNb)
      {
        Debug.Log("Killin Object (less that requiered size");
        transform.gameObject.SetActive(false);
      }
    }

  }
}