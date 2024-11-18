using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Atavism
{
    public class GroundItem
    {
        public long id;
        public Vector3 loc;
        public int templateId;
        public int stack;
    }

    public class GroundLootManager : MonoBehaviour
    {
        static GroundLootManager instance;
        protected Dictionary<long, GroundItem> items = new Dictionary<long, GroundItem>();
        protected Dictionary<long, GroundItemDisplay> itemsSpawned = new Dictionary<long, GroundItemDisplay>();
        protected Dictionary<int, Dictionary<int, Dictionary<int, long>>> itemsLocation = new Dictionary<int, Dictionary<int, Dictionary<int, long>>>();

        [SerializeField] private GroundItemDisplayUGUI itemUiPrefab;
        [SerializeField] private float distanceToDespawn = 10f;
        [SerializeField] private float distanceMaxSpawn = 10f;
        [SerializeField] private float secondsToCheckSpawn = 0.5f;
        [SerializeField] private float gridSize = 0.5f;
        [SerializeField] float spawnOverTheTerrain = 0.2f;
        [SerializeField] LayerMask groundLayer = (1 << 0) | (1 << 30) | (1 << 26) | (1 << 20);
         RaycastHit groundHit;
         [SerializeField] float groundMaxRayDistance = 5f;

        [SerializeField] private float secondsToCheckLabels = 0.2f;
        [SerializeField] private bool useCoroutineToCheckLabels = true;
        [HideInInspector] GameObject itemCanvas;
        [HideInInspector] Transform centerPointSensor;

        // Start is called before the first frame update
        void Start()
        {

            if (instance != null)
            {
                Destroy(this);
                return;
            }

            instance = this;
            StartCoroutine(checkRange(secondsToCheckSpawn));
            NetworkAPI.RegisterExtensionMessageHandler("LootGroundUpdate", HandleLootGroundUpdate);
            SceneManager.sceneLoaded += OnSceneLoaded;
            if(useCoroutineToCheckLabels)
            StartCoroutine(checkLabels(secondsToCheckLabels));
        }

        private void OnDestroy()
        {
            NetworkAPI.RemoveExtensionMessageHandler("LootGroundUpdate", HandleLootGroundUpdate);
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private IEnumerator checkRange(float f)
        {
            WaitForSeconds delay = new WaitForSeconds(f);
            while (true)
            {
                Vector3 position = Vector3.zero;
                if (ClientAPI.GetPlayerObject() != null && ClientAPI.GetPlayerObject().GameObject != null)
                    position = ClientAPI.GetPlayerObject().GameObject.transform.position;

                bool spawned = false;
                // Debug.LogError("Items count "+items.Count);
                foreach (var item in items.Values)
                {
                    float distance = Vector3.Distance(item.loc, position);
                    //    Debug.LogError("distance "+distance+" distanceToDespawn="+distanceToDespawn+" distanceMaxSpawn="+distanceMaxSpawn);
                    if (distance > distanceToDespawn)
                    {
                        if (itemsSpawned.ContainsKey(item.id))
                        {
                            GroundItemDisplay go = itemsSpawned[item.id];
                            itemsSpawned.Remove(item.id);
                            itemsLocation[go.gridLocY][go.gridLocX].Remove(go.gridLocZ);
                            if (itemsLocation[go.gridLocY][go.gridLocX].Count == 0)
                                itemsLocation[go.gridLocY].Remove(go.gridLocX);
                            if (itemsLocation[go.gridLocY].Count == 0)
                                itemsLocation.Remove(go.gridLocY);
                            Destroy(go.uiElement.gameObject);
                            GameObject.Destroy(go.gameObject);
                        }
                    }
                    else if (distance < distanceMaxSpawn)
                    {
                        
                        if (!itemsSpawned.ContainsKey(item.id) && !spawned)
                        {
                            ItemPrefabData ipd = AtavismPrefabManager.Instance.GetItemTemplateByID(item.templateId);
                            if (ipd.groundPrefab != null && ipd.groundPrefab.Length > 0)
                            {
                                string prefabName = ipd.groundPrefab;
                                if (prefabName.Contains(".prefab"))
                                {
                                    int resourcePathPos = prefabName.IndexOf("Resources/");
                                    prefabName = prefabName.Substring(resourcePathPos + 10);
                                    prefabName = prefabName.Remove(prefabName.Length - 7);
                                }

                                GameObject prefab = (GameObject)Resources.Load(prefabName);
                                GameObject go = null;
                                int x = (int)(item.loc.x / gridSize);
                                int y = (int)(item.loc.y / gridSize);
                                int z = (int)(item.loc.z / gridSize);

                                bool found = false;
                                if (itemsLocation.ContainsKey(y))
                                {
                                    if (itemsLocation[y].ContainsKey(x))
                                    {
                                        if (itemsLocation[y][x].ContainsKey(z))
                                        {
                                            for (var i = 1; i < 10; i++)
                                            {
                                                if (found)
                                                    break;
                                                for (var ix = x - i; ix < x + i; ix++)
                                                {
                                                    if (found)
                                                        break;
                                                    for (var iz = z - i; iz < z + i; iz++)
                                                    {
                                                        if (itemsLocation[y].ContainsKey(ix) && itemsLocation[y][ix].ContainsKey(iz))
                                                        {

                                                        }
                                                        else
                                                        {
                                                            found = true;
                                                            if (!itemsLocation[y].ContainsKey(ix))
                                                                itemsLocation[y].Add(ix, new Dictionary<int, long>());
                                                            itemsLocation[y][ix].Add(iz, item.id);
                                                            x = ix;
                                                            z = iz;
                                                            break;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            itemsLocation[y][x].Add(z, item.id);
                                        }

                                    }
                                    else
                                    {
                                        itemsLocation[y].Add(x, new Dictionary<int, long>());
                                        itemsLocation[y][x].Add(z, item.id);
                                    }
                                }
                                else
                                {
                                    itemsLocation.Add(y, new Dictionary<int, Dictionary<int, long>>());
                                    itemsLocation[y].Add(x, new Dictionary<int, long>());
                                    itemsLocation[y][x].Add(z, item.id);
                                }

                                Vector3 spawnLoc = new(x * gridSize, y * gridSize, z * gridSize);
                                Ray ray1 = new Ray(spawnLoc + new Vector3(0, 2f, 0), Vector3.down);
                                // raycast for check the ground distance
                                if (Physics.Raycast(ray1, out groundHit, groundMaxRayDistance, groundLayer))
                                {
                                    spawnLoc = groundHit.point;
                                }

                                spawnLoc += Vector3.up * spawnOverTheTerrain;
                                if (prefab != null)
                                {
                                    go = (GameObject)Instantiate(prefab, spawnLoc, Quaternion.identity);
                                }
                                else
                                {
                                    Debug.LogError("item prefab is null model: " + prefabName);
                                }

                                if (go != null)
                                {
                                    GroundItemDisplay groundItemDisplay = go.GetComponent<GroundItemDisplay>();
                                    itemsSpawned.Add(item.id, groundItemDisplay);
                                    groundItemDisplay.uiElement = Instantiate(itemUiPrefab, itemCanvas.transform);
                                    groundItemDisplay.uiElement.groundItemDisplay = groundItemDisplay;
                                    groundItemDisplay.Setup(item.id, item.templateId, item.stack, "", item.loc, spawnLoc, x, y, z);

                                }
                            }

                            spawned = true;
                        }
                    }
                }

                //   Debug.LogError("checkRange itemsSpawned count "+itemsSpawned.Count);
                yield return delay;
            }

        }

        private void HandleLootGroundUpdate(Dictionary<string, object> props)
        {
            if (AtavismLogger.isLogDebug())
            {
                AtavismLogger.LogDebugMessage("HandleLootGroundUpdate");
                string keys = " [ ";
                foreach (string it in props.Keys)
                {
                    if (!it.Contains("icon2"))
                        keys += " ; " + it + " => " + props[it] + "\n";
                    if (keys.Length > 10000)
                    {
                        AtavismLogger.LogDebugMessage("HandleLootGroundUpdate: keys:" + keys);
                        keys = "";
                    }
                }

                AtavismLogger.LogDebugMessage("HandleLootGroundUpdate: keys:" + keys);
            }

            int num = (int)props["num"];
            List<long> keyList = new List<long>(this.items.Keys);

            for (int i = 0; i < num; i++)
            {
                OID id = (OID)props["i" + i + "id"];
                keyList.Remove(id.ToLong());
                if (!items.ContainsKey(id.ToLong()))
                {
                    int templateId = (int)props["i" + i + "tid"];
                    int stack = (int)props["i" + i + "s"];
                    Vector3 loc = (Vector3)props["i" + i + "loc"];
                    //  Debug.LogError("HandleLootGroundUpdate id " + id + " templateId = " + templateId + " stack=" + stack + " loc=" + loc);
                    GroundItem gi = new GroundItem();
                    gi.id = id.ToLong();
                    gi.templateId = templateId;
                    gi.stack = stack;
                    gi.loc = loc;
                    items.Add(gi.id, gi);
                }
                else
                {
                    //  Debug.LogError("HandleLootGroundUpdate ContainsKey id " + id);

                }
            }

            foreach (var key in keyList)
            {
                if (itemsSpawned.ContainsKey(key))
                {
                    GroundItemDisplay go = itemsSpawned[key];
                    itemsSpawned.Remove(key);
                    itemsLocation[go.gridLocY][go.gridLocX].Remove(go.gridLocZ);
                    if (itemsLocation[go.gridLocY][go.gridLocX].Count == 0)
                        itemsLocation[go.gridLocY].Remove(go.gridLocX);
                    if (itemsLocation[go.gridLocY].Count == 0)
                        itemsLocation.Remove(go.gridLocY);
                    GameObject.Destroy(go.gameObject);
                }

                items.Remove(key);
            }
            // Debug.LogError("HandleLootGroundUpdate items "+items.Count);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name.Equals("Login"))
            {
                items.Clear();
                itemsSpawned.Clear();
                itemsLocation.Clear();
            }

            if (itemCanvas == null)
            {
                itemCanvas = GameObject.FindGameObjectWithTag("ItemUI");
            }

        }

        // Update is called once per frame
        void Update()
        {
            if(camera==null)
                camera = Camera.main;
            if (centerPointSensor == null && ClientAPI.GetPlayerObject() != null)
            {
                centerPointSensor = ClientAPI.GetPlayerObject().GetControllingTransform();
            }

            if (!useCoroutineToCheckLabels)
            {
                CheckPositionsOfLabels();
            }
        }

        private Camera camera = null;
        IEnumerator checkLabels(float waitTime)
        {
            while (true)
            {
    
                try
                {
                    CheckPositionsOfLabels();
                }
                catch (Exception e)
                {
                    
                }
                //  Debug.LogError("Start !!!!!!!!!!!!!");
                yield return new WaitForSeconds(waitTime);
            }
        }

        void CheckPositionsOfLabels()
        {
            bool skip = false;
            int c = 0;
            int d = 0;
            if (camera != null)
            {
                Dictionary<long, GroundItemDisplay> _itemsSpawned = new Dictionary<long, GroundItemDisplay>(itemsSpawned);
                foreach (var gid1 in _itemsSpawned.Values)
                {
                    skip = false;
                    d = 0;
                    Vector3 screenPos = camera.WorldToScreenPoint(gid1.getPointPosition());
                    if (gid1.uiElement != null)
                    {
                        gid1.uiElement.transform.position = new Vector3(screenPos.x, screenPos.y);
                        //   Debug.LogError(" screenPos " + gid1.Id + " " + c + " " + d +" move  ---> " + gid1.uiElement.rect.position+ " "+ gid1.uiElement.rect.rect.position+" size "+gid1.uiElement.rect.rect.size);

                        if (gid1.uiElement != null)
                        {

                            float dy1 = 0f;
                            float dy2 = 0f;
                            float dx1 = 0f;
                            float dx2 = 0f;
                            int numChange = 0;
                            List<RectTransform> oldCheck = new List<RectTransform>();
                            foreach (var gid in _itemsSpawned.Values)
                            {

                                if (!skip)
                                    if (gid1.Id != gid.Id)
                                    {
                                        if (CheckOverlap(gid1.uiElement.rect, gid.uiElement.rect))
                                        {
                                            //  Debug.LogError("OverLap "+gid1.Id+ " "+gid.Id+" "+c+" "+d);
                                            // float _dy1 = (gid1.uiElement.rect.localPosition.y - gid.uiElement.rect.localPosition.y);
                                            // float _dx1 = (gid1.uiElement.rect.localPosition.x - gid1.uiElement.rect.rect.width / 2 - gid.uiElement.rect.localPosition.x + gid.uiElement.rect.rect.width / 2);
                                            gid1.uiElement.transform.position = new Vector3(gid1.uiElement.transform.position.x, gid.uiElement.transform.position.y + (gid1.uiElement.rect.rect.height + 5)* gid1.uiElement.rect.lossyScale.y);

                                            //    Debug.LogError("Overlaped " + gid1.Id + " " + gid.Id + " " + c + " " + d +" move from" + screenPos + " ---> " + gid1.uiElement.rect.position+ " "+ gid1.uiElement.rect.rect.position+" size "+gid1.uiElement.rect.rect.size);
                                            //    yield return new WaitForEndOfFrame();
                                            int ildid = 0;
                                            bool run = true;

                                            while (run && ildid < 1000) 
                                            {
                                                run = false;
                                                foreach (var v in oldCheck)
                                                {
                                                    if (CheckOverlap(v, gid1.uiElement.rect))
                                                    {
                                                        // float aa_dy1 = (gid1.uiElement.rect.localPosition.y - gid.uiElement.rect.localPosition.y);
                                                        // float aa_dx1 = (gid1.uiElement.rect.localPosition.x - gid1.uiElement.rect.rect.width / 2 - gid.uiElement.rect.localPosition.x + gid.uiElement.rect.rect.width / 2);
                                                        // float a_dy1 = (gid1.uiElement.rect.localPosition.y + gid1.uiElement.rect.rect.height - v.localPosition.y);
                                                        // float a_dy2 = (v.localPosition.y + v.rect.height - gid1.uiElement.rect.localPosition.y);
                                                        // float a_dx1 = (v.localPosition.x + v.rect.width - gid1.uiElement.rect.localPosition.x);
                                                        // float a_dx2 = (gid1.uiElement.rect.localPosition.x + gid1.uiElement.rect.rect.width - v.localPosition.x);
                                                        //  Debug.LogError("OverLap " + gid1.Id + " " + gid.Id + " " + c + " " + d + " old OverLap check " + ildid + " old [" + aa_dy1 + " " + aa_dx1 + "]  [" + a_dy1 + " " + a_dy2 + " " + a_dx1 + " " + a_dx2 + "] v size " + v.rect.size +" " + v.position);
                                                        gid1.uiElement.transform.position = new Vector3(gid1.uiElement.transform.position.x, v.position.y + (gid1.uiElement.rect.rect.height + 5)* gid1.uiElement.rect.lossyScale.y);
                                                     
                                                        run = true;
                                                    }

                                                    ildid++;
                                                }
                                            }

                                            oldCheck.Add(gid.uiElement.rect);
                                            numChange++;
                                        }
                                        else
                                        {
                                            oldCheck.Add(gid.uiElement.rect);
                                        }
                                    }
                                    else
                                    {
                                        skip = true;
                                    }

                                d++;
                            }
                        }
                    }

                    c++;
                }
            }
        }

        bool CheckOverlap(RectTransform image1rect, RectTransform image2rect)
        {
            if (image1rect == null || image2rect == null)
                return false;
            if (image1rect.localPosition.x < image2rect.localPosition.x + image2rect.rect.width &&
                image1rect.localPosition.x + image1rect.rect.width > image2rect.localPosition.x &&
                image1rect.localPosition.y < image2rect.localPosition.y + image2rect.rect.height &&
                image1rect.localPosition.y + image1rect.rect.height > image2rect.localPosition.y)
            {
                return true;
            }

            return false;
        }


        public static GroundLootManager Instance
        {
            get { return instance; }
        }

    }
}