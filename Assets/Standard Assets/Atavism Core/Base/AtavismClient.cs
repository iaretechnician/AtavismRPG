using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System;
using System.Net;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Timers;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.ComponentModel;
using Debug = UnityEngine.Debug;

namespace Atavism
{

    public class LoopbackWorldResponse
    {
        public WorldServerEntry worldServerEntry;
        public byte[] idToken;
    }

    public enum GameState
    {
        Login,
        Character,
        World
    }

    public enum WorldConnectionState
    {
        Disconnected,
        Disconnecting,
        ConnectingToLogin,
        ConnectedToLogin,
        ConnectingToProxy,
        ConnectedToProxy,
        Failed
    }

    public delegate void LoadLevelAsync(string levelName);

    public class AtavismClient : MonoBehaviour
    {

        public static AtavismClient instance;

        #region Fields
        static readonly string clientVersion = "10.9.0";

        public bool loginSuccess = false;
        
        public GameObject defaultObject;
        public string mobTag;
        public LayerMask playerLayer;
        public string playerTag;
        public GameObject scriptObject;
        public AtavismResourceManager resourceManager;
        string loginScene;
        string characterScene;
        bool ignoreProps = true;
        bool resyncMobs = true; // If the pathInterpolator shows the mobs position is out of sync, should they be teleported to their correct position

        AtavismWorldManager worldManager;
        GameState gameState;
        volatile WorldConnectionState worldConnectionState = WorldConnectionState.Disconnected;
        volatile CancellationTokenSource worldConnectionCTS;
        bool disconnect = false;
        int updateCount = 0;
        LoadLevelAsync loadLevelAsync;

        /// <summary>
        ///   If this is set, we are debugging, and don't use a server
        /// </summary>
        public bool standalone;

        /// <summary>
        ///    Set to true if the last resize left the form maximized.
        ///    This is the only way we can tell that the restore
        ///    button was clicked.
        /// </summary>
        protected bool lastMaximized = false;
        public bool patchMedia = true;
        public bool exitAfterMediaPatch = false;

        /// <summary>
        ///	  If this is set, and we're talking to a real server, we get the assets
        ///   from the directory cached in the registry.
        /// </summary>
        public bool useRepository = false;

        /// <summary>
        ///	  If this is set, get the repository paths from here instead of from
        ///   the directory or directories cached in the registry.
        /// </summary>
        public List<string> repositoryPaths = new List<string>();

        /// <summary>
        /// If this is a counter of frames, only used if
        /// framesBetweenSleeps is non-zero
        /// </summary>
        protected int frameSleepCount = 0;
        protected bool useCooperativeInput = true;
        protected float time = 0.0f;

        bool loginFailed;
        string loginMessage;
        string worldServerVersion;
        string worldPatcherUrl;
        string worldUpdateUrl;
        static bool serverRelease1_5 = false;

        // Used to set an error message if we are going to exit
        string errorMessage = null;

        /// <summary>
        ///   WorldServerEntry that we will use if we are not connecting to a master server
        /// </summary>
        WorldServerEntry loopbackWorldServerEntry;
        /// <summary>
        ///   Id token to submit
        /// </summary>
        byte[] loopbackIdToken;

        /// <summary>
        ///   Is the client fully initialized?  Set to false
        ///   initially, and to true by the first LoadingStateMessage
        ///   with loadingState = true
        /// </summary>
        protected bool fullyInitialized = false;

        AtavismNetworkHelper networkHelper;

        LoginSettings loginSettings = new LoginSettings();
        static AtavismLoginHelper loginHelper;
        int loginAttempts;
        int loginStep;

        int currentWorld = -1;

        float lastHeartbeatSent = 0f;
        float timeBetweenHeartbeats = 20f;

        #region Constants

        public const int OneMeter = 1;
        public const int HorizonDistance = 1000 * OneMeter;
        protected int lastFrameRenderCalls = 0;
        protected long lastTotalRenderCalls = 0;
        protected float maxRecentFrameTime = 0.0f;
        const long MaxMSForMessagesPerFrame = 100;

        public string DefaultMasterServer = "";
        //const string DefaultMasterServer = "localhost";
        public string DefaultWorldId = "smoo_dev"; //null;
                                                   //public bool webPlayer = false;
                                                   // This list of capabilities supported by the client are passed 
                                                   // to the server in the LoginMessage as part of the version string
        const string ClientCapabilities = "DirLocOrient";

        #endregion

       // public bool getgonode = false;
       // public List<GameObject> gonode = new List<GameObject>();
        public IGameWorld gameWorld;

        private static string overrideVersion = null;

        // If the player is stuck in a collision volume for this many
        // milliseconds, then PrimeMover sends the server the /goto
        // stuck command
        public int MillisecondsStuckBeforeGotoStuck = 5000;

        // When we login to the master server or proxy, if this is
        // true, we'll use TCP rather than RDP.
        protected bool useTCP = false;
     //   protected Renderer guiRenderer = null;

        /// <summary>
        ///    If true, CastRay will determine pick based on the
        ///    distance from the ray origin to the closest
        ///    CollisionShape in the object, rather than using the
        ///    bounding box.  The result of CastRay is what determines
        ///    the InputHandler's mouseoverObject.  
        /// </summary>
        protected bool targetBasedOnCollisionVolumes = false;

        public bool streamer = false;


        public int mobLoadFrameSkip = 10;
        public float despawnDelay = 2f;
        public float smoothRotation = 10f;

        #endregion Fields


