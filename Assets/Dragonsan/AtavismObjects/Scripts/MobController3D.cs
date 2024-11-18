using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
#if UNITY_5_5_OR_NEWER
using UnityEngine.Profiling;
#endif

namespace Atavism
{

    /// <summary>
    /// The main 3D mob movement class. Manages movement properties such as speed and state,
    /// handles jumping and gravity and calls controller.Move() when DoMovement() is called.
    /// </summary>
    public abstract  class MobController3D : AtavismMobController
    {

        #region Animation Fields
        protected bool inCombat = false;
        protected string mountAnim = "Mount";

        #endregion Animation Fields

        #region Movement Fields
        protected Vector3 movement = Vector3.zero;
        protected Vector3 lastmovement = Vector3.zero;

        protected bool dead = false;
        protected string state = "";
        protected bool wnomove = false;
        // Is Walk on
        protected bool walk = false;
        // The speed when walking
        //float walkSpeed = 2.0f;
        public float runThreshold = 2.5f;

        // The gravity for the character
        float gravity = 20.0f;
        // The gravity in controlled descent mode
        //	float speedSmoothing = 10.0f;
        //	float rotateSpeed = 5.0f; // was 250
        //	float trotAfterSeconds = 3.0f;

        // The current vertical speed
        private float defaultverticalSpeed = -2.5f;

        private float verticalSpeed = 0.0f;
        // The current x-z move speed
        private float moveSpeed = 0.0f;

        // The last collision flags returned from controller.Move
        private CollisionFlags collisionFlags;

        // Are we moving backwards (This locks the camera to not do a 180 degree spin)
        private bool movingBack = false;
        public float combatSpeed = 0.8f;
        protected bool skillMove = false;
        protected float skillMoveSpeed = 0f;
        protected bool noMove = false;
        protected float noMoveTime = 0f;
        protected int mstate = 0;
        #region Jumping fields
        // How high do we jump when pressing jump and letting go immediately
        public float jumpHeight = 1.5f;
        // Last time we performed a jump
        private float lastJumpTime = -1.0f;
        bool canJump = true;
        private float jumpRepeatTime = 0.05f;
        private float jumpTimeout = 0.15f;
        private float groundedTimeout = 0.25f;
        private Vector3 inAirVelocity = Vector3.zero;
        private float lastGroundedTime = 0.0f;
        private bool isControllable = true;

        public bool slideWhenOverSlopeLimit = true;
        private float slideLimit;
        private Vector3 contactPoint;
        protected bool isFalling = false;
        protected float fallingDistance = 0;
        protected float fallStartHeight = float.MinValue;
        protected float fallThreshold = 3f;
        #endregion Jumping fields

      //  protected GameObject mount;

        protected float waterHeight = float.MinValue;
        bool underWater = false;

         protected const int MOVEMENT_STATE_WALKING = 1;
        protected const int MOVEMENT_STATE_SWIMMING = 2;
        protected const int MOVEMENT_STATE_FLYING = 3;
        #endregion Movement Fields

        public GameObject friendlyTargetDecal;
        public GameObject neutralTargetDecal;
        public GameObject enemyTargetDecal;
        GameObject targetDecal;

        public bool checkUnderGround = true;
        public RaycastHit groundHit;
        public LayerMask groundDistanceLayer = (1 << 0) | (1 << 30) | (1 << 26) | (1 << 20);
        public float maxGroundDistance = 20f;
        protected float groundDistance;
        protected CharacterController _capsuleCollider;
        public float colliderRadius, colliderHeight;
        public Vector3 colliderCenter;
        //  float jumptimecheck = 0f;
        AtavismNode node;
        [Header("Surface Align")]
        public float RayCastUpdateSeconds = 0.1f;
        float RayCastUpdateTimer;
        int AlignmentLayerMask = 1 << 11;
        public bool alignSurface = true;
        //Dodge options
        public bool dodgeMove = false;
        private float dodgeMoveSpeed = 0f;
        public Vector3 dodgeOrientation = Vector3.zero;
        
        // Use this for initialization
        void Start()
        {
          //  movementState = MOVEMENT_STATE_WALKING; // Used to switch between running (1), swimming (2) and flying (3)

        }

        protected virtual void ObjectNodeReady()
        {
            
            this.oid = GetComponent<AtavismNode>().Oid;
            if (node == null)
                node = GetComponent<AtavismNode>();
            else if (!node.Oid.Equals(GetComponent<AtavismNode>().Oid))
                node = GetComponent<AtavismNode>();
      
            GetComponent<AtavismNode>().SetMobController(this);
            moveSpeed = runSpeed;
            if (GetComponent<CharacterController>() != null)
            {
                slideLimit = GetComponent<CharacterController>().slopeLimit - 0.1f;
            }

            node.RegisterObjectPropertyChangeHandler("deadstate", HandleDeadState);
            node.RegisterObjectPropertyChangeHandler("state", HandleState);
            node.RegisterObjectPropertyChangeHandler("combatstate", HandleCombatState);
            node.RegisterObjectPropertyChangeHandler("waterHeight", HandleWaterHeight);
            node.RegisterObjectPropertyChangeHandler("aggressive", HandleAggressive);
            node.RegisterObjectPropertyChangeHandler("world.nomove", HandleNoMove);
            // Get deadstate property
            if (node.PropertyExists("deadstate"))
            {
                dead = (bool)node.GetProperty("deadstate");
            }
            // Get state property
            if (node.PropertyExists("state"))
            {
                state = (string)node.GetProperty("state");
            }
            // Get MovementState property
            node.RegisterObjectPropertyChangeHandler("movement_state", MovementStateHandler);
            if (node.PropertyExists("movement_state"))
            {
                mstate = (int)node.GetProperty("movement_state");
                movementState = mstate;
            }
            // Get WaterHeight property
            if (node.PropertyExists("waterHeight"))
            {
                waterHeight = (float)node.GetProperty("waterHeight");
            }
            // Get Mount property
            node.RegisterObjectPropertyChangeHandler("mount", HandleMount);
            if (node.PropertyExists("mount"))
            {
                string mountProp = (string)node.GetProperty("mount");
                if (mountProp != "")
                {
                    StartMount(mountProp);
                }
                else
                {
                    StopMount();
                }
            }
            //GetComponent<AtavismNode>().RegisterObjectPropertyChangeHandler("currentAction", null);
            transform.position = new Vector3(transform.position.x, transform.position.y +ClientAPI.Instance.spawnMobAdditionalY, transform.position.z);

            // Get this mob to ignore collisions with other mobs/players
            foreach (AtavismMobNode mNode in ClientAPI.WorldManager.GetMobNodes())
            {
                if (mNode.GameObject != null && mNode.GameObject.GetComponent<Collider>() != null && GetComponent<Collider>() != mNode.GameObject.GetComponent<Collider>())
                    foreach (Collider nCollider in mNode.GameObject.GetComponents<Collider>())
                    {
                        foreach (Collider mCollider in GetComponents<Collider>())
                            Physics.IgnoreCollision(mCollider, nCollider);
                    }
                //            Physics.IgnoreCollision (GetComponent<Collider>(), mNode.GameObject.GetComponent<Collider>());
            }
            //if (go != gameObject)
            if (ClientAPI.GetPlayerObject() != null && !isPlayer)
                if (ClientAPI.GetPlayerObject().GameObject.GetComponent<Collider>() != null && GetComponent<Collider>() != null)
                    foreach (Collider pCollider in ClientAPI.GetPlayerObject().GameObject.GetComponents<Collider>())
                    {
                        foreach (Collider mCollider in GetComponents<Collider>())
                            Physics.IgnoreCollision(mCollider, pCollider);
                    }
            //        Physics.IgnoreCollision (GetComponent<Collider>(), ClientAPI.GetPlayerObject().GameObject.GetComponent<Collider>());

            string[] args = new string[1];
            args[0] = oid.ToString();
            AtavismEventSystem.DispatchEvent("MOB_CONTROLLER_ADDED", args);
        }

