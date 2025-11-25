using System;
using System.Collections;
using System.Collections.Generic;
using Artistar.Puzzle.Core;
using Unity.VisualScripting;
using UnityEngine;

public class IngameBlockPoolController : MonoBehaviour
{
    private static IngameBlockPoolController _blockPoolController;
    public static IngameBlockPoolController Instance => _blockPoolController;

    [Header("블록 프리팹")]
    [SerializeField] private GameObject blockPrefab;

    [Header("특수블록 프리팹 - Create")]                               //블록 프리팹 하위에 생성된다.
    [SerializeField] private GameObject prefab_create_301;          //비행기 [운석]
    [SerializeField] private GameObject prefab_create_302;          //로켓
    [SerializeField] private GameObject prefab_create_303;          //폭탄 [토성]
    [SerializeField] private GameObject prefab_create_304;          //미러볼 [블랙홀]
    [SerializeField] private GameObject prefab_create_305;          //스타레이

    [Header("일반블록 프리팹 - Explode")] 
    [SerializeField] private GameObject prefab_explode_101;
    [SerializeField] private GameObject prefab_explode_102;
    [SerializeField] private GameObject prefab_explode_103;
    [SerializeField] private GameObject prefab_explode_104;
    [SerializeField] private GameObject prefab_explode_105;
    
    [Header("특수블록 프리팹 - Explode")]
    [SerializeField] private GameObject prefab_move_301;            //비행기 [운석] 날라갈 때
    [SerializeField] private GameObject prefab_alarm_301;           //비행기 [운석] 4방향 알람
    [SerializeField] private GameObject prefab_explode_302;         //로켓 4방향 날라갈 때
    [SerializeField] private GameObject prefab_explode_303;         //폭탄 [토성] 터질 때
    [SerializeField] private GameObject prefab_explode_304;         //미러볼 [블랙홀] 터질 때 - 회전
    [SerializeField] private GameObject prefab_explode_305;         //스타레이 터질 때

    private static Dictionary<string, List<GameObject>> specialBlockPool;
    private static List<GameObject> normalBlockPool;
    
    private void Awake()
    {
        _blockPoolController = this;
    }

    private void OnDisable()
    {
        specialBlockPool?.Clear();
        normalBlockPool?.Clear();
    }

    public static void InitPool(int poolSize, int specialBlockPoolSize)
    {
        InitNormalBlockPool(poolSize);
        InitSpecialCreateBlockPool(specialBlockPoolSize);
        InitSpecialPangBlockPool(specialBlockPoolSize);
    }

    public static GameObject SpawnBlock()
    {
        if (normalBlockPool == null)
            normalBlockPool = new List<GameObject>();
        
        var targetItem = normalBlockPool.Find(x => !x.activeSelf);
        if (targetItem != null)
        {
            targetItem.SetActive(true);
            return targetItem;
        }

        GameObject targetPrefab = Instance.blockPrefab;
        GameObject newObj = Instantiate(targetPrefab, Instance.transform);
        normalBlockPool.Add(newObj);
        
        return newObj;
    }

    //Pool에서 원하는 생성 블록 GameObject를 전달 받는다.
    public static GameObject SpawnCreateSpecialBlock(string keyName)
    {
        if (specialBlockPool == null)
            specialBlockPool = new Dictionary<string, List<GameObject>>();
        
        if (!specialBlockPool.ContainsKey(keyName))
        {
            specialBlockPool.Add(keyName, new List<GameObject>());
        }

        var targetItem = specialBlockPool[keyName].Find(x => !x.activeSelf);
        
        //pool에서 사용되고 있지 않은 object 하나 전달해 준다.
        if (targetItem != null)
        {
            targetItem.SetActive(true);
            return targetItem;
        }
        //pool을 다 사용하고 있는 경우
        else
        {
            GameObject targetPrefab = null;
            switch (keyName)
            {
                case "301":
                    targetPrefab = Instance.prefab_create_301;
                    break;
                case "302":
                    targetPrefab = Instance.prefab_create_302;
                    break;
                case "303":
                    targetPrefab = Instance.prefab_create_303;
                    break;
                case "304":
                    targetPrefab = Instance.prefab_create_304;
                    break;
                case "305":
                    targetPrefab = Instance.prefab_create_305;
                    break;
            }

            Transform newObjParent = Instance.transform.parent.Find(keyName);
            GameObject newObj = Instantiate(targetPrefab, newObjParent);
            specialBlockPool[keyName].Add(newObj);

            return newObj;
        }
    }

