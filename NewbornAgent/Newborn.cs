﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Newborn : MonoBehaviour
{
  private string bio;
  private string bornPlace;
  private string hexColor;
  private string id;
  public string title;
  public string Sex;
  public bool isGestating;
  public int GenerationIndex;
  public GameObject CellPrefab;
  public List<List<GameObject>> NewBornGenerations;
  public List<GameObject> Cells;
  public List<Vector3> CellPositions;
  public List<Vector3> CelllocalPositions;
  public List<GeneInformation> GeneInformations;
}