        // Use this for initialization
        public void Initalise(GameObject scriptObject, GameObject defaultObject, string characterScene,
         string worldName, bool webPlayer, LogLevel logLevel, LayerMask playerLayer, string playerTag, float maxTimeWithoutMessage,string prefabServer,int prefabServerPort)
        {
            this.scriptObject = scriptObject;
            this.defaultObject = defaultObject;
            this.characterScene = characterScene;
            this.WorldId = worldName;
            //    this.webPlayer = webPlayer;
            AtavismLogger.logLevel = logLevel;
            this.playerLayer = playerLayer;
            this.playerTag = playerTag;
            MessageDispatcher.Instance.MaxTimeWithoutMessage = maxTimeWithoutMessage;
            //this.mobTag = mobTag;
            // No need to set up everything if it is already set up
            if (instance != null)
            {
                GameObject.DestroyImmediate(gameObject);
                return;
            }
            UnityEngine.Debug.Log("Atavism Client <color=green>" + clientVersion + "</color>");
            // Don't let this object be destroyed as it will mess things up when changing instances
            DontDestroyOnLoad(this.gameObject);
            instance = this;
            gameState = GameState.Login;

            // Store the name of the current scene so when we log out or disconnect we can get back in
            loginScene = SceneManager.GetActiveScene().name;

            // Don't let the game pause when in the background - it needs to keep getting and sending data
            Application.runInBackground = true;
             
            // Set up our game specific logic
            // gameWorld = new BetaWorld(this);
            gameWorld = new AtavismGameWorld(this);

            worldManager = new AtavismWorldManager();
            gameWorld.WorldManager = worldManager;

            // Register our handlers.  We must do this before we call
            // MessageDispatcher.Instance.HandleMessageQueue, so that we will
            // get the callbacks for the incoming messages.
            SetupMessageHandlers();

            // Set up our game specific stuff (right now, just betaworld)
            gameWorld.Initialize();
            gameWorld.SetupMessageHandlers();

            SceneManager.sceneLoaded += OnSceneLoaded;
            if (AtavismNetworkHelper.Instance == null)
            {
                networkHelper = new AtavismNetworkHelper(worldManager);
            }
            else
            {
                networkHelper = AtavismNetworkHelper.Instance;
            }
            networkHelper.StartClientPrefab(prefabServer, prefabServerPort);

        }

        // Update is called once per frame
        void Update()
        {

            AtavismEventSystem.Update();
            HandleWorldConnectionState();
            if (disconnect)
            {
                disconnect = false;
                DoDisconnect();
            }
            MessageDispatcher.Instance.HandleMessageQueue(MaxMSForMessagesPerFrame);
            if (gameState == GameState.Character && worldConnectionState == WorldConnectionState.ConnectedToLogin)
            {
                // Do we need to send a heartbeat?
                //Debug.LogWarning("gameState Character lastHeartbeatSent="+lastHeartbeatSent+" timeBetweenHeartbeats="+timeBetweenHeartbeats+" Time.realtimeSinceStartup="+Time.realtimeSinceStartup);
                if ((lastHeartbeatSent + timeBetweenHeartbeats) < Time.realtimeSinceStartup)
                {
                    if (networkHelper.LoginServerHeartbeat(clientVersion) != NetworkHelperStatus.Success)
                    {
                        gameState = GameState.Login;
                        worldManager.Disconnected();
                        return;
                    }
                    lastHeartbeatSent = Time.realtimeSinceStartup;
                }
            }
            else if (gameState == GameState.World)
            {
                worldManager.Update();
            }
            if (networkHelper != null)
            {
                networkHelper.handlePrefabMessages();
            }
        }

        void HandleWorldConnectionState()
        {

            updateCount = (updateCount + 1) % 100;
            if (worldConnectionState == WorldConnectionState.ConnectingToLogin)
            {
                // TODO: Some UI for showing the progress
                if (updateCount == 0)
                {
                    AtavismLogger.LogInfoMessage("Connecting to login... (report progress here...)");
                }
            }
            else if (worldConnectionState == WorldConnectionState.ConnectingToProxy)
            {
                // TODO: Some UI for showing the progress
                if (updateCount == 0)
                {
                    AtavismLogger.LogInfoMessage("Connecting to proxy... (report progress here...)");
                }
            }
            if (worldConnectionState == WorldConnectionState.Failed)
            {
                // TODO: Some proper UI for error message?
                AtavismLogger.LogInfoMessage("Could not connect to world server");
                // and reset the state
                worldConnectionState = WorldConnectionState.Disconnected;
            }
            if (worldConnectionState == WorldConnectionState.ConnectingToLogin
                || worldConnectionState == WorldConnectionState.ConnectingToProxy)
            {
                // Wait for some standard messages like login response and player data
                if (CheckStartupMessagesReceived())
                {
                    bool loadCharacterScene = false;
                    if (worldConnectionState == WorldConnectionState.ConnectingToLogin)
                    {
                        worldConnectionState = WorldConnectionState.ConnectedToLogin;
                        loadCharacterScene = gameState != GameState.Character;
                        gameState = GameState.Character;
                    }
                    else
                    {
                        worldConnectionState = WorldConnectionState.ConnectedToProxy;
                        gameState = GameState.World;
                    }
                    string[] args = new string[1];
                    args[0] = "Success";
                    AtavismEventSystem.DispatchEvent("LOGIN_RESPONSE", args);

                    if (loadCharacterScene)
                    {
                        LoadCharacterScene();
                    }
                }
            }
            if (worldConnectionState == WorldConnectionState.Disconnecting)
            {
                worldConnectionState = WorldConnectionState.Disconnected;
                gameState = GameState.Login;
                worldManager.Disconnected();
            }
        }

