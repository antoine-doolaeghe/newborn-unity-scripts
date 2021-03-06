﻿using UnityEngine;


namespace Components.Newborn
{
  [CreateAssetMenu(fileName = "Agent config", menuName = "agentConfig")]
  public class AgentConfig : ScriptableObject
  {
    public int LayerNumber;
    public Vector3 LimbSpacing;
  }
};