        void OnDestroy()
        {
            string[] args = new string[1];
            args[0] = oid.ToString();
            AtavismEventSystem.DispatchEvent("MOB_CONTROLLER_REMOVED", args);

            if (node != null)
            {
                node.RemoveObjectPropertyChangeHandler("deadstate", HandleDeadState);
                node.RemoveObjectPropertyChangeHandler("state", HandleState);
                node.RemoveObjectPropertyChangeHandler("combatstate", HandleCombatState);
                node.RemoveObjectPropertyChangeHandler("movement_state", MovementStateHandler);
                node.RemoveObjectPropertyChangeHandler("mount", HandleMount);
                node.RemoveObjectPropertyChangeHandler("waterHeight", HandleWaterHeight);
                node.RemoveObjectPropertyChangeHandler("aggressive", HandleAggressive);
            }
            //StopMount();
            if (mount != null)
                Destroy(mount);
        }

       protected CharacterController controller;

        protected void DoMovement()
        {
            if (node == null)
                node = GetComponent<AtavismNode>();

            if (SceneManager.GetActiveScene().name.Equals(ClientAPI.Instance.characterSceneName))
                return;
            
            if (!ClientAPI.WorldManager.sceneLoaded)
            {
                if (controller == null)
                    controller = GetComponent<CharacterController>();
                if (mount != null)
                {
                    controller = mount.GetComponent<CharacterController>();
                    transform.localPosition = Vector3.zero;
                }
                if (controller != null)
                    if (!controller.enabled)
                        controller.enabled = false;
                return;
            }
            
            if (controller == null)
                controller = GetComponent<CharacterController>();
            Profiler.BeginSample("MC3D DoMovement");
            if (isPlayer)
            {
                if ((!dead || state == "spirit") &&
                    ClientAPI.WorldManager.Player != null &&
                    ClientAPI.WorldManager.Player.CanMove())
                {
                    if (ClientAPI.WorldManager.sceneLoaded)
                    {
                        if (controller != null)
                            if (!controller.enabled)
                                controller.enabled = true;
                        movement = MovePlayer();
                    }
                    else
                    {
                        if (controller != null)
                            if (controller.enabled)
                                controller.enabled = false;
                    }
                }
                else
                {
                    SkillMove(0f);
                    // Still need to get player movement even when dead
                    AtavismInputController inputManager = ClientAPI.GetInputController();
                    inputManager.GetPlayerMovement();
                    if (transform.position.y > waterHeight)
                    {
                        ApplyGravity();
                    }
                    movement = new Vector3(0, verticalSpeed * Time.deltaTime, 0) + inAirVelocity;
                }
            }
            else if (!dead && !wnomove)
            {
                if (checkUnderGround && AtavismClient.Instance.AtavismState == GameState.World)
                {
                    bool isAboveTerrain = IsAboveTerrain();
                    int attempts = 0;
                    while (!isAboveTerrain && attempts < 20 && groundLayers > 0)
                    {
                        transform.position += new Vector3(0, 1.5f, 0);
                        isAboveTerrain = IsAboveTerrain();
                        attempts++;
                    }
                }
                if (controller != null)
                    if (!controller.enabled)
                        controller.enabled = true;
                movement = MoveMob();
            }
            else
            {
                bool isAboveTerrain = IsAboveTerrain();
                int attempts = 0;
                while (!isAboveTerrain && attempts < 20 && groundLayers > 0)
                {
                    transform.position += new Vector3(0, 1.5f, 0);
                    isAboveTerrain = IsAboveTerrain();
                    attempts++;
                }
                if (transform.position.y > waterHeight)
                {
                    ApplyGravity();
                }
                lastDirTimestamp = Time.time;
                lastLocTimestamp = Time.time;
                movement = new Vector3(0, verticalSpeed * Time.deltaTime, 0) + inAirVelocity;
            }
            // Move the controller
            if (controller == null)
                controller = GetComponent<CharacterController>();
            if (controller == null)
                return;
            if (mount != null)
            {
                controller = mount.GetComponent<CharacterController>();
                //transform.rotation = Quaternion.identity;
                transform.localPosition = Vector3.zero;
                //transform.localRotation = Quaternion.identity;
            }
            if (controller != null)
                if (!controller.enabled)
                  controller.enabled = true;
            if (movement != Vector3.zero || lastmovement != movement)
            {
               // Debug.LogWarning("MC3D  move " + oid + " movement=" + movement.ToString("F8") + " verticalSpeed=" + verticalSpeed + " inAirVelocity=" + inAirVelocity);
                collisionFlags = controller.Move(movement);
                lastmovement = movement;
            }

            // Update facing if needed
            if (!isPlayer && target != -1 && !dead)
            {
                AtavismMobNode mobNode = (AtavismMobNode)AtavismClient.Instance.WorldManager.GetObjectNode(oid);
                AtavismObjectNode targetNode = AtavismClient.Instance.WorldManager.GetObjectNode(target);
                if (targetNode != null && targetNode.GameObject != null && mobNode.CanTurn() && movement.x == 0 && movement.z == 0)
                {
                    Vector3 relativePos = ((targetNode.MobController == null ||(targetNode.MobController != null && targetNode.MobController.Mount == null) )? targetNode.GameObject.transform.position : targetNode.MobController.Mount.transform.position);
                    // the second argument, upwards, defaults to Vector3.up
                    if (mount != null)
                    {
                        relativePos.y = mount.transform.position.y;
                        Quaternion rotation = Quaternion.LookRotation(relativePos - mount.transform.position, Vector3.up);
                        if (Quaternion.Angle(mount.transform.rotation, rotation) > 1f)
                            mount.transform.LookAt(relativePos);
                    }
                    else
                    {

                        relativePos.y = transform.position.y;
                        Quaternion rotation = Quaternion.LookRotation(relativePos - transform.position, Vector3.up);
                      //    Debug.LogError("Mob " + oid + " Vextor Angle " + Vector3.Angle(transform.position, relativePos) + " Quat Angle " + Quaternion.Angle(transform.rotation, rotation) + " rotation=" + rotation + " rot Angles" + rotation.eulerAngles + " mrot angles " +transform.rotation.eulerAngles);
                        // if (Quaternion.Angle(transform.rotation, rotation) > 1f)
                        //     mobNode.SetOrientation(rotation);
                    }
                }
            }
            else if (isPlayer && ClientAPI.GetInputController() is ClickToMoveInputController)
            {
                if (inCombat && movement.magnitude < 0.2f)
                {
                    AtavismMobNode mobNode = (AtavismMobNode)AtavismClient.Instance.WorldManager.GetObjectNode(oid);
                    AtavismMobNode targetNode = ClientAPI.GetTargetObject();
                    if (targetNode != null && mobNode.CanTurn() && targetNode.GameObject!=null&& movement.x == 0 && movement.z == 0)
                    {
                        Vector3 relativePos = (targetNode.MobController.Mount == null ? targetNode.GameObject.transform.position : targetNode.MobController.Mount.transform.position);//- mobNode.GameObject.transform.position;

                        // the second argument, upwards, defaults to Vector3.up
                     
                        if (mount != null)
                        {
                            relativePos.y = mount.transform.position.y;
                            Quaternion rotation = Quaternion.LookRotation(relativePos - mount.transform.position, Vector3.up);
                            if (Quaternion.Angle(mount.transform.rotation, rotation) > 1f)
                             mount.transform.LookAt(relativePos);
                        }
                        else
                        {
                           relativePos.y = transform.position.y;
                            Quaternion rotation = Quaternion.LookRotation(relativePos - transform.position, Vector3.up);
                           //  Debug.LogError("Mob " + oid + " Vextor Angle " + Vector3.Angle(transform.position, relativePos) + " Quat Angle " + Quaternion.Angle(transform.rotation, rotation) + " rotation=" + rotation + " rot Angles" + rotation.eulerAngles + " mrot angles " +transform.rotation.eulerAngles);
                            if (Quaternion.Angle(transform.rotation, rotation) > 1f)
                                mobNode.SetOrientation(rotation);
                        }
                    }
                }
            }

            // Set rotation to the move direction
            /*if (IsGrounded ()) {
                transform.rotation = rotation;
                //transform.rotation = Quaternion.LookRotation (moveDirection);

            } else {
                var xzMove = movement;
                xzMove.y = 0;
                if (xzMove.sqrMagnitude > 0.001) {
                    transform.rotation = Quaternion.LookRotation (xzMove);
                }
            }*/

            // We are in jump mode but just became grounded
            if (IsGrounded())
            {
                lastGroundedTime = Time.time;
                inAirVelocity = Vector3.zero;
                if (jumping)
                {
                    jumping = false;
                    SendMessage("DidLand", SendMessageOptions.DontRequireReceiver);
                    if (mount != null)
                    {
                        mount.GetComponent<AtavismMount>().SetJumping(false);
                    }
                }
            }
            Profiler.EndSample();
        }