        private bool CheckStartupMessagesReceived()
        {
            return worldManager.PlayerStubInitialized && loginMessage != null;
        }

        void LateUpdate()
        {
            if (gameState == GameState.World)
            {
                worldManager.LateUpdate();
            }
        }



        void LevelWasLoaded(Scene newScene, LoadSceneMode mode)
        {
            AtavismLogger.LogInfoMessage("Level was loaded");
            if (networkHelper != null)
            {
                networkHelper.SendSceneLoadedMessage();
            }

        }


        private string _loadLevelName = "";

        public string LoadLevelName
        {
            get { return _loadLevelName; }
        }

        private List<AtavismStreamer> streamers = new List<AtavismStreamer>();
        void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            AtavismLogger.LogInfoMessage("Level was loaded");
            string[] args = new string[1];
            args[0] = scene.name;
            if (scene.name.ToLower().Equals(_loadLevelName.ToLower()))
            {
                if (streamer)
                {
                    streamers.Clear();
                    GameObject[] gos = GameObject.FindGameObjectsWithTag("SceneStreamer");
                    foreach (GameObject go in gos)
                    {
                        AtavismStreamer g = go.GetComponent<AtavismStreamer>();
                        if (g != null)
                            streamers.Add(g);
                    }
                    AtavismLogger.LogDebugMessage("AtavismClient OnSceneLoaded streamers # ->" + streamers.Count);
                    StartCoroutine(checkStreamer());
                }
                else
                {
                    worldManager.sceneLoaded = true;
                    worldManager.sceneInLoading = false;
                    AtavismEventSystem.DispatchEvent("LOADING_SCENE_END", args);
                    if (networkHelper != null)
                        networkHelper.SendSceneLoadedMessage();
                }
            }
        }

        public void StartDespawnDelay(long oid)
        {
                StartCoroutine(DelayObjDespawn(oid));
        }

        IEnumerator DelayObjDespawn(long oid)
        {
            WaitForSeconds delay = new WaitForSeconds(AtavismClient.Instance.despawnDelay);
            yield return delay;
            worldManager.RemoveObjectNode(oid);
            
        }

        public void StartDelay()
        {
            if (!delayStart && worldManager.sceneInLoading)
                StartCoroutine(DelayPlayerMove());
        }

        bool delayStart = false;
        IEnumerator DelayPlayerMove()
        {
            delayStart = true;
            WaitForSeconds delay = new WaitForSeconds(0.3f);
            while (!worldManager.sceneLoaded)
            {
                yield return delay;
            }
            worldManager.DelayPlayerMove();
            delayStart = false;
        }

        IEnumerator checkStreamer()
        {
            AtavismLogger.LogDebugMessage("AtavismClient OnSceneLoaded streamers check Start->" + streamers.Count);
            WaitForSeconds delay2 = new WaitForSeconds(2f);
            WaitForSeconds delay = new WaitForSeconds(0.3f);
            yield return delay2;
            float progres = 0f;
            bool rundelay = true;
            int delayCount = 0;
            if (streamers.Count > 0)
                while (progres < 1f && rundelay)
                {
                    try
                    {
                        progres = 0f;
                        for (int i = 0; i < streamers.Count; i++)
                        {
                            if (streamers[i] != null)
                            {
                                AtavismLogger.LogDebugMessage("AtavismClient.checkStreamer.streamers.for loaded->" + streamers[i].GetTilesLoaded() + " / " + streamers[i].GetTilesToLoad());
                                progres += streamers[i].GetLoadingProgress() / (float)streamers.Count;
                            }
                        }
                        if (progres == 1f)
                        {
                            AtavismLogger.LogDebugMessage("AtavismClient OnSceneLoaded streamers check  progres->" + progres + " delay end delayCount=" + delayCount);
                            delayCount++;
                            if (delayCount > 20)
                                rundelay = false;
                        }
                        else
                        {
                            delayCount = 0;
                        }

                    }
                    catch (Exception e)
                    {
                        AtavismLogger.LogError("checkStreamer: Exception " + e.Message + " " + e);
                    }
                    yield return delay;
                    AtavismLogger.LogDebugMessage("AtavismClient OnSceneLoaded streamers check progres->" + progres);
                }
            try
            {
                AtavismLogger.LogInfoMessage("Level was loaded checkStreamer ");
                AtavismLogger.LogDebugMessage("AtavismClient OnSceneLoaded streamers check End->" + streamers.Count);
                string[] args = new string[1];
                args[0] = "streamer";
                worldManager.sceneLoaded = true;
                worldManager.sceneInLoading = false;
                AtavismEventSystem.DispatchEvent("LOADING_SCENE_END", args);
                networkHelper.SendSceneLoadedMessage();
            }
            catch (Exception e)
            {
                AtavismLogger.LogError("checkStreamer: Exception " + e.Message + " " + e);
            }
        }

        public void SceneReady()
        {
            worldManager.sceneLoaded = true;
        }

        public void Login(string username, string password)
        {
            Login(username, password, new Dictionary<string, object>());
        }