    public static void CheckFirstBlock(Block firstBlock, Block secondBlock = null)
    {
        if (firstBlock != null)
        {
            firstBlock.isFirstBlock = false;
        }
        if (secondBlock != null)
        {
            secondBlock.isFirstBlock = false;
        }
    }
    
    public static void ReleaseNormalBlock(GameObject blockObj)
    {
        foreach (Transform child in blockObj.transform)
        {
            if (child.name.Equals("Block"))
            {
                foreach (Transform blockChild in child)
                {
                    Destroy(blockChild.gameObject);
                }
            }
            else
            {
                Destroy(child.gameObject);
            }
        }

        blockObj.name = "blockPrefab(Clone)";
        blockObj.transform.SetParent(Instance.transform.parent.Find("NormalBlock_Pool"));
        blockObj.SetActive(false);
    }

    public static void ReleaseExplodeSpecialBlock(string keyName, GameObject obj)
    {
        SBDebug.Log("SJW ReleaseExplodeSpecialBlock " + keyName);
        
        Transform newObjParent = Instance.transform.parent
            .Find("SpecialBlock_Pang_Pool")
            .Find(keyName);
        
        obj.transform.SetParent(newObjParent);
        obj.SetActive(false);
    }

    private static void InitNormalBlockPool(int poolSize)
    {
        GameObject rootObj = new GameObject("NormalBlock_Pool");
        rootObj.transform.SetParent(Instance.transform.parent);
        rootObj.transform.localScale = Vector3.one;
        
        normalBlockPool = new List<GameObject>();

        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(Instance.blockPrefab, rootObj.transform);
            obj.SetActive(false);
            
            normalBlockPool.Add(obj);   
        }
    }

    //특수블록 생성 Effect Pool 초기화
    private static void InitSpecialCreateBlockPool(int poolSize)
    {
        specialBlockPool = new Dictionary<string, List<GameObject>>();
        
        GameObject rootObj = new GameObject("SpecialBlock_Create_Pool");
        rootObj.transform.SetParent(Instance.transform.parent);
        
        //301 블록 pool 세팅
        {
            GameObject parentObj = new GameObject("301");
            parentObj.transform.SetParent(rootObj.transform);

            specialBlockPool.Add(parentObj.name, new List<GameObject>());
            
            for (int i = 0; i < poolSize; i++)
            {
                GameObject obj = Instantiate(Instance.prefab_create_301, parentObj.transform);
                specialBlockPool[parentObj.name].Add(obj);
                obj.SetActive(false);
            }
        }

        //302 블록 pool 세팅
        {
            GameObject parentObj = new GameObject("302");
            parentObj.transform.SetParent(rootObj.transform);
            
            specialBlockPool.Add(parentObj.name, new List<GameObject>());
            
            for (int i = 0; i < poolSize; i++)
            {
                GameObject obj = Instantiate(Instance.prefab_create_302, parentObj.transform);
                specialBlockPool[parentObj.name].Add(obj);
                obj.SetActive(false);
            }
        }

        //303 블록 pool 세팅
        {
            GameObject parentObj = new GameObject("303");
            parentObj.transform.SetParent(rootObj.transform);
            
            specialBlockPool.Add(parentObj.name, new List<GameObject>());
            
            for (int i = 0; i < poolSize; i++)
            {
                GameObject obj = Instantiate(Instance.prefab_create_303, parentObj.transform);
                specialBlockPool[parentObj.name].Add(obj);
                obj.SetActive(false);
            }
        }

        //304 블록 pool 세팅
        {
            GameObject parentObj = new GameObject("304");
            parentObj.transform.SetParent(rootObj.transform);
            
            specialBlockPool.Add(parentObj.name, new List<GameObject>());
            
            for (int i = 0; i < poolSize; i++)
            {
                GameObject obj = Instantiate(Instance.prefab_create_304, parentObj.transform);
                specialBlockPool[parentObj.name].Add(obj);
                obj.SetActive(false);
            }
        }
        
        //305 블록 pool 세팅
        {
            GameObject parentObj = new GameObject("305");
            parentObj.transform.SetParent(rootObj.transform);
            
            specialBlockPool.Add(parentObj.name, new List<GameObject>());
            
            for (int i = 0; i < poolSize; i++)
            {
                GameObject obj = Instantiate(Instance.prefab_create_305, parentObj.transform);
                specialBlockPool[parentObj.name].Add(obj);
                obj.SetActive(false);
            }
        }
    }

    //특수블록 Explode Effect Pool 초기화
    private static void InitSpecialPangBlockPool(int poolSize)
    {
        GameObject rootObj = new GameObject("SpecialBlock_Pang_Pool");
        rootObj.transform.SetParent(Instance.transform.parent);
        
        //301 날라가는 프리팹 pool 세팅
        {
            GameObject parentObj = new GameObject("301_move");
            parentObj.transform.SetParent(rootObj.transform);
            
            specialBlockPool.Add(parentObj.name, new List<GameObject>());
            
            for (int i = 0; i < poolSize; i++)
            {
                GameObject obj = Instantiate(Instance.prefab_move_301, parentObj.transform);
                specialBlockPool[parentObj.name].Add(obj);
                obj.SetActive(false);
            }
        }

        //301 4방향 알람 프리팹 pool 세팅
        {
            GameObject parentObj = new GameObject("301_alarm");
            parentObj.transform.SetParent(rootObj.transform);
            
            specialBlockPool.Add(parentObj.name, new List<GameObject>());
            
            for (int i = 0; i < poolSize; i++)
            {
                GameObject obj = Instantiate(Instance.prefab_alarm_301, parentObj.transform);
                specialBlockPool[parentObj.name].Add(obj);
                obj.SetActive(false);
            }
        }

        //302 블록 pool 세팅
        {
            GameObject parentObj = new GameObject("302_pang");
            parentObj.transform.SetParent(rootObj.transform);
            
            specialBlockPool.Add(parentObj.name, new List<GameObject>());
            
            for (int i = 0; i < poolSize; i++)
            {
                GameObject obj = Instantiate(Instance.prefab_explode_302, parentObj.transform);
                specialBlockPool[parentObj.name].Add(obj);
                obj.SetActive(false);
            }
        }
        
        //303 블록 pool 세팅
        {
            GameObject parentObj = new GameObject("303_pang");
            parentObj.transform.SetParent(rootObj.transform);
            
            specialBlockPool.Add(parentObj.name, new List<GameObject>());
            
            for (int i = 0; i < poolSize; i++)
            {
                GameObject obj = Instantiate(Instance.prefab_explode_303, parentObj.transform);
                specialBlockPool[parentObj.name].Add(obj);
                obj.SetActive(false);
            }
        }
        
        
        //304 블록 pool 세팅
        {
            GameObject parentObj = new GameObject("304_pang");
            parentObj.transform.SetParent(rootObj.transform);
            
            specialBlockPool.Add(parentObj.name, new List<GameObject>());
            
            for (int i = 0; i < poolSize; i++)
            {
                GameObject obj = Instantiate(Instance.prefab_explode_304, parentObj.transform);
                specialBlockPool[parentObj.name].Add(obj);
                obj.SetActive(false);
            }
        }
        
        //305 블록 pool 세팅
        {
            GameObject parentObj = new GameObject("305_pang");
            parentObj.transform.SetParent(rootObj.transform);
            
            specialBlockPool.Add(parentObj.name, new List<GameObject>());
            
            for (int i = 0; i < poolSize; i++)
            {
                GameObject obj = Instantiate(Instance.prefab_explode_305, parentObj.transform);
                specialBlockPool[parentObj.name].Add(obj);
                obj.SetActive(false);
            }
        }
    }
}
