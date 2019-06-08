﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using Newborn;
using SimpleJSON;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Newborn
{
  [ExecuteInEditMode]
  public class NewbornService : MonoBehaviour
  {
    public string responseUuid;
    public GameObject cell;
    public delegate void QueryComplete();
    public static event QueryComplete onQueryComplete;
    public enum Status { Neutral, Loading, Complete, Error };
    public static Dictionary<string, string> variable = new Dictionary<string, string>();
    public static Dictionary<string, string[]> array = new Dictionary<string, string[]>();
    private NewbornAgent newborn;
    private static String graphQlInput;
    static byte[] postData;
    static Dictionary<string, string> postHeader;
    public delegate IEnumerator PostModelCallback(Transform transform, WWW www, GameObject agent, string newbornId);

    public delegate IEnumerator PostNewbornFromReproductionCallback(GameObject agent, GameObject agentPartner, string newbornId);

    public static IEnumerator GetNewborn(string id, GameObject agent, bool IsGetAfterPost)
    {
      NewbornService.variable["id"] = id;
      WWW www;
      ServiceHelpers.graphQlApiRequest(NewbornService.variable, NewbornService.array, out postData, out postHeader, out www, out graphQlInput, ApiConfig.newBornGraphQlQuery, ApiConfig.apiKey, ApiConfig.url);

      yield return www;
      if (www.error != null)
      {
        throw new Exception("There was an error sending request: " + www.error);
      }
      else
      {
        JSONNode responseData = JSON.Parse(www.text)["data"]["getNewborn"];
        if (responseData == null)
        {
          throw new Exception("There was an error sending request: " + responseData);
        }
        Debug.Log("NewBorn successfully requested!");
        List<float> infoResponse = new List<float>();
        string responseId = responseData["id"];
        foreach (var cellInfo in responseData["models"]["items"][0]["cellInfos"].AsArray)
        {
          infoResponse.Add(cellInfo.Value.AsFloat);
        }
        agent.GetComponent<NewBornBuilder>().BuildNewbornFromResponse(agent, responseId, infoResponse);
      }
    }

    public static IEnumerator UpdateInstanceId(string id, string instanceId)
    {
      NewbornService.variable["id"] = "\"" + id + "\"";
      NewbornService.variable["instanceId"] = "\"" + instanceId + "\"";
      WWW www;
      ServiceHelpers.graphQlApiRequest(NewbornService.variable, NewbornService.array, out postData, out postHeader, out www, out graphQlInput, ApiConfig.updateNewbornInstanceId, ApiConfig.apiKey, ApiConfig.url);
      yield return www;
      if (www.error != null)
      {
        throw new Exception("There was an error sending request: " + www.error);
      }
      else
      {
        JSONNode responseData = JSON.Parse(www.text);
        if (responseData["data"]["updateNewborn"] != null)
        {
          Debug.Log("NewBorn instanceId successfully updated!");
        }
        else
        {
          throw new Exception("There was an error sending request: " + responseData);
        }
      }
    }

    public static IEnumerator UpdateDevelopmentStage(string id, string stage)
    {
      NewbornService.variable["id"] = "\"" + id + "\"";
      NewbornService.variable["stage"] = "\"" + stage + "\"";
      WWW www;
      ServiceHelpers.graphQlApiRequest(NewbornService.variable, NewbornService.array, out postData, out postHeader, out www, out graphQlInput, ApiConfig.updateDevelopmentStage, ApiConfig.apiKey, ApiConfig.url);
      yield return www;
      if (www.error != null)
      {
        Debug.Log(www.text);
        throw new Exception("There was an error sending request: " + www.error);
      }
      else
      {
        JSONNode responseData = JSON.Parse(www.text);
        if (responseData["data"]["updateNewborn"] != null)
        {
          Debug.Log("NewBorn instanceId successfully updated!");
        }
        else
        {
          throw new Exception("There was an error sending request: " + responseData);
        }
      }
    }

    public static IEnumerator PostNewbornModel(Transform transform, GenerationPostData generationPostData, string modelId, GameObject agent, PostModelCallback callback)
    {
      string cellPositionsString = BuildCellPositionString(generationPostData);
      NewbornService.variable["id"] = generationPostData.id;
      NewbornService.variable["modelNewbornId"] = modelId;
      NewbornService.variable["cellPositions"] = cellPositionsString;
      NewbornService.variable["cellInfos"] = JSON.Parse(JsonUtility.ToJson(generationPostData))["cellInfos"].ToString();

      WWW www;
      ServiceHelpers.graphQlApiRequest(NewbornService.variable, NewbornService.array, out postData, out postHeader, out www, out graphQlInput, ApiConfig.modelGraphQlMutation, ApiConfig.apiKey, ApiConfig.url);

      yield return www;
      if (www.error != null)
      {
        throw new Exception("There was an error sending request: " + www.error);
      }
      else
      {
        if (JSON.Parse(www.text)["data"]["createModel"] == null)
        {
          throw new Exception("There was an error sending request: " + www.error);
        }
        yield return callback(transform, www, agent, modelId);
      }
    }
    public static IEnumerator RebuildAgent(Transform transform, WWW www, GameObject agent, string newbornId)
    {
      Debug.Log("Newborn Model successfully posted!");
      List<float> infoResponse = new List<float>();
      DestroyAgent(transform);
      JSONNode responseData = JSON.Parse(www.text)["data"]["createModel"];
      string responseId = responseData["id"];
      foreach (var cellInfo in responseData["cellInfos"].AsArray)
      {
        infoResponse.Add(cellInfo.Value.AsFloat);
      }
      agent.GetComponent<NewBornBuilder>().BuildNewbornFromResponse(agent, responseId, infoResponse);
      yield return "";
    }
    public static IEnumerator PostNewborn(NewBornPostData newBornPostData, GameObject agent = null)
    {
      NewbornService.variable["id"] = newBornPostData.id;
      NewbornService.variable["name"] = "\"newborn\"";
      NewbornService.variable["sex"] = "\"demale\"";
      NewbornService.variable["newbornGenerationId"] = newBornPostData.generationId;

      WWW www;
      ServiceHelpers.graphQlApiRequest(variable, array, out postData, out postHeader, out www, out graphQlInput, ApiConfig.newbornGraphQlMutation, ApiConfig.apiKey, ApiConfig.url);
      Debug.Log(graphQlInput);
      yield return www;
      if (www.error != null)
      {
        throw new Exception("There was an error sending request: " + www.error);
      }
      else
      {
        if (JSON.Parse(www.text)["data"]["createNewborn"] == null)
        {
          throw new Exception("There was an error sending request: " + www.error);
        }
        string createdNewBornId = JSON.Parse(www.text)["data"]["createNewborn"]["id"];
        agent.transform.GetComponent<NewbornAgent>().GenerationIndex = JSON.Parse(www.text)["data"]["createNewborn"]["generation"]["index"];
        NewbornService.PostModelCallback callback = NewbornService.RebuildAgent;
        yield return agent.transform.GetComponent<NewBornBuilder>().PostNewbornModel(createdNewBornId, 0, agent, callback); // will it always be first generation
      }
    }

    public static IEnumerator PostNewbornFromReproduction(NewBornPostData newBornPostData, GameObject agent, GameObject agentPartner, PostNewbornFromReproductionCallback callback)
    {
      NewbornService.variable["id"] = newBornPostData.id;
      NewbornService.variable["name"] = "\"newborn\"";
      NewbornService.variable["sex"] = "\"female\"";
      NewbornService.variable["newbornGenerationId"] = newBornPostData.generationId;
      NewbornService.variable["parentA"] = "\"" + agent.GetComponent<AgentTrainBehaviour>().brain.name + "\"";
      NewbornService.variable["parentB"] = "\"" + agentPartner.GetComponent<AgentTrainBehaviour>().brain.name + "\"";
      WWW www;
      ServiceHelpers.graphQlApiRequest(variable, array, out postData, out postHeader, out www, out graphQlInput, ApiConfig.postNewbornFromReproductionGraphQlMutation, ApiConfig.apiKey, ApiConfig.url);
      yield return www;
      if (www.error != null)
      {
        throw new Exception("There was an error sending request: " + www.error);
      }
      else
      {
        if (JSON.Parse(www.text)["data"]["createNewborn"] == null)
        {
          throw new Exception("There was an error sending request: " + www.error);
        }
        string createdNewBornId = JSON.Parse(www.text)["data"]["createNewborn"]["id"];
        yield return callback(agent, agentPartner, createdNewBornId);
      }
    }

    public static IEnumerator UpdateNewbornChilds(string newbornId, List<string> childs)
    {
      NewbornService.variable["id"] = "\"" + newbornId + "\"";
      NewbornService.variable["childs"] = ServiceHelpers.ReturnNewbornChilds(childs);
      WWW www;
      ServiceHelpers.graphQlApiRequest(NewbornService.variable, NewbornService.array, out postData, out postHeader, out www, out graphQlInput, ApiConfig.updateNewbornChilds, ApiConfig.apiKey, ApiConfig.url);
      yield return www;
      if (www.error != null)
      {
        throw new Exception("There was an error sending request: " + www.error);
      }
      else
      {
        JSONNode responseData = JSON.Parse(www.text);
        if (responseData["data"]["updateNewborn"] == null)
        {
          throw new Exception("There was an error sending request: " + responseData);
        }
        Debug.Log("NewBorn childs successfully updated!");
      }
    }

    private static string ReturnNewbornChilds(List<string> childs)
    {
      string childString = "";
      foreach (var child in childs)
      {
        childString = childString + "\"" + child + "\"" + ",";
      }

      return childString;
    }

    public static void DestroyAgent(Transform transform)
    {
      Transform[] childs = transform.Cast<Transform>().ToArray();
      transform.gameObject.SetActive(true);
      transform.GetComponent<NewBornBuilder>().ClearNewborns();
      foreach (Transform child in childs)
      {
        DestroyImmediate(child.gameObject);
      }
    }

    private static string BuildCellPositionString(GenerationPostData generationPostData)
    {
      string cellPositionsString = "[";
      for (int i = 0; i < generationPostData.cellPositions.Count; i++)
      {
        cellPositionsString = cellPositionsString + JSON.Parse(JsonUtility.ToJson(generationPostData.cellPositions[i]))["position"].ToString() + ",";
      }
      cellPositionsString = cellPositionsString + "]";
      return cellPositionsString;
    }

    public static IEnumerator SuccessfullModelCallback(Transform transform, WWW www, GameObject agent, string newbornId = null)
    {
      Debug.Log("Successfull Newborn reproduction");
      yield return TrainingService.TrainNewborn(newbornId);
    }

    public static IEnumerator SuccessfullPostNewbornFromReproductionCallback(GameObject agent, GameObject agentPartner, string newbornId)
    {
      agent.transform.GetComponent<NewbornAgent>().childs.Add(newbornId);
      agentPartner.transform.GetComponent<NewbornAgent>().childs.Add(newbornId);
      yield return NewbornService.UpdateNewbornChilds(agent.transform.GetComponent<AgentTrainBehaviour>().brain.name, agent.transform.GetComponent<NewbornAgent>().childs);
      yield return NewbornService.UpdateNewbornChilds(agentPartner.transform.GetComponent<AgentTrainBehaviour>().brain.name, agentPartner.transform.GetComponent<NewbornAgent>().childs);
      NewbornService.PostModelCallback PostModelCallback = NewbornService.SuccessfullModelCallback;
      yield return agent.GetComponent<NewBornBuilder>().PostNewbornModel(newbornId, 0, agent, PostModelCallback); // will it always be first generation
    }
  }
}