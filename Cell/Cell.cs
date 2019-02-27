﻿using System.Collections;
using System.Collections.Generic;
using MLAgents;
using UnityEngine;

namespace Gene
{
  public class Cell : MonoBehaviour
  {
    public GameObject CellPrefab;
    [Header("Connection to API Service")]
    public NewbornService newbornService;
    public AgentConfig agentConfig;
    public bool postApiData;
    public bool requestApiData;
    public string cellId;
    public int cellNb = 0;
    public int minCellNb;
    private int cellInfoIndex = 0;
    private bool initialised;
    public List<List<GameObject>> Germs;
    public List<GameObject> Cells;
    public List<Vector3> CellPositions;

    public List<Vector3> CelllocalPositions;
    private AgentTrainBehaviour aTBehaviour;
    private List<Vector3> sides = new List<Vector3> {
                new Vector3 (1f, 0f, 0f),
                new Vector3 (0f, 1f, 0f),
                new Vector3 (0f, 0f, 1f),
                new Vector3 (-1f, 0f, 0f),
                new Vector3 (0f, -1f, 0f),
                new Vector3 (0f, 0f, -1f)
            };

    private Vector3 childPositionSum = new Vector3(0f, 0f, 0f);

    [HideInInspector] public Vector3 center;
    [HideInInspector] public bool isRequestDone;
    [HideInInspector] public float threshold;
    [HideInInspector] public int partNb;
    [HideInInspector] public List<List<float>> GenerationInfos = new List<List<float>>();


    void Awake()
    {
      initialised = false;
    }

    void Update()
    {
      childPositionSum = new Vector3(0f, 0f, 0f);
      foreach (Transform child in transform)
      {
        childPositionSum += child.transform.position;
      }

      center = (childPositionSum / transform.childCount);
    }

    public void DeleteCells()
    {
      Cells.Clear();
      Germs.Clear();
      CellPositions.Clear();
      CelllocalPositions.Clear();
      cellInfoIndex = 0;
      initialised = false;
      GenerationInfos.Clear();
      cellNb = 0;
    }

    public void handleResponseData()
    {
      List<float> response = newbornService.response;
      if (response.Count != 0 && !initialised)
      {
        GenerationInfos.Add(new List<float>());
        for (int i = 0; i < response.Count; i++)
        {
          GenerationInfos[0].Add(response[i]);
        }
        initGerms(partNb, threshold);
        initialised = true;
      }
    }

    public void initGerms(int numGerms, float threshold)
    {
      transform.gameObject.name = transform.GetComponent<AgentTrainBehaviour>().brain + "";
      Germs = new List<List<GameObject>>();
      Cells = new List<GameObject>();
      CellPositions = new List<Vector3>();
      CelllocalPositions = new List<Vector3>();
      if (GenerationInfos.Count == 0)
      {
        GenerationInfos.Add(new List<float>());
      }
      Germs.Add(new List<GameObject>());
      GameObject initCell = InitBaseShape(Germs[0], 0);
      initCell.transform.parent = transform;
      InitRigidBody(initCell);
      HandleStoreCell(initCell, initCell.transform.position, initCell.transform.position);
      for (int y = 1; y < numGerms; y++)
      {
        int prevCount = Germs[y - 1].Count;
        Germs.Add(new List<GameObject>());
        for (int i = 0; i < prevCount; i++)
        {
          for (int z = 0; z < sides.Count; z++)
          {
            if (!requestApiData || cellInfoIndex < GenerationInfos[0].Count)
            {
              bool isValid = true;
              float cellInfo = 0f;
              Vector3 cellPosition = Germs[y - 1][i].transform.position + sides[z];
              isValid = CheckIsValid(isValid, cellPosition);
              cellInfo = HandleCellInfos(0, cellInfoIndex);
              cellInfoIndex++;
              if (isValid)
              {
                if (cellInfo > threshold)
                {
                  GameObject cell = InitBaseShape(Germs[y], y);
                  InitPosition(sides, y, i, z, cell);
                  InitRigidBody(cell);
                  initJoint(cell, Germs[y - 1][i], sides[z], y, z);
                  cell.transform.parent = transform;
                  HandleStoreCell(cell, cellPosition, cellPosition);
                }
              }
            }
          }
        }
      }


      foreach (var cell in Cells)
      {
        cell.transform.parent = transform; // RESET CELL TO MAIN TRANSFORM
        cell.GetComponent<SphereCollider>().radius /= 2f;
      }

      cellNb = Cells.Count;

      checkMinCellNb();
      AddAgentPart(true);
    }