        /// <summary>
        /// Login to the master server. 
        /// </summary>
        public void Login(string username, string password, Dictionary<string, object> props)
        {
            this.MasterServer = DefaultMasterServer;

            if (AtavismNetworkHelper.Instance == null)
            {
                networkHelper = new AtavismNetworkHelper(worldManager);
            }
            else
            {
                networkHelper = AtavismNetworkHelper.Instance;
            }
            // Wait til now to set the networkHelper.UseTCP flag,
            // because the ClientInit.py may have overriden it.
            networkHelper.UseTCP = this.UseTCP;
            if (standalone)
                this.LoopbackWorldServerEntry = networkHelper.GetWorldEntry("standalone");
            if (this.LoopbackWorldServerEntry != null)
            {
                string worldId = this.LoopbackWorldServerEntry.Name;
                // Bypass the login and connection to master server
                loginSettings.worldId = worldId;
                networkHelper.SetWorldEntry(worldId, this.LoopbackWorldServerEntry);
                networkHelper.AuthToken = this.LoopbackIdToken;
            }
            else
            {
                // Show the login dialog.  If we successfully return from this
                // we have initialized the helper's world entry map with the 
                // resolveresponse data.
                AtavismLogger.LogDebugMessage("About to call master login");
                loginSettings.username = username;
                loginSettings.password = password;
                loginSettings.worldId = DefaultWorldId;
                loginSettings.props = props;
                //loginThread = new Thread(MasterLoginAsync);
                loginAttempts = 0;
                loginStep = 0;
                loginHelper = new AtavismLoginHelper(loginSettings, networkHelper, false);

                string[] args = new string[1];
                args[0] = "Logging In ...";
                AtavismEventSystem.DispatchEvent("LOGIN_RESPONSE", args);
                StartCoroutine(MasterLoginAsync());

            }
        }

        public bool CreateAccount(string username, string password, string email)
        {
            return CreateAccount(username, password, email, new Dictionary<string, object>());
        }

        /// <summary>
        /// Send a request to make a new account to the master server. Assumes the username, password 
        /// and email fields have already been checked.
        /// </summary>
        public bool CreateAccount(string username, string password, string email, Dictionary<string, object> props)
        {
            this.MasterServer = DefaultMasterServer;

            // Prefetch policy document for master server

            if (AtavismNetworkHelper.Instance == null)
            {
                networkHelper = new AtavismNetworkHelper(worldManager);
            }
            else
            {
                networkHelper = AtavismNetworkHelper.Instance;
            }
            // Wait til now to set the networkHelper.UseTCP flag,
            // because the ClientInit.py may have overriden it.
            networkHelper.UseTCP = this.UseTCP;
            loginSettings.username = username;
            loginSettings.password = password;
            loginSettings.worldId = DefaultWorldId;
            loginSettings.createAccount = true;
            loginSettings.emailAddress = email;
            loginSettings.props = props;
            AtavismLoginHelper loginHelper = new AtavismLoginHelper(loginSettings, networkHelper, false);
            return loginHelper.CreateAccountMaster();
        }

        public void Disconnected()
        {
            disconnect = true;
        }

        public void DoDisconnect()
        {
            if (gameState == GameState.World)
            {
                gameState = GameState.Login;
                worldManager.Disconnected();
                string[] args = new string[1];
                AtavismEventSystem.DispatchEvent("LOGGED_OUT", args);
            }
            else if (gameState == GameState.Character)
            {
                gameState = GameState.Login;
                worldManager.Disconnected();
                string[] args = new string[1];
                AtavismEventSystem.DispatchEvent("LOGGED_OUT", args);
                if (AtavismClient.Instance != null)
                    AtavismClient.Instance.LoadLevel(AtavismClient.Instance.LoginScene);
                else
                    SceneManager.LoadScene(0);
            }else
            {
                AtavismLogger.LogDebugMessage("Disconnected gameState=" + gameState);

            }
        }

        
        /// <summary>
        /// Login to the master server.
        /// </summary>
        protected bool MasterLogin()
        {
            loginHelper = new AtavismLoginHelper(loginSettings, networkHelper, false);
            //loginHelper.SetWorld(loginSettings.worldId);
            int loginStep = 0;
            NetworkHelperStatus status =  loginHelper.LoginMaster(DefaultWorldId, ref loginStep);
            if (status == NetworkHelperStatus.WorldResolveSuccess)
                return true;
            else
                return false;
            
        }

        protected IEnumerator MasterLoginAsync()
        {
            while (loginAttempts < 10 && LoginStatus != "Checking" && LoginError == "")
            {
                NetworkHelperStatus status = loginHelper.LoginMaster(DefaultWorldId, ref loginStep); 
                if (status != NetworkHelperStatus.WorldResolveChecking)
                {
                    if (status != NetworkHelperStatus.MasterTcpConnectFailure)
                    {
                        String[] args = new string[1];
                        args[0] = loginHelper.ErrorMessage;
                        AtavismEventSystem.DispatchEvent("LOGIN_RESPONSE", args);
                    }

                    loginAttempts++;
                    if (loginAttempts == 10)
                    {
                        loginHelper.StatusMessage = "";
                        loginHelper.ErrorMessage = "Could not connect to Authentication Server";
                        string[] args = new string[1];
                        args[0] = loginHelper.ErrorMessage;
                        AtavismEventSystem.DispatchEvent("LOGIN_RESPONSE", args);
                    }
                }
                else
                {
                    loginHelper.StatusMessage = "Checking";
                    
                    
                }
                
                yield return new WaitForSeconds(0.5f);
            }

            while (LoginStatus != "Success" && LoginError == "")
            {
                NetworkHelperStatus status = loginHelper.ResolveWorldChecking();
                switch (status)
                {
                    case NetworkHelperStatus.WorldResolveChecking:
                        break;
                    case NetworkHelperStatus.WorldResolveSuccess:
                        //parentForm.DialogResult = DialogResult.OK;
                        AtavismLogger.LogInfoMessage("Success: " + status);
                        loginHelper.StatusMessage = "Success";
                        //parentForm.Close ();
                        break;
                    case NetworkHelperStatus.WorldResolveFailure:
                        loginHelper.StatusMessage = "";
                        loginHelper.ErrorMessage = "Cannot find World Server";
                        break;
                    case NetworkHelperStatus.MasterConnectFailure:
                        loginHelper.StatusMessage = "";
                        loginHelper.ErrorMessage = "Unable to connect to Master Server";
                        break;
                    default:
                        loginHelper.StatusMessage = "";
                        loginHelper.ErrorMessage = "Unable to connect to World Server";
                        break;
                }
                yield return new WaitForSeconds(1f);
            }

            if (LoginStatus == "Success")
            {
                AtavismLogger.LogInfoMessage("Connected to master, getting world address...");
                // Update our media tree (base this on the world to which we connect)
                WorldServerEntry entry = networkHelper.GetWorldEntry(loginSettings.worldId);
                AtavismLogger.LogInfoMessage("Connected to master, got world address: " + entry.Hostname + " for world: " + loginSettings.worldId);

                ConnectToLogin();
            }
        }

