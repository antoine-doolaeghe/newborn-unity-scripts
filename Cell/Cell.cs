﻿using System.Collections;
using System.Collections.Generic;
using MLAgents;
using UnityEngine;

namespace Gene
{
  public class Cell : MonoBehaviour
  {
    [Header("Connection to API Service")]
    public PostGene postGene;
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
    private AgentTrainBehaviour aTBehaviour;
    private List<Vector3> sides = new List<Vector3> {
                new Vector3 (1f, 0f, 0f),
                new Vector3 (0f, 1f, 0f),
                new Vector3 (0f, 0f, 1f),
                new Vector3 (-1f, 0f, 0f),
                new Vector3 (0f, -1f, 0f),
                new Vector3 (0f, 0f, -1f)
            };

    [HideInInspector] public bool isRequestDone;
    [HideInInspector] public float threshold;
    [HideInInspector] public int partNb;
    [HideInInspector] public List<List<float>> GenerationInfos = new List<List<float>>();


    void Awake()
    {
      initialised = false;
    }

    public void DeleteCells()
    {
      Cells.Clear();
      Germs.Clear();
      CellPositions.Clear();
      cellInfoIndex = 0;
      initialised = false;
      GenerationInfos.Clear();
      cellNb = 0;
    }

    public void parseRequestData()
    {
      List<List<Info>> response = postGene.response;
      if (response.Count != 0 && !initialised)
      {
        Debug.Log(response[0][0].val);
        Debug.Log(response[0][1].val);
        Debug.Log(response[0][2].val);
        for (int generationInfo = 0; generationInfo < response.Count; generationInfo++)
        {
          GenerationInfos.Add(new List<float>());
          for (int i = 0; i < response[generationInfo].Count; i++)
          {
            string val = response[generationInfo][i].val;
            GenerationInfos[generationInfo].Add(float.Parse(val));
          }
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
      if(GenerationInfos.Count == 0) {
        GenerationInfos.Add(new List<float>());
      }
      Germs.Add(new List<GameObject>());
      GameObject initCell = InitBaseShape(Germs[0], 0);
      initCell.transform.parent = transform;
      InitRigidBody(initCell);
      HandleStoreCell(initCell, initCell.transform.position);
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
                  HandleStoreCell(cell, cellPosition);
                  cell.transform.parent = transform;
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
    }

    public void AddGeneration()
    {
      int indexInfo = 0;
      int prevCount = 0;
      int germNb = 0;
      partNb += 1;
      

      for (int i = 0; i < Germs.Count; i++)
      {
          if(Germs[i].Count > 0)
          {
            prevCount = Germs[i].Count;
            germNb = i;
          }
      }

      GenerationInfos.Add(new List<float>());
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
              GameObject cell = InitBaseShape(Germs[germNb], germNb);
              InitPosition(sides, germNb + 1, i, z, cell);
              InitRigidBody(cell);
              initJoint(cell, Germs[germNb][i], sides[z], germNb + 1, z);
              HandleStoreCell(cell, cellPosition);
              cell.transform.parent = transform;
            }
          }
        }
      }
    }

    private void checkMinCellNb()
    {
      if (cellNb < minCellNb)
      {
        Debug.Log("Killin Object (less that requiered size");
        transform.gameObject.SetActive(false);
      }
    }

    public List<CellInfo> HandlePostData()
    {
      List<CellInfo> generationInfos = new List<CellInfo>();
      for (int i = 0; i < GenerationInfos.Count; i++)
      {
        List<Info> postData = new List<Info>();
        for (int y = 0; y < GenerationInfos[i].Count; y++)
        {
          postData.Add(new Info(GenerationInfos[i][y].ToString()));
        }
        generationInfos.Add(new CellInfo(postData));
      }

      Debug.Log(generationInfos[0].infos[0].val);
      Debug.Log(generationInfos[0].infos[1].val);
      Debug.Log(generationInfos[0].infos[2].val);

      return generationInfos;
    }

    public void PostCell()
    {
      List<CellInfo> postData = HandlePostData();
      StartCoroutine(postGene.postCell(postData, transform.gameObject.name));
    }

    private void HandleStoreCell(GameObject cell, Vector3 cellPosition)
    {
      Cells.Add(cell);
      CellPositions.Add(cellPosition);
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
      germs.Add(GameObject.CreatePrimitive(PrimitiveType.Sphere));
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
      cj.anchor = new Vector3(0f, 0f, 0f);
      cj.connectedBody = connectedBody.gameObject.GetComponent<Rigidbody>();
      cj.rotationDriveMode = RotationDriveMode.Slerp;
      cj.angularYLimit = new SoftJointLimit() { limit = agentConfig.yLimit, bounciness = agentConfig.bounciness };
      cj.highAngularXLimit = new SoftJointLimit() { limit = agentConfig.highXLimit, bounciness = agentConfig.bounciness };
      cj.lowAngularXLimit = new SoftJointLimit() { limit = agentConfig.lowXLimit, bounciness = agentConfig.bounciness };
      cj.angularZLimit = new SoftJointLimit() { limit = agentConfig.zLimit, bounciness = agentConfig.bounciness };
      part.gameObject.GetComponent<Rigidbody>().useGravity = true;
      part.gameObject.GetComponent<Rigidbody>().mass = 1f;
    }

    private void AddAgentPart()
    {
      aTBehaviour = transform.gameObject.GetComponent<AgentTrainBehaviour>();
      aTBehaviour.initPart = Cells[0].transform;
      for (int i = 1; i < Cells.Count; i++)
      {
        aTBehaviour.parts.Add(Cells[i].transform);
      }
      aTBehaviour.initBodyParts();
    }
  }
}