        public void LateUpdate()
        {
            // Update camera if it is the player
            if (isPlayer && ClientAPI.GetInputController() != null)
            {
                ClientAPI.GetInputController().RunCameraUpdate();
            }
            //to move  server side
            if (noMoveTime < Time.time && noMoveTime != 0f)
            {
                noMove = false;
                noMoveTime = 0f;
            }
            if (controller != null)
            {
                TestSpeed = new Vector3(controller.velocity.x, 0, controller.velocity.z).magnitude;
            }
        }
        float TestSpeed = 0f;

        public override Vector3 MoveMob()
        {

            float timeDifference = (Time.time - lastLocTimestamp);
            if (pathInterpolator != null)
            {
                PathLocAndDir locAndDir = pathInterpolator.Interpolate(Time.time);
                float interpolateSpeed = pathInterpolator.Speed;
//                UnityEngine.Debug.LogWarning("MobNode.ComputePosition: oid " + oid + ", followTerrain " + followTerrain + ", pathInterpolator " + (locAndDir == null ? "null" : locAndDir.ToString ())+" Pos="+ transform.position+" interpolateSpeed="+interpolateSpeed);
                if (locAndDir != null)
                {
                    /*if (locAndDir.LengthLeft > 0.25f) {
                        transform.forward = locAndDir.Direction;
                        //transform.rotation = Quaternion.LookRotation(locAndDir.Direction);
                        //transform.rotation = Quaternion.LookRotation(LastDirection.normalized);
                        //UnityEngine.Debug.Log("Set rotation to: " + transform.rotation);
                    }*/
                    lastDirection = locAndDir.Direction;
                    lastDirTimestamp = Time.time;
                    lastLocTimestamp = Time.time;
                    Vector3 loc = locAndDir.Location;
                    if (AtavismMobNode.useMoveMobNodeForPathInterpolator)
                    {
                        Vector3 diff = loc - transform.position;
                        if (movementState != MOVEMENT_STATE_SWIMMING && movementState != MOVEMENT_STATE_FLYING)
                             diff.y = 0;
                            //desiredDisplacement = diff * timeDifference;
                            if (diff.magnitude > 0.1f)
                            diff = diff.normalized;
                        desiredDisplacement = diff * (interpolateSpeed * timeDifference);
                     //     UnityEngine.Debug.LogError("pathInterpolator " +name+" displacement: " + desiredDisplacement + " with loc: " + loc + " and current position: " + transform.position + " and speed: " + interpolateSpeed+" | "+(interpolateSpeed * timeDifference));
                      //  UnityEngine.Debug.Log("pathInterpolator " + name + " displacement: " + desiredDisplacement + " with loc: " + loc + " and current position: " + transform.position + " and speed: " + interpolateSpeed+ " diff="+ diff+ " timeDifference="+ timeDifference);
                        if (desiredDisplacement.normalized != Vector3.zero)
                        {
                            //transform.forward = locAndDir.Direction;
                          //  transform.rotation = Quaternion.LookRotation(desiredDisplacement);
                          
                          RayCastUpdateTimer += Time.deltaTime;

                          Quaternion qTarget = Quaternion.LookRotation(desiredDisplacement, Vector3.up);
                          if (inCombat || interpolateSpeed > 2)
                          {
                             // transform.rotation = qTarget;
                          }
                          else
                          {
                              if (alignSurface) // option not all mobs may want to follow surface alignment.
                              {
                                  if (RayCastUpdateTimer >= RayCastUpdateSeconds)
                                  {
                                      RaycastHit HitDown;
                                      if (Physics.Raycast(new Vector3(transform.position.x, transform.position.y + 0.25f, transform.position.z), -transform.up, out HitDown, 2, AlignmentLayerMask))
                                      {
                                          if (HitDown.transform != this.transform)
                                          {
                                              Quaternion slopeRotation = Quaternion.FromToRotation(transform.up, HitDown.normal);
                                              transform.rotation = Quaternion.Slerp(transform.rotation, slopeRotation * qTarget, Time.deltaTime * 5f);
                                          }
                                      }
                                  }
                              }
                              else
                              {
                                  // Smooth rotate mob instead of surface alignment.
                                  //transform.rotation = Quaternion.Lerp(transform.rotation, qTarget, Time.deltaTime * 2f); 
                                  AtavismMobNode mobNode = (AtavismMobNode)AtavismClient.Instance.WorldManager.GetObjectNode(oid);
                                  mobNode.SetOrientation(qTarget);
                              }
                          }
                          
                          
                        }
                    }
                    else
                    {
                        desiredDisplacement = Vector3.zero;
                    }

                 //   UnityEngine.Debug.Log("MobNode.ComputePosition: oid " + oid + " desiredDisplacement=" + desiredDisplacement);


                }
                else
                {
                    // This interpolator has expired, so get rid of it
                    pathInterpolator = null;
                    lastDirection = Vector3.zero;
                    desiredDisplacement = Vector3.zero;
                    //UnityEngine.Debug.Log("Path interpolator for mob: " + oid + " has expired");
                }
            }
            else
            {
                // Quite likely another player since there is no PathInterpolator
                lastLocTimestamp = Time.time;
                Vector3 offset = Vector3.zero;

                if (syncOffset != Vector3.zero && lastDirection != Vector3.zero )
                {
                    float frameMaxOffset = ClientAPI.Instance.movingSyncFrameMultiply * Time.deltaTime;
                    offset = syncOffset; 
                    if (offset.magnitude > frameMaxOffset)
                    {
                        offset = offset.normalized * frameMaxOffset;
                        syncOffset = syncOffset - offset;
                    }
                    else
                    {
                        syncOffset = Vector3.zero;
                    }
                } else if (syncOffset != Vector3.zero && syncOffset.magnitude > ClientAPI.Instance.staticSyncPositionDiffTolerance)
                {
                    float frameMaxOffset = ClientAPI.Instance.staticSyncFrameMultiply * Time.deltaTime;
                    offset = syncOffset; 
                    if (offset.magnitude > frameMaxOffset)
                    {
                        offset = offset.normalized * frameMaxOffset;
                        syncOffset = syncOffset - offset;
                    }
                    else
                    {
                        syncOffset = Vector3.zero;
                    }
                }
               
                //Debug.Log("Got sync offset: " + syncOffset + " giving offset: " + offset);


                if (mount != null)
                {
                    Vector3 pos = mount.transform.position + (Time.deltaTime * lastDirection ) + offset;
                    desiredDisplacement =pos - mount.transform.position;
                }
                else
                {
                    Vector3 pos = transform.position + (Time.deltaTime * lastDirection ) + offset;
                    desiredDisplacement = pos - transform.position;
                }

                // Only apply rotatingDirection if the player is not moving
                if (rotatingDirection != 0 && desiredDisplacement.x == 0 && desiredDisplacement.z == 0)
                {
                    transform.Rotate(Vector3.up, rotatingDirection * UnityEngine.Time.deltaTime);
                }
              //  Debug.LogError("no pathInterpolator "+name+"   desiredDisplacement="+ desiredDisplacement+"    "+((Time.deltaTime * lastDirection/* * 0.95f*/) + offset)+ "  "+ offset+"  lastDirection =" + lastDirection+"   dir lenth="+ lastDirection.magnitude);

            }

            // Added to allow players see other players swimming.
            if (transform.position.y < waterHeight)
            {
                if (movementState != MOVEMENT_STATE_SWIMMING)
                {
                    if (IsJumping())
                        jumping = false;
                    movementState = MOVEMENT_STATE_SWIMMING;
                    followTerrain = false;
                }
            }
          /*  else if (waterRegions.Count == 0 && movementState == MOVEMENT_STATE_SWIMMING)
            {
                if (transform.position.y > waterHeight && movementState != MOVEMENT_STATE_FLYING)
                {
                    movementState = MOVEMENT_STATE_WALKING;
                    followTerrain = true;
                }
            }*/

            // Apply gravity
            // - extra power jump modifies gravity
            // - controlledDescent mode modifies gravity
            //if (!grounded) {
            if (transform.position.y > waterHeight && movementState != MOVEMENT_STATE_FLYING && movementState != MOVEMENT_STATE_SWIMMING)
            {
                ApplyGravity();
            }
            else
            {
                verticalSpeed = 0.0f;
            }
             // Apply jumping logic
            ApplyJumping();
            CheckGroundDistance();
         /*   if (groundDistance == maxGroundDistance && enableReverseGravityAfterExceedingMaxDistance)
            {
                if (movementState == MOVEMENT_STATE_WALKING)
                {
                    verticalSpeed = 4f;
                }
            }*/
            // Calculate actual motion
            // Multiply inAirVelocity by delta time as we don't multiply the whole movement
            inAirVelocity *= Time.deltaTime;
            //if (verticalSpeed > 0)
            //	UnityEngine.Debug.Log("Vertical speed: " + verticalSpeed);
            Vector3 movement = desiredDisplacement + new Vector3(0, verticalSpeed * Time.deltaTime, 0) + inAirVelocity;
            //movement *= Time.deltaTime;
         //  Debug.LogWarning("Mob  move "+oid+" movement=" + movement.ToString("F8")+ " verticalSpeed="+ verticalSpeed+ " inAirVelocity="+ inAirVelocity);
            return movement;
        }
        