        protected CharacterEntry SelectAnyCharacter(List<CharacterEntry> entries)
        {
            if (entries.Count <= 0)
            {
                return null;
            }
            return entries[0];
        }

        protected CharacterEntry SelectCharacter(List<CharacterEntry> entries, OID characterId)
        {
            foreach (CharacterEntry entry in entries)
            {
                if (entry.CharacterId.compareTo(characterId) == 0)
                    return entry;
            }
            return null;
        }

        public bool GetGameServerList()
        {
            NetworkHelperStatus status = networkHelper.GetServerList(clientVersion);
            if (status == NetworkHelperStatus.Success)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public Task<bool> ConnectToGameServer(string worldId)
        {
            if (this.loginSettings.worldId.Length > 0)
            {
                try
                {
                    networkHelper.Disconnect();
                }
                catch (Exception e)
                {
                }

                this.MasterServer = DefaultMasterServer;
                if (!MasterLogin())
                    return Task.FromResult(false);

                CharacterEntries.Clear();

                this.loginSettings.worldId = worldId;
                return ConnectToLogin();
            }
            else
            {
                CharacterEntries.Clear();
                this.loginSettings.worldId = worldId;
                AtavismClient.instance.loginSuccess = true;
            }
          
           return null;
        }

        protected Task<bool> ConnectToLogin()
        {
            return ConnectToWorld(WorldConnectionState.ConnectingToLogin);
        }

        protected Task<bool> ConnectToProxy()
        {
            return ConnectToWorld(WorldConnectionState.ConnectingToProxy);
        }

        protected Task<bool> ConnectToWorld(WorldConnectionState state)
        {
            if (worldConnectionCTS != null)
            {
                worldConnectionCTS.Cancel();
                worldConnectionCTS.Dispose();
                worldConnectionCTS = null;
            }
            worldConnectionCTS = new CancellationTokenSource();
            return Task.Run<bool>(async () => await ConnectToWorldAsync(state, worldConnectionCTS.Token));
        }

        protected async Task<bool> ConnectToWorldAsync(WorldConnectionState state, CancellationToken token)
        {
            AtavismLogger.LogDebugMessage("ConnectToWorldAsync: Started on thread " + Thread.CurrentThread.Name);
            try
            {
                InitWorldConnect();
                worldConnectionState = state;
                bool connected = true;
                if (state == WorldConnectionState.ConnectingToLogin)
                {
                    connected = await ConnectToLoginAsync(token);
                }
                else if (state == WorldConnectionState.ConnectingToProxy)
                {
                    connected = await ConnectToProxyAsync(token);
                }
                if (!connected || token.IsCancellationRequested)
                {
                    if (worldConnectionState != WorldConnectionState.Disconnected &&
                        worldConnectionState != WorldConnectionState.Disconnecting)
                    {
                        worldConnectionState = WorldConnectionState.Failed;
                    }
                    return false;
                }
                AtavismLogger.LogDebugMessage("ConnectToWorldAsync: Connected to server, setting filters");
                SetUpMessageFilters();
                if (state == WorldConnectionState.ConnectingToLogin)
                    InitializeStartupWorld();

                // Wait for startup messages to get processed by Update() for 10 seconds
                AtavismLogger.LogDebugMessage("ConnectToWorldAsync: Waiting for startup message sequence");
                for (int i = 0; i < 100; i++)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }
                    if (worldConnectionState == WorldConnectionState.ConnectedToLogin
                        || worldConnectionState == WorldConnectionState.ConnectedToProxy)
                    {
                        AtavismLogger.LogDebugMessage("ConnectToWorldAsync: Finished successfully");
                        return true;
                    }
                    Thread.Sleep(100);
                }
            }
            catch (Exception ex)
            {
                AtavismLogger.LogError("Got an exception in world connect: " + ex);
                gameState = GameState.Login;
            }
            worldConnectionState = WorldConnectionState.Failed;
            AtavismLogger.LogDebugMessage("ConnectToWorldAsync: Failed");
            return false;
        }

        protected void InitWorldConnect() 
        {
            // Set up the mask window, so that they don't see the loading
            MessageDispatcher.Instance.ClearQueue();
            networkHelper.DisconnectFromMaster();
            worldManager.ClearWorld();
            if (!networkHelper.HasWorldEntry(loginSettings.worldId))
                networkHelper.ResolveWorld(loginSettings);
            // We use the null setting to indicate that we haven't logged in, so
            // we need to reset it back to null before the connect.
            loginMessage = null;
        }

        protected async Task<bool> ConnectToLoginAsync(CancellationToken token)
        {
            NetworkHelperStatus status = await networkHelper.ConnectToLoginAsync(loginSettings.worldId, Version, token);
            if (status == NetworkHelperStatus.WorldTcpConnectFailure)
            {
                SendLoginErrorEvent("Unable to connect to tcp world server");
                AtavismLogger.LogError(errorMessage);
                return false;
            }
            else if (status == NetworkHelperStatus.UnsupportedClientVersion)
            {
                SendLoginErrorEvent("Server does not support this client version");
                AtavismLogger.LogError(errorMessage);
                return false;
            }
            else if (status != NetworkHelperStatus.Success && status != NetworkHelperStatus.Standalone)
            {
                SendLoginErrorEvent("Unable to connect to server");
                return false;
            }

            if (AtavismLogger.logLevel <= LogLevel.Debug)
            {
                foreach (Dictionary<string, object> c in networkHelper.CharacterEntries)
                {
                    AtavismLogger.LogDebugMessage("Character: num props: " + c.Count);
                    foreach (string attr in c.Keys)
                    {
                        AtavismLogger.LogDebugMessage("  " + attr + " => " + c[attr]);
                    }
                }
            }
            return true;
        }   

        protected void SetUpMessageFilters()
        {
            // We need to hook our message filter, whether or not we are 
            // standalone, so instead of doing it later (right before 
            // RdpWorldConnect), do it here.
            RequireLoginFilter checkAndHandleLogin = new RequireLoginFilter(worldManager);
            MessageDispatcher.Instance.SetWorldMessageFilter(checkAndHandleLogin.ShouldQueue);
        }

        protected async Task<bool> ConnectToProxyAsync(CancellationToken token)
        {

            //Monitor.Enter(scene);
            try
            {

                CharacterEntry charEntry;
                if (loginSettings.characterId != null)
                {
                    AtavismLogger.LogInfoMessage("Selecting character with id: " + loginSettings.characterId);
                    charEntry = SelectCharacter(networkHelper.CharacterEntries, loginSettings.characterId);
                }
                else
                {
                    charEntry = SelectAnyCharacter(networkHelper.CharacterEntries);
                }

                string proxyHostname = charEntry.Hostname;
                int proxyPort = charEntry.Port;
                while (proxyHostname == null && !token.IsCancellationRequested)
                {
                    NetworkHelperStatus tokenStatus = networkHelper.GetProxyToken(charEntry.CharacterId);
                    if (tokenStatus == NetworkHelperStatus.WorldEnterQueue)
                    {
                        Thread.Sleep(250);
                        continue;
                    }
                    if (tokenStatus == NetworkHelperStatus.WorldEnterFailure)
                    {
                        worldManager.PlayerId = 0;
                        return true;
                    }

                    if (tokenStatus != NetworkHelperStatus.Success)
                    {
                        AtavismLogger.LogInfoMessage("Failed to get proxy Settings");
                        worldConnectionState = WorldConnectionState.Disconnecting;
                        return false;
                    }
                    proxyHostname = networkHelper.ProxyPluginHost;
                    proxyPort = networkHelper.ProxyPluginPort;
                }
                AtavismLogger.LogInfoMessage("Got proxy Settings");
                networkHelper.DisconnectFromLogin();
                AtavismLogger.LogInfoMessage("Disconnected from Login");

                string actualHost = proxyHostname;
                if (actualHost == ":same")
                    actualHost = AtavismNetworkHelper.Instance.LoginPluginHost;

                AtavismLogger.LogInfoMessage("About to connect to world");
                NetworkHelperStatus status = networkHelper.ConnectToWorld(charEntry.CharacterId,
                                                        actualHost,
                                                        proxyPort,
                                                        clientVersion + ", " + ClientCapabilities);
                if (status != NetworkHelperStatus.Success)
                {
                    AtavismLogger.LogError("World Connect Status: " + status);
                    return false;
                }
            }
            catch (Exception ex)
            {
                AtavismLogger.LogError("Got an exception in world connect: " + ex);
                worldConnectionState = WorldConnectionState.Disconnecting;
                return false;
            }

            return true;
        }

        void SendLoginErrorEvent(string errorMessage)
        {
            string[] args = new string[1];
            args[0] = errorMessage;
            AtavismEventSystem.DispatchEvent("LOGIN_RESPONSE", args);
        }

        void LoadCharacterScene()
        {
            LoadLevel(characterScene);
        }

        /// <summary>
        ///   Set up our hooks for handling messages.
        /// </summary>
        protected void SetupMessageHandlers()
        {
            // Register our handler for the Portal messages, so that we
            // can drop our connection to the world server, and establish a new 
            // connection to the new world.
            MessageDispatcher.Instance.RegisterHandler(WorldMessageType.Portal,
                                                           new WorldMessageHandler(this.HandlePortal));
            // Register our handler for the UiTheme messages, so that we
            // can swap out the user interface.
            //MessageDispatcher.Instance.RegisterHandler(WorldMessageType.UiTheme,
            //                                           new WorldMessageHandler(this.HandleUiTheme));

            // Register our handler for the AuthorizedLoginResponse messages, so that we
            // can throw up a dialog if needed.  This version allows us to get the server
            // version information as well (and possibly capabilities).
            MessageDispatcher.Instance.RegisterHandler(WorldMessageType.AuthorizedLoginResponse,
                                                       new WorldMessageHandler(this.HandleAuthorizedLoginResponse));
        }

        public void InitializeStartupWorld()
        {
            // First, the login response
            AuthorizedLoginResponseMessage loginResponse = new AuthorizedLoginResponseMessage();
            loginResponse.Oid = 0;
            loginResponse.Success = true;
            loginResponse.Message = "standalone";
            loginResponse.Version = "standalone";
            MessageDispatcher.Instance.QueueMessage(loginResponse);
            // Next, set up the world
            String terrainInitString = "<Terrain><algorithm>HybridMultifractalWithFloor</algorithm><xOffset>0</xOffset><yOffset>0</yOffset><zOffset>0</zOffset><h>0.25</h><lacunarity>2</lacunarity><octaves>8</octaves><metersPerPerlinUnit>500</metersPerPerlinUnit><heightScale>0</heightScale><heightOffset>0</heightOffset><fractalOffset>0.7</fractalOffset><heightFloor>0</heightFloor></Terrain>";
            TerrainConfigMessage terrainConfigMessage = new TerrainConfigMessage();
            terrainConfigMessage.ConfigString = terrainInitString;
            MessageDispatcher.Instance.QueueMessage(terrainConfigMessage);
            // Now set up the player stub
            NewObjectMessage newObjMessage = new NewObjectMessage();
            newObjMessage.Oid = 0;
            newObjMessage.ObjectId = 0;
            newObjMessage.Name = "char";
            newObjMessage.Location = Vector3.zero;
            //newObjMessage.Location = new Vector3 (0, 0, 0);
            newObjMessage.Orientation = Quaternion.identity;
            newObjMessage.ScaleFactor = Vector3.one;
            newObjMessage.ObjectType = ObjectNodeType.User;
            newObjMessage.FollowTerrain = false;
            MessageDispatcher.Instance.QueueMessage(newObjMessage);
        }

        public void EnterGameWorld(OID characterOid)
        {
            if (characterOid == null)
                return;
            PortalMessage portalMessage = new PortalMessage();
            portalMessage.WorldId = WorldId;
            portalMessage.CharacterId = characterOid;
            MessageDispatcher.Instance.QueueMessage(portalMessage);
            AtavismLogger.LogInfoMessage("Logging in to world: " + WorldId + " with characterId: " + characterOid);
        }

        public void GetCharacters()
        {

            networkHelper.GetCharacters(clientVersion);
        }

        private void HandleAuthorizedLoginResponse(BaseWorldMessage message)
        {
            AuthorizedLoginResponseMessage loginResponse = (AuthorizedLoginResponseMessage)message;
            loginFailed = !loginResponse.Success;
            loginMessage = loginResponse.Message;
            worldServerVersion = loginResponse.Version;
            AtavismLogger.LogInfoMessage("In HandleAuthorizedLoginResponse, testing server version " + worldServerVersion);
            if ((worldServerVersion.Length >= 10 && worldServerVersion.Substring(0, 10) == "2007-07-20") ||
                    (worldServerVersion.Length >= 3 && worldServerVersion.Substring(0, 3) == "1.0"))
                fullyInitialized = true;
            if (worldServerVersion.Length >= 3 && worldServerVersion.Substring(0, 3) == "1.5")
                serverRelease1_5 = true;
            AtavismLogger.LogInfoMessage("World Server Version: " + worldServerVersion);
        }

        /// <summary>
        /// Handles the portal message which is used to log the player into the world.
        /// </summary>
        /// <param name="message"></param>
        private void HandlePortal(BaseWorldMessage message)
        {
            PortalMessage portalMessage = (PortalMessage)message;
            AtavismLogger.LogInfoMessage("Transporting via portal to alternate world.");
          //  AtavismLogger.LogError("Transporting via portal to alternate world.");

            loginSettings.worldId = portalMessage.WorldId;
            loginSettings.characterId = portalMessage.CharacterId;

            // Get character's world property along with the world files url
            CharacterEntry selectedChar = SelectCharacter(networkHelper.CharacterEntries, loginSettings.characterId);
            currentWorld = (int)selectedChar["world"];
            bool isAdmin = (bool)selectedChar["worldAdmin"];

            portalMessage.AbortHandling = true;
            ConnectToProxy();
        }

        public void LoadLevel(string level)
        {
            WorldManager.TargetId = -1;
            worldManager.sceneInLoading = true;
            AtavismLogger.LogInfoMessage("AtClient.LoadLevel =>"+level+" async ->"+loadLevelAsync);
          //  System.Diagnostics.StackTrace t = new System.Diagnostics.StackTrace();
          //  Debug.LogError("AtClient.LoadLevel =>" + level + " async ->" + loadLevelAsync+" "+t.ToString());
            _loadLevelName = level;
            if (loadLevelAsync != null)
            {
                loadLevelAsync(level);
                string[] arg = new string[1];
                arg[0] = level;
                AtavismEventSystem.DispatchEvent("LOADING_SCENE_START", arg);
                return;
            }
            else
            {
                StartCoroutine(LoadNextScene(level));
            }
            string[] args = new string[1];
            args[0] = level;
            AtavismEventSystem.DispatchEvent("LOADING_SCENE_START", args);
        }

        private IEnumerator LoadNextScene(string level)
        {
            SceneManager.LoadSceneAsync(level);
            yield return null;
        }


        public void ProcessServerLogout(bool connectToLoginServer)
        {
            worldConnectionState = WorldConnectionState.Disconnected;
            if (connectToLoginServer)
            {
                networkHelper.Disconnect();
                worldManager.ClearWorld();
                ConnectToLogin();
            }
            else
            {
                worldManager.Disconnected();
                worldManager.ClearWorld();
                worldManager.sceneLoaded = false;
            }
            string[] args = new string[1];
            AtavismEventSystem.DispatchEvent("LOGGED_OUT", args);
        }

        void OnApplicationQuit()
        {
            // Disconnect from the server
            if (networkHelper != null)
            {

                networkHelper.Disconnect();
                networkHelper.TcpPrefabDisconnect();
            }
            else
            {

            }
        }


        #region Properties

        public static AtavismClient Instance
        {
            get
            {
                return instance;
            }
        }

        // Login settings
        // Default settings for login settings
        public string MasterServer
        {
            set
            {
                loginSettings.rdpServer = value;
                loginSettings.tcpServer = value;
            }
        }
        public string PrefabServer
        {
            set
            {
                loginSettings.rdpServer = value;
                loginSettings.tcpServer = value;
            }
        }
        public ushort PrefabServerPort
        {
            set
            {
                loginSettings.tcpPort = value;

            }
        }
        public ushort MasterServerTcpPort
        {
            set
            {
                loginSettings.tcpPort = value;

            }
        }
        public ushort MasterServerRdpPort
        {
            set
            {
                loginSettings.rdpPort = value;

            }
        }

        public string WorldId
        {
            get
            {
                return loginSettings.worldId;
            }
            set
            {
                loginSettings.worldId = value;
            }
        }

        public OID CharacterId
        {
            get
            {
                return loginSettings.characterId;
            }
            set
            {
                loginSettings.characterId = value;
            }
        }

        public string LoginUrl
        {
            set
            {
                loginSettings.loginUrl = value;
            }
        }
        /// <summary>
        ///   Override for the world specific patcher url
        /// </summary>
        public string WorldPatcherUrl
        {
            set
            {
                worldPatcherUrl = value;
            }
            get
            {
                return worldPatcherUrl;
            }
        }
        /// <summary>
        ///   Override for the world specific asset url
        /// </summary>
        public string WorldUpdateUrl
        {
            set
            {
                worldUpdateUrl = value;
            }
            get
            {
                return worldUpdateUrl;
            }
        }

        public AtavismNetworkHelper NetworkHelper
        {
            get
            {
                return networkHelper;
            }
        }

        public AtavismWorldManager WorldManager
        {
            get
            {
                return worldManager;
            }
        }

        public GameState AtavismState
        {
            get
            {
                return gameState;
            }
        }

        public static string Version
        {
            get
            {
                if (overrideVersion != null)
                    return overrideVersion;
                return clientVersion;
            }
        }

        public IGameWorld GameWorld
        {
            set
            {
                gameWorld = value;
                gameWorld.WorldManager = worldManager;
            }
            get
            {
                return gameWorld;
            }
        }

        public bool IgnoreProps
        {
            get
            {
                return ignoreProps;
            }
            set
            {
                ignoreProps = value;
            }
        }

        public WorldServerEntry LoopbackWorldServerEntry
        {
            get
            {
                return loopbackWorldServerEntry;
            }
            set
            {
                loopbackWorldServerEntry = value;
            }
        }

        public byte[] LoopbackIdToken
        {
            get
            {
                return loopbackIdToken;
            }
            set
            {
                loopbackIdToken = value;
            }
        }

        public List<CharacterEntry> CharacterEntries
        {
            get
            {
                return networkHelper.CharacterEntries;
            }
        }

        public int NumCharacterSlots
        {
            get
            {
                return networkHelper.NumCharacterSlots;
            }
        }

        public Dictionary<string, WorldServerEntry> WorldServerMap
        {
            get
            {
                return networkHelper.WorldServerMap;
            }
        }

        public string ErrorMessage
        {
            get
            {
                return errorMessage;
            }
        }

        public bool UseTCP
        {
            get
            {
                return useTCP;
            }
            set
            {
                useTCP = value;
            }
        }

        public GameObject ScriptObject
        {
            get
            {
                return scriptObject;
            }
            set
            {
                scriptObject = value;
            }
        }

        public AtavismInputController ActiveInputController
        {
            get
            {
                return worldManager.ActiveInputController;
            }
        }

        public static bool ServerRelease1_5
        {
            get
            {
                return serverRelease1_5;
            }
        }

        ///<summary>
        ///    Getter/Setter for data member that determines if collision 
        ///    volumes instead of bounding boxes are used to determine distances 
        ///    to objects.
        ///</summary>
        public bool TargetBasedOnCollisionVolumes
        {
            get
            {
                return targetBasedOnCollisionVolumes;
            }
            set
            {
                targetBasedOnCollisionVolumes = value;
            }
        }

        public string LoginScene
        {
            get
            {
                return loginScene;
            }
        }

        public string LoginStatus
        {
            get
            {
                if (loginHelper == null)
                    return "";
                return loginHelper.StatusMessage;
            }
        }

        public string LoginError
        {
            get
            {
                if (loginHelper == null)
                    return "";
                return loginHelper.ErrorMessage;
            }
        }

        public LoadLevelAsync LoadLevelAsync
        {
            get
            {
                return loadLevelAsync;
            }
            set
            {
                loadLevelAsync = value;
            }
        }

        public bool ResyncMobs
        {
            get
            {
                return resyncMobs;
            }
            set
            {
                resyncMobs = value;
            }
        }

        #endregion
    }
}