    public void AddGeneration(int generationInfo, bool isAfterRequest)
    {
      int indexInfo = 0;
      int prevCount = 0;
      int germNb = 0;
      partNb += 1;


      for (int i = 0; i < Germs.Count; i++)
      {
        if (Germs[i].Count > 0)
        {
          prevCount = Germs[i].Count;
          germNb = i;
        }
        else
        {
          Germs.RemoveAt(i);
        }
      }
      if (!isAfterRequest)
      {
        GenerationInfos.Add(new List<float>());
      }

      Germs.Add(new List<GameObject>());

      for (int i = 0; i < prevCount; i++)
      {
        for (int z = 0; z < sides.Count; z++)
        {
          bool isValid = true;
          float cellInfo = 0f;
          Vector3 cellPosition = Germs[germNb][i].transform.position + sides[z];
          isValid = CheckIsValid(isValid, cellPosition);
          cellInfo = HandleCellInfos(GenerationInfos.Count - 1, indexInfo);
          indexInfo++;
          if (isValid)
          {
            if (cellInfo > threshold)
            {
              GameObject cell = InitBaseShape(Germs[germNb + 1], germNb + 1);
              InitPosition(sides, germNb + 1, i, z, cell);
              InitRigidBody(cell);
              initJoint(cell, Germs[germNb][i], sides[z], germNb + 1, z);
              cell.transform.parent = transform;
              HandleStoreCell(cell, cellPosition, cellPosition);
            }
          }
        }
      }
      cellNb = Cells.Count;
      AddAgentPart(false);
    }

    private void checkMinCellNb()
    {
      if (cellNb < minCellNb)
      {
        Debug.Log("Killin Object (less that requiered size");
        transform.gameObject.SetActive(false);
      }
    }

    public List<float> ReturnGenerationInfos(int generationId)
    {
      List<float> generationInfos = new List<float>();

      for (int i = 0; i < GenerationInfos[generationId].Count; i++)
      {
        generationInfos.Add(GenerationInfos[generationId][i]);
      }

      return generationInfos;
    }

    public List<List<float>> ReturnGenerationPositions()
    {
      List<List<float>> positions = new List<List<float>>();
      for (int i = 0; i < CelllocalPositions.Count; i++)
      {
        List<float> position = new List<float>();
        position.Add(CelllocalPositions[i].x);
        position.Add(CelllocalPositions[i].y);
        position.Add(CelllocalPositions[i].z);
        positions.Add(position);
      }
      return positions;
    }

    public void PostCell()
    {
      string newBornName = "\"cellName\"";
      int newBornId = Random.Range(1, 1000);
      NewBornPostData newBornPostData = new NewBornPostData(newBornName, newBornId);
      newbornService.postNewborn(newBornPostData, 0);
    }

    public void PostGeneration(string newbornId, int generationId, int agentId)
    {
      List<float> cellInfos = ReturnGenerationInfos(generationId);
      List<List<float>> cellPositions = ReturnGenerationPositions();
      int id = Random.Range(1, 1000);
      // cellGenerations.Add(new Generation(cellInfos, cellPositions));
      GenerationPostData generationPostData = new GenerationPostData(id, cellPositions, cellInfos);
      StartCoroutine(newbornService.postGeneration(generationPostData, newbornId, agentId));
    }

    private void HandleStoreCell(GameObject cell, Vector3 cellPosition, Vector3 cellLocalPosition)
    {
      Cells.Add(cell);
      CellPositions.Add(cellPosition);
      CelllocalPositions.Add(cellLocalPosition);
    }

    private float HandleCellInfos(int generationIndex, int cellIndex)
    {
      if (requestApiData)
      {
        float cellInfo = GenerationInfos[generationIndex][cellIndex];
        return cellInfo;
      }
      else
      {
        float cellInfo = Random.Range(0f, 1f);
        GenerationInfos[generationIndex].Add(cellInfo);
        return cellInfo;
      }
    }



    private static void InitRigidBody(GameObject cell)
    {
      cell.AddComponent<Rigidbody>();
      cell.GetComponent<Rigidbody>().useGravity = true;
      cell.GetComponent<Rigidbody>().mass = 1f;
    }

    private void InitPosition(List<Vector3> sides, int y, int i, int z, GameObject cell)
    {
      cell.transform.parent = Germs[y - 1][i].transform;
      cell.transform.localPosition = sides[z];
    }

    private GameObject InitBaseShape(List<GameObject> germs, int y)
    {
      germs.Add(Instantiate(CellPrefab));
      GameObject cell = Germs[y][Germs[y].Count - 1];
      cell.transform.position = transform.position;
      return cell;
    }

    private bool CheckIsValid(bool isValid, Vector3 cellPosition)
    {
      foreach (var position in CellPositions)
      {
        if (cellPosition == position)
        {
          isValid = false;
        }
      }

      return isValid;
    }

    private void initJoint(GameObject part, GameObject connectedBody, Vector3 jointAnchor, int y, int z)
    {
      ConfigurableJoint cj = part.transform.gameObject.AddComponent<ConfigurableJoint>();
      cj.xMotion = ConfigurableJointMotion.Locked;
      cj.yMotion = ConfigurableJointMotion.Locked;
      cj.zMotion = ConfigurableJointMotion.Locked;
      cj.angularXMotion = ConfigurableJointMotion.Limited;
      cj.angularYMotion = ConfigurableJointMotion.Limited;
      cj.angularZMotion = ConfigurableJointMotion.Limited;
      cj.anchor = -jointAnchor;
      cj.connectedBody = connectedBody.gameObject.GetComponent<Rigidbody>();
      cj.rotationDriveMode = RotationDriveMode.Slerp;
      cj.angularYLimit = new SoftJointLimit() { limit = agentConfig.yLimit, bounciness = agentConfig.bounciness };
      handleAngularLimit(cj, jointAnchor);
      cj.angularZLimit = new SoftJointLimit() { limit = agentConfig.zLimit, bounciness = agentConfig.bounciness };
      part.gameObject.GetComponent<Rigidbody>().useGravity = true;
      part.gameObject.GetComponent<Rigidbody>().mass = 1f;
    }

    private void handleAngularLimit(ConfigurableJoint cj, Vector3 jointAnchor)
    {
      if(jointAnchor.y == -1) {
        cj.lowAngularXLimit = new SoftJointLimit() { limit = -agentConfig.highLimit, bounciness = agentConfig.bounciness };
        cj.highAngularXLimit = new SoftJointLimit() { limit = -agentConfig.lowLimit, bounciness = agentConfig.bounciness };
      } else if(jointAnchor.y == 1) {
        cj.highAngularXLimit = new SoftJointLimit() { limit = agentConfig.highLimit, bounciness = agentConfig.bounciness };
        cj.lowAngularXLimit = new SoftJointLimit() { limit = agentConfig.lowLimit, bounciness = agentConfig.bounciness };
      } else if(jointAnchor.x == 1) {
        cj.highAngularXLimit = new SoftJointLimit() { limit = agentConfig.highLimit, bounciness = agentConfig.bounciness };
        cj.lowAngularXLimit = new SoftJointLimit() { limit = agentConfig.lowLimit, bounciness = agentConfig.bounciness };
        cj.axis = new Vector3(0f, -1f, 0f);
      } else if(jointAnchor.x == -1) {
        cj.lowAngularXLimit = new SoftJointLimit() { limit = -agentConfig.highLimit, bounciness = agentConfig.bounciness };
        cj.highAngularXLimit = new SoftJointLimit() { limit = -agentConfig.highLimit, bounciness = agentConfig.bounciness };
        cj.axis = new Vector3(0f, -1f, 0f);
      } else if(jointAnchor.z == 1) {
        cj.axis = new Vector3(0f, -1f, 0f);
        cj.highAngularXLimit = new SoftJointLimit() { limit = agentConfig.lowLimit, bounciness = agentConfig.bounciness };
        cj.lowAngularXLimit = new SoftJointLimit() { limit = -agentConfig.lowLimit, bounciness = agentConfig.bounciness };
      } else if(jointAnchor.z == -1) {
        cj.axis = new Vector3(-1f, 0f, 0f);
        cj.highAngularXLimit = new SoftJointLimit() { limit = agentConfig.lowLimit, bounciness = agentConfig.bounciness };
        cj.lowAngularXLimit = new SoftJointLimit() { limit = -agentConfig.lowLimit, bounciness = agentConfig.bounciness };
      }
    }

    private void AddAgentPart(bool init)
    {
      aTBehaviour = transform.gameObject.GetComponent<AgentTrainBehaviour>();
      aTBehaviour.initPart = Cells[0].transform;
      for (int i = 1; i < cellNb; i++)
      {
        if(aTBehaviour.parts.Count < i) {
          aTBehaviour.parts.Add(Cells[i].transform);
        }
      }
      if (init)
      {
        aTBehaviour.initBodyParts();   
      }
    }
  }
}