        public override Vector3 MovePlayer()
        {
            /*if (Input.GetButtonDown ("Jump")) {
                lastJumpButtonTime = Time.time;
                AtavismClient.Instance.WorldManager.SendJumpStarted ();
            }*/
            AtavismInputController inputManager = ClientAPI.GetInputController();
            if (inputManager == null)
                return Vector3.zero;
            Vector3 direction = inputManager.GetPlayerMovement();
            if (noMove)
            {
                if (skillMove)
                {
                    direction = ClientAPI.GetPlayerObject().Orientation * new Vector3(0f, 0f, 1f);
                } else if (dodgeMove)
                {
                    direction = dodgeOrientation; 
                    transform.rotation = Quaternion.LookRotation(dodgeOrientation, Vector3.up);
                }
                else
                    direction = ClientAPI.GetPlayerObject().Orientation * Vector3.zero;
            }

            // state check
            if (transform.position.y < waterHeight)
            {
                if (movementState != MOVEMENT_STATE_SWIMMING)
                {
                    // start swimming and send state update to server
                    movementState = MOVEMENT_STATE_SWIMMING;
                    if (isPlayer)
                    {
                        SendMovementState();
                    }
                }
                followTerrain = false;
            }
            else if (waterRegions.Count == 0 && movementState == MOVEMENT_STATE_SWIMMING)
            {
                if (transform.position.y > waterHeight && mstate == MOVEMENT_STATE_WALKING)
                {
                    movementState = MOVEMENT_STATE_WALKING;
                    followTerrain = true;
                }
                else
                    followTerrain = false;
                if (isPlayer)
                {
                    //   SendMovementState();
                }
            }
            else if (movementState == MOVEMENT_STATE_FLYING)
            {
                followTerrain = false;
            }
            else if (movementState == MOVEMENT_STATE_WALKING)
            {
                followTerrain = true;
            }

            if (transform.position.y < (waterHeight - 1))
            {
                if (!underWater)
                {
                    underWater = true;
                }
            }
            else if (underWater)
            {
                underWater = false;
            }

            if (slideLimit == 0)
            {
                if (GetComponent<CharacterController>() != null)
                {
                    slideLimit = GetComponent<CharacterController>().slopeLimit - 0.1f;
                }
            }
            //   Debug.LogError("MovePlayer movementState:" + movementState + " followTerrain:" + followTerrain + " underWater:" + underWater);

            bool sliding = false;
            if (ApplyGravity())
            {
                RaycastHit hit;
                if (controller == null)
                    controller = GetComponent<CharacterController>();
                float rayDistance = controller.height * .5f + controller.radius;
                // See if surface immediately below should be slid down. We use this normally rather than a ControllerColliderHit point,
                // because that interferes with step climbing amongst other annoyances
                if (Physics.Raycast(transform.position, -Vector3.up, out hit, rayDistance))
                {
                    if (Vector3.Angle(hit.normal, Vector3.up) > slideLimit)
                        sliding = true;
                }
                // However, just raycasting straight down from the center can fail when on steep slopes
                // So if the above raycast didn't catch anything, raycast down from the stored ControllerColliderHit point instead
                else
                {
                    Physics.Raycast(contactPoint + Vector3.up, -Vector3.up, out hit);
                    if (Vector3.Angle(hit.normal, Vector3.up) > slideLimit)
                        sliding = true;
                }

                // If sliding (and it's allowed), or if we're on an object tagged "Slide", get a vector pointing down the slope we're on
                if ((sliding && slideWhenOverSlopeLimit))
                { // || (slideOnTaggedObjects && hit.collider.tag == "Slide")) {
                    Vector3 hitNormal = hit.normal;
                    direction = new Vector3(hitNormal.x, -hitNormal.y, hitNormal.z);
                    Vector3.OrthoNormalize(ref hitNormal, ref direction);
                    direction *= gravity;
                    //playerControl = false;
                }
              
            }

            // Apply gravity if not swimming or flying
            //if (followTerrain) {
            // - extra power jump modifies gravity
            // - controlledDescent mode modifies gravity

            // Apply jumping logic
            if (!sliding)
            {
                ApplyJumping();
            }
        
            CheckGroundDistance();
           /* if (groundDistance == maxGroundDistance && enableReverseGravityAfterExceedingMaxDistance)
            {
                if (movementState == MOVEMENT_STATE_WALKING)
                {
                    verticalSpeed = 4f;
                }
            }*/
            // Calculate actual motion
            direction.Normalize();
            float speed = runSpeed;
            if (walk || movingBack)
            {
                //speed = walkSpeed;
                speed = runSpeed * 0.5f;
            }

            if (skillMove)
                speed = skillMoveSpeed;
            
            if (dodgeMove)
                speed = dodgeMoveSpeed;


            //Vector3 displacement = (transform.rotation * direction) * speed;
            Vector3 displacement = (direction * speed);
            if (inCombat && !noMove)
                displacement *= combatSpeed;
            Vector3 movement = displacement + new Vector3(0, verticalSpeed, 0) + inAirVelocity;

            //test normalization player speed
       /*     movement = movement.normalized * speed;
            if (inCombat && !noMove)
                movement *= combatSpeed;
            Debug.LogWarning(movement+" "+ movement.normalized * speed);
            */

            movement *= Time.deltaTime;

            // Update player direction - Used for MMO
            if (mount != null)
            {
                AtavismClient.Instance.WorldManager.Player.SetDirection(displacement, mount.transform.position, Time.time);
                AtavismClient.Instance.WorldManager.Player.Orientation = mount.transform.rotation;
            }
            else
            {
                AtavismClient.Instance.WorldManager.Player.SetDirection(displacement, transform.position, Time.time);
                AtavismClient.Instance.WorldManager.Player.Orientation = transform.rotation;
            }

            return movement;
        }

        private long dodgeCoordId = -1L;
        public void DodgeMove(string data)
        {
            string[] d = data.Split("_");
            float s = float.Parse(d[0]); 
            long  ceid = long.Parse(d[1]); 
                
            DateTime now = DateTime.Now;
            
            //  UnityEngine.Debug.Log("SendPacket: " + now2 + " - packet " + packet+ " \nstate="+ state);
         //   Debug.LogError("DodgeMove "+now.ToString("MM/dd/yyyy hh:mm:ss.fff tt") + " " + data);
            if (s > 0)
            {
                this.dodgeMove = true;
                this.dodgeMoveSpeed = s;
                dodgeCoordId = ceid;

                Vector3 movement = ClientAPI.GetInputController().GetPlayerMovement();

                if (movement != Vector3.zero)
                {
                    dodgeOrientation = movement;
                }
                else
                {
                    if (!AtavismSettings.Instance.GetKeySettings().dodgeDoubleTap)
                    {
                        if (mount != null)
                        {
                            Vector3 eulerAngle = mount.transform.eulerAngles;
                            dodgeOrientation = Quaternion.Euler(0f,eulerAngle.y,0f) * Vector3.forward;
                        }
                        else
                        {
                            Vector3 eulerAngle = transform.eulerAngles;
                            dodgeOrientation = Quaternion.Euler(0f,eulerAngle.y,0f) * Vector3.forward;
                        }
                    }

                    //If movement is zero and player attempts to dodge then dodge forward by default
                    //Use Camera rotation instead so when the player is standing still it will always dodge forward away from the camera look direction.
                    if (AtavismSettings.Instance.GetGeneralSettings().dodgeDirection == DodgeDirectionEnum.CameraForward)
                    {
                        Vector3 eulerAngle = Camera.main.transform.eulerAngles;
                        dodgeOrientation = Quaternion.Euler(0f, eulerAngle.y, 0f) * Vector3.forward;
                    }
                    if (AtavismSettings.Instance.GetGeneralSettings().dodgeDirection == DodgeDirectionEnum.CameraBackward)
                    {
                        Vector3 eulerAngle = Camera.main.transform.eulerAngles;
                        dodgeOrientation = Quaternion.Euler(0f, eulerAngle.y, 0f) * Vector3.back;
                    }
                }
            }
            else if(ceid == dodgeCoordId)
            {
                this.dodgeMove = false;
                this.dodgeMoveSpeed = 0;
            }
        }
        void SendMovementState()
        {
            /*Dictionary<string, object> props = new Dictionary<string, object> ();
            props.Add("movement_state", movementState);
            NetworkAPI.SendExtensionMessage (ClientAPI.GetPlayerOid (), false, "ao.MOVEMENT_STATE", props);*/
        }

        #region Movement
        public void StartJump()
        {
            lastJumpButtonTime = Time.time;
            AtavismClient.Instance.WorldManager.SendJumpStarted();
        }

        public void ApplyJumping()
        {
            // Prevent jumping too fast after each other
            if (lastJumpTime + jumpRepeatTime > Time.time)
            {
                return;
            }

            if ((IsGrounded() && !noMove) || (!followTerrain && !noMove))
            {
                // Jump
                // - Only when pressing the button down
                // - With a timeout so you can press the button slightly before landing		
                if (canJump && Time.time < lastJumpButtonTime + jumpTimeout)
                {
                    jumpingReachedApex = false;
                    AtavismLogger.LogDebugMessage("Applying jump for mob: " + name);
                    verticalSpeed = CalculateJumpVerticalSpeed(jumpHeight);
                    if (movementState == MOVEMENT_STATE_WALKING)
                    {
                        SendMessage("DidJump", SendMessageOptions.DontRequireReceiver);
                    }

                    jumping = true;

                    if (mount != null)
                    {
                        mount.GetComponent<AtavismMount>().SetJumpingReachedApex(false);
                        mount.GetComponent<AtavismMount>().SetJumping(true);
                    }

                    lastJumpTime = Time.time;
                }
            }
        }

        /// <summary>
        /// Applies gravity to the player or mob. Returns whether or not the controller is grounded.
        /// </summary>
        /// <returns><c>true</c>, if gravity was applyed, <c>false</c> otherwise.</returns>
        public bool ApplyGravity()
        {
            if (!isControllable)
            {
                return true;
            }

            // When we reach the apex of the jump we send out a message
            if (jumping && !jumpingReachedApex && verticalSpeed <= 0.0)
            {
                jumpingReachedApex = true;
                SendMessage("DidJumpReachApex", SendMessageOptions.DontRequireReceiver);
                if (mount != null)
                {
                    mount.GetComponent<AtavismMount>().SetJumpingReachedApex(true);
                }
            }

            bool grounded = false;
            if (mount != null)
            {
                grounded = mount.GetComponent<AtavismMount>().IsGrounded(collisionFlags);
            }
            else
            {
                grounded = IsGrounded();
            }

            if (!followTerrain)
            {
                verticalSpeed = 0.0f;
                jumping = false;
                if (mount != null)
                {
                    mount.GetComponent<AtavismMount>().SetJumping(false);
                }
                fallStartHeight = float.MinValue;
                fallingDistance = 0;
                isFalling = false;
            }
            else if (grounded)
            {
                verticalSpeed = defaultverticalSpeed;
                if (isFalling)
                {
                    ClientAPI.WorldManager.SendFallEnded();
                }
                fallStartHeight = float.MinValue;
                fallingDistance = 0;
                isFalling = false;
            }
            else
            {
                verticalSpeed -= gravity * Time.deltaTime;
                if (fallStartHeight == float.MinValue)
                {
                    fallStartHeight = transform.position.y;
                }
                else
                {
                    fallingDistance = fallStartHeight - transform.position.y;
                    if (fallingDistance > 1 && !isFalling)
                    {
                        isFalling = true;
                        ClientAPI.WorldManager.SendFallStarted();
                    }
                }
            }

            return grounded;
        }

        public float CalculateJumpVerticalSpeed(float targetJumpHeight)
        {
            // From the jump height and gravity we deduce the upwards speed 
            // for the character to reach at the apex.
            return Mathf.Sqrt(2 * targetJumpHeight * gravity);
        }

        public void OnControllerColliderHit(ControllerColliderHit hit)
        {
            //	Debug.DrawRay(hit.point, hit.normal);
            //if (hit.moveDirection.y > 0.01) 
            //	return;
            contactPoint = hit.point;
        }

        public float GetSpeed()
        {
            return moveSpeed;
        }

        public bool IsMovingBackwards()
        {
            return movingBack;
        }

        public bool IsMoving()
        {
            return Mathf.Abs(Input.GetAxisRaw("Vertical")) + Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.5;
        }

        public bool IsJumping()
        {
            return jumping;
        }

        public bool IsGrounded()
        {
            return (collisionFlags & CollisionFlags.CollidedBelow) != 0;
        }

        public bool HasJumpReachedApex()
        {
            return jumpingReachedApex;
        }

        public bool IsGroundedWithTimeout()
        {
            return lastGroundedTime + groundedTimeout > Time.time;
        }

        bool IsAboveTerrain()
        {
            // Make sure the corpse isn't underground
            Ray ray = new Ray(transform.position, Vector3.down);
            RaycastHit hit;
            // Casts the ray and get the first game object hit
            return Physics.Raycast(ray, out hit, Mathf.Infinity, groundLayers);
        }

        #endregion Movement

        #region Other Functions
        public override void PlayMeleeAttackAnimation(string attackType, string result)
        {
        }

        public override void PlayMeleeRecoilAnimation(string result)
        {
        }

        public override void PlayAnimation(string animationName, float length, string modSpeedParam, float modSpeed)
        {
        }

        public override void WaterRegionEntered(int id)
        {
            if (!waterRegions.Contains(id))
                waterRegions.Add(id);

            followTerrain = false;
            movementState = MOVEMENT_STATE_SWIMMING;
        }

        public override void WaterRegionLeft(int id)
        {
            waterRegions.Remove(id);

            if (waterRegions.Count == 0 && transform.position.y > waterHeight)
            {
                followTerrain = true;
                movementState = MOVEMENT_STATE_WALKING;
            }
        }

        /// <summary>
        /// Called when this mob is now targeted by the player.
        /// </summary>
        public void StartTarget()
        {
            GetComponent<AtavismNode>().RegisterObjectPropertyChangeHandler("reaction", targetHendler);
            GetComponent<AtavismNode>().RegisterObjectPropertyChangeHandler("aggressive", targetHendler);
            UpdateTarget();
        }

        void UpdateTarget()
        {
            int targetType = 0;
            if (GetComponent<AtavismNode>().PropertyExists("reaction"))
            {
                targetType = (int)GetComponent<AtavismNode>().GetProperty("reaction");
            }

            if (GetComponent<AtavismNode>().PropertyExists("aggressive"))
            {
                if ((bool)GetComponent<AtavismNode>().GetProperty("aggressive"))
                {
                    targetType = -1;
                }
            }

            if (targetDecal != null)
            {
                DestroyImmediate(targetDecal);
            }
            //   float referRadius = 0.25f;
            //   float size = colliderRadius / referRadius;

            if ((GetComponent<AtavismNode>().Oid.Equals(ClientAPI.GetPlayerOid()) || targetType > 0) && friendlyTargetDecal != null)
            {
                targetDecal = (GameObject)Instantiate(friendlyTargetDecal, transform.position, transform.rotation);
                if (mount != null)
                {
                    targetDecal.transform.parent = mount.transform;
                    targetDecal.transform.localPosition = Vector3.zero;
                }
                else
                    targetDecal.transform.parent = transform;
                if (targetDecal.GetComponentInChildren<Projector>() != null)
                    targetDecal.GetComponentInChildren<Projector>().orthographicSize = colliderRadius + 0.65f;
            }
            else if (targetType == 0 && neutralTargetDecal != null)
            {
                targetDecal = (GameObject)Instantiate(neutralTargetDecal, transform.position, transform.rotation);
                if (mount != null)
                {
                    targetDecal.transform.parent = mount.transform;
                    targetDecal.transform.localPosition = Vector3.zero;
                }
                else
                    targetDecal.transform.parent = transform;
                if (targetDecal.GetComponentInChildren<Projector>() != null)
                    targetDecal.GetComponentInChildren<Projector>().orthographicSize = colliderRadius + 0.65f;
            }
            else if (enemyTargetDecal != null)
            {
                targetDecal = (GameObject)Instantiate(enemyTargetDecal, transform.position, transform.rotation);
                if (mount != null)
                {
                    targetDecal.transform.parent = mount.transform;
                    targetDecal.transform.localPosition = Vector3.zero;
                }
                else
                    targetDecal.transform.parent = transform;
                if (targetDecal.GetComponentInChildren<Projector>() != null)
                    targetDecal.GetComponentInChildren<Projector>().orthographicSize = colliderRadius + 0.65f;
            }
        }

        private void targetHendler(object sender, PropertyChangeEventArgs args)
        {
            UpdateTarget();
        }


        /// <summary>
        /// Called when this mob is no longer targeted by the player
        /// </summary>
        public void StopTarget()
        {
            GetComponent<AtavismNode>().RemoveObjectPropertyChangeHandler("reaction", targetHendler);
            GetComponent<AtavismNode>().RemoveObjectPropertyChangeHandler("aggressive", targetHendler);
            if (targetDecal != null)
            {
                Destroy(targetDecal);
                targetDecal = null;
            }
        }

        public void SetWalk(bool walk)
        {
            this.walk = walk;
        }

        #endregion Other Functions

        #region Property Handlers
        public void HandleDeadState(object sender, PropertyChangeEventArgs args)
        {
            //Debug.Log ("Got dead update: " + oid);
            dead = (bool)AtavismClient.Instance.WorldManager.GetObjectNode(oid).GetProperty("deadstate");
            if (dead)
            {
                target = -1;
            }
            //Debug.Log ("Set dead state to: " + dead);
        }

        public void HandleState(object sender, PropertyChangeEventArgs args)
        {
            //Debug.Log ("Got dead update: " + oid);
            state = (string)AtavismClient.Instance.WorldManager.GetObjectNode(oid).GetProperty("state");
            //Debug.Log ("Set dead state to: " + dead);
        }
        public void HandleNoMove(object sender, PropertyChangeEventArgs args)
        {
            //Debug.Log ("Got dead update: " + oid);
            wnomove = (bool)AtavismClient.Instance.WorldManager.GetObjectNode(oid).GetProperty("world.nomove");
            //Debug.Log ("Set dead state to: " + dead);
        }


        public void HandleCombatState(object sender, PropertyChangeEventArgs args)
        {
            inCombat = (bool)AtavismClient.Instance.WorldManager.GetObjectNode(oid).GetProperty("combatstate");
        }

        public void MovementStateHandler(object sender, PropertyChangeEventArgs args)
        {
            AtavismLogger.LogDebugMessage("Got movementstate");
            //	AtavismObjectNode node = (AtavismObjectNode)sender;
            mstate = (int)GetComponent<AtavismNode>().GetProperty(args.PropertyName);
           // Debug.LogError("movementstate state=" + state + " movementState="+ movementState);
            if (isPlayer && mstate != MOVEMENT_STATE_SWIMMING && transform.position.y < waterHeight)
            {
                movementState = MOVEMENT_STATE_SWIMMING;
            }
            else
            {
                movementState = mstate;
            }
        }

        public void HandleWaterHeight(object sender, PropertyChangeEventArgs args)
        {
            AtavismLogger.LogDebugMessage("Got waterheight");
            //	AtavismObjectNode node = (AtavismObjectNode)sender;
            waterHeight = (float)GetComponent<AtavismNode>().GetProperty(args.PropertyName);
        }

        public void HandleMount(object sender, PropertyChangeEventArgs args)
        {
            //	AtavismObjectNode node = (AtavismObjectNode)sender;
            string mount = (string)AtavismClient.Instance.WorldManager.GetObjectNode(oid).GetProperty(args.PropertyName);
            AtavismLogger.LogDebugMessage("Got mount: " + mount);
            if (mount != "")
            {
                StartMount(mount);
            }
            else
            {
                StopMount();
            }
        }

        public virtual void StartMount(string mountPrefab)
        {
            if (mount != null)
            {
                StopMount();
            }
            int resourcePathPos = mountPrefab.IndexOf("Resources/");
            mountPrefab = mountPrefab.Substring(resourcePathPos + 10);
            mountPrefab = mountPrefab.Remove(mountPrefab.Length - 7);
            GameObject prefab = (GameObject)Resources.Load(mountPrefab);
            mount = (GameObject)UnityEngine.Object.Instantiate(prefab, transform.position, transform.rotation);
            mount.layer = gameObject.layer;
            CharacterController cc = mount.GetComponent<CharacterController>();
            cc.enabled = false;
            Animator _animator  = mount.GetComponentInChildren<Animator>();
            if (_animator)
            {
                bool rootmotion = _animator.applyRootMotion;
                
                if (rootmotion)
                { 
                    _animator.applyRootMotion=false;
                    StartCoroutine(ActivateWaitRootMotionf());
                }
            }
            SetAllChildLayer(mount.transform);
            DontDestroyOnLoad(mount);
            AtavismMount mountComp = mount.GetComponent<AtavismMount>();
            if (mountComp != null)
            {
                Transform characterSocket = GetComponent<AtavismMobAppearance>().GetSocketTransform(mount.GetComponent<AtavismMount>().characterSocket);
                transform.position = mount.GetComponent<AtavismMount>().mountSocket.position;
                transform.parent = mount.GetComponent<AtavismMount>().mountSocket;
                transform.localPosition = transform.position - characterSocket.position;
                transform.localRotation = Quaternion.identity;
                //ClientAPI.GetInputController().Target = mount.transform;
                ClientAPI.GetObjectNode(oid).Parent = mount;
                mountComp.SetRider(oid);
                mountComp.mc3d = this;
                mountAnim = mountComp.riderAnimation;
            }
            foreach (AtavismMobNode mNode in ClientAPI.WorldManager.GetMobNodes())
            {
                if (mNode.GameObject != null && mNode.GameObject.GetComponent<Collider>() != null && GetComponent<Collider>() != mNode.GameObject.GetComponent<Collider>())
                    foreach (Collider nCollider in mNode.GameObject.GetComponents<Collider>())
                    {
                        foreach (Collider mCollider in mount.GetComponents<Collider>())
                            Physics.IgnoreCollision(mCollider, nCollider);
                    }
                //            Physics.IgnoreCollision (GetComponent<Collider>(), mNode.GameObject.GetComponent<Collider>());
            }
        }
        
        // <summary>
        /// Coroutine to stop player for start cast
        /// </summary>
        /// <returns></returns>
        IEnumerator ActivateWaitRootMotionf()
        {
            yield return new WaitForSeconds(1f);
            if (mount)
            {
                Animator _animator  = mount.GetComponentInChildren<Animator>();
                if (_animator)
                {
                    _animator.applyRootMotion=true;
                }
            }
        }

        void SetAllChildLayer(Transform tr)
        {
            foreach (Transform t in tr)
            {
                t.gameObject.layer = tr.gameObject.layer;
                if (t.childCount > 0)
                    SetAllChildLayer(t);
            }
        }

        public virtual void StopMount()
        {
          //  Debug.LogError("StopMount MC3d" );
            if (mount != null)
            {
                if (!mount.GetComponent<AtavismMount>().StartDismount())
                {
                    transform.parent = null;
                    ClientAPI.GetObjectNode(oid).Parent = null;
                    //ClientAPI.GetInputController().Target = transform;
                    DestroyImmediate(mount);
                }
                mount = null;
            }
        }

        public void HandleAggressive(object sender, PropertyChangeEventArgs args)
        {
            if (ClientAPI.GetTargetOid() == oid)
                StartTarget();
        }
        #endregion Property Handlers

        #region Properties
        public bool Walking
        {
            get
            {
                return walk;
            }
            set
            {
                walk = value;
            }
        }

        public float MobYaw
        {
            get
            {
                float yaw;
                yaw = transform.rotation.eulerAngles.y;
                return yaw;
            }
            set
            {
                Vector3 pitchYawRoll = transform.eulerAngles;
                pitchYawRoll.y = value;
                transform.eulerAngles = pitchYawRoll;
            }
        }
        #endregion Properties

        public void NoMove(float v)
        {
            if (noMove && v < 0.1f)
                return;
            noMoveTime = Time.time + v;
            noMove = true;

        }
        public void DsCombat(bool v)
        {
            inCombat = v;
        }
     

        void CheckGroundDistance()
        {
            if (_capsuleCollider == null)
                _capsuleCollider = GetComponent<CharacterController>();

            if (_capsuleCollider != null)
            {
                colliderHeight = _capsuleCollider.height;
                colliderRadius = _capsuleCollider.radius;
                colliderCenter = _capsuleCollider.center;

                // radius of the SphereCast
                float radius = _capsuleCollider.radius * 0.9f;
                var dist = maxGroundDistance;
                // position of the SphereCast origin starting at the base of the capsule
                Vector3 pos = transform.position + Vector3.up * (_capsuleCollider.radius);
                // ray for RayCast
                Ray ray1 = new Ray(transform.position + new Vector3(0, colliderHeight / 2, 0), Vector3.down);
                // ray for SphereCast
                Ray ray2 = new Ray(pos, -Vector3.up);
                // raycast for check the ground distance
                if (Physics.Raycast(ray1, out groundHit, maxGroundDistance, groundDistanceLayer))
                    dist = transform.position.y - groundHit.point.y;
                //   var r1 = dist;
                //  var r2 = 10f;
                // sphere cast around the base of the capsule to check the ground distance
                if (Physics.SphereCast(ray2, radius, out groundHit, 3f, groundDistanceLayer))
                {
                    // check if sphereCast distance is small than the ray cast distance
                    if (dist > (groundHit.distance - _capsuleCollider.radius * 0.1f))
                        dist = (groundHit.distance - _capsuleCollider.radius * 0.1f);
                    //  r2 = groundHit.distance - _capsuleCollider.radius * 0.1f;
                }

                groundDistance = dist;
            }
        }

        public void despawn()
        {
        }

        public void MovingBack(bool movingBack)
        {
            this.movingBack = movingBack;
        }
        
        public void SkillMove(string data)
        {
            string[] d = data.Split("_");
            float s = float.Parse(d[0]); 
            long  ceid = long.Parse(d[1]); 

            if (s > 0)
            {
                dodgeCoordId = ceid;
                this.skillMove = true;
                this.skillMoveSpeed = s;
            }
            else if( dodgeCoordId == ceid)
            {
                this.skillMove = false;
                this.skillMoveSpeed = 0;
            }
        }
        
        
        public void SkillMove(float s)
        {

            if (s > 0)
            {
                this.skillMove = true;
                this.skillMoveSpeed = s;
            }
            else
            {
                this.skillMove = false;
                this.skillMoveSpeed = 0;
            }
        }

        public Vector3 Movement
        {
            get
            {
                return movement;
            }
        }

        public float RotatingDirection
        {
            get
            {
                return rotatingDirection;
            }
        }
    }
}