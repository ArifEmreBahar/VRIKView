using System.Collections.Generic;
using Sirenix.OdinInspector;
using AEB.Photon;
using UnityEngine;

namespace AEB.Interactable.Platform
{
    public class PlatformHelper : MonoBehaviour
    {
        #region Fields

        /// <summary>
        /// Reference to the MovingPlatform component.
        /// </summary>
        MovingPlatform _movingPlatform;

        /// <summary>
        /// Last recorded position of the platform.
        /// </summary>
        protected Vector3 lastPosition;

        /// <summary>
        /// Last recorded rotation of the platform.
        /// </summary>
        protected Quaternion lastRotation = Quaternion.identity;

        /// <summary>
        /// List of players currently on the platform.
        /// </summary>
        [ShowInInspector]
        protected List<Transform> objectsOnPlatform;

        /// <summary>
        /// Cache of VRIKView components for players on the platform.
        /// </summary>
        protected Dictionary<Transform, VRIKView> vRIKViewCache;

        /// <summary>
        /// Cache of NetworkedGrabbable components for interactables on the platform.
        /// </summary>
        protected Dictionary<Transform, IMovableNetworked> iMovableNetworkedCache;

        /// <summary>
        /// Cache of initial parent transforms for players on the platform.
        /// </summary>
        protected Dictionary<Transform, Transform> initialParent;

        /// <summary>
        /// Tag used to identify player objects.
        /// </summary>
        const string PLAYER_TAG = "Player";

        /// <summary>
        /// Tag used to identify interactable objects.
        /// </summary>
        const string OBJ_TAG = "Interactable";

        #endregion

        #region Unity

        protected virtual void LateUpdate()
        {
            AddPlatformMotion();
        }

        protected virtual void OnEnable()
        {
            Initialize();

            lastPosition = transform.position;
            lastRotation = transform.rotation;
        }

        protected virtual void OnTriggerEnter(Collider collider)
        {
            if (!collider.CompareTag(PLAYER_TAG) && !collider.CompareTag(OBJ_TAG)) return;

            if (!objectsOnPlatform.Contains(collider.transform))
                objectsOnPlatform.Add(collider.transform);

            if (collider.CompareTag(PLAYER_TAG))
                CachePlayer(collider.transform);
            if (collider.CompareTag(OBJ_TAG))
                CacheObject(collider.transform);

            if (CheckMovingPlateform())
                CacheSetInitialParent(collider.transform);
        }

        protected virtual void OnTriggerExit(Collider collider)
        {
            if (!collider.CompareTag(PLAYER_TAG) && !collider.CompareTag(OBJ_TAG)) return;

            if (objectsOnPlatform.Contains(collider.transform))
                objectsOnPlatform.Remove(collider.transform);

            if (collider.CompareTag(PLAYER_TAG))
                HandlePlayerExit(collider.transform);
            else if (collider.CompareTag(OBJ_TAG))
                HandleObjectExit(collider.transform);
        }

        #endregion

        #region Protected
        /// <summary>
        /// check if we have moving plateform component
        /// </summary>
        /// <returns></returns>
        protected virtual bool CheckMovingPlateform()
        {
            return _movingPlatform != null;
        }

        /// <summary>
        /// Initializes necessary fields and collections.
        /// </summary>
        protected virtual void Initialize()
        {
            if (_movingPlatform == null)
                _movingPlatform = GetComponent<MovingPlatform>();
            if (objectsOnPlatform == null)
                objectsOnPlatform = new List<Transform>();
            if (vRIKViewCache == null)
                vRIKViewCache = new Dictionary<Transform, VRIKView>();
            if (iMovableNetworkedCache == null)
                iMovableNetworkedCache = new Dictionary<Transform, IMovableNetworked>();
            if (initialParent == null)
                initialParent = new Dictionary<Transform, Transform>();
        }

        /// <summary>
        /// Handles the end of platform movement by reverting the parent of the players and easing settings.
        /// </summary>
        protected virtual void HandlePlayerExit(Transform transform)
        {
            if (transform.parent != null)
                transform.parent.parent = null;
        }

        /// <summary>
        /// Handles the end of platform movement by reverting the parent of the players and easing settings.
        /// </summary>
        protected virtual void HandleObjectExit(Transform transform)
        {
            if (iMovableNetworkedCache.TryGetValue(transform, out var networkedData))
            {
                transform.parent = networkedData.OriginalParent;
                networkedData.OriginalParent = null;
            }
        }
        #endregion

        #region Private

        /// <summary>
        /// Adds the platform's motion to the players' VRIK solvers.
        /// </summary>
        void AddPlatformMotion()
        {
            Vector3 deltaPosition = transform.position - lastPosition;
            Quaternion deltaRotation = transform.rotation * Quaternion.Inverse(lastRotation);

            foreach (Transform obj in objectsOnPlatform)
            {
                if (vRIKViewCache.ContainsKey(obj))
                    vRIKViewCache[obj].AddReferanceMotion(deltaPosition, deltaRotation, transform.position);
                else if (iMovableNetworkedCache.TryGetValue(obj, out var movableNetworked))
                    movableNetworked.AddReferanceMotion(deltaPosition, deltaRotation);
            }

            lastRotation = transform.rotation;
            lastPosition = transform.position;
        }

        /// <summary>
        /// Caches the VRIKView component of the player.
        /// </summary>
        /// <param name="player">The player's transform.</param>
        void CachePlayer(Transform player)
        {
            if (!vRIKViewCache.TryGetValue(player, out var vRIKView))
            {
                vRIKView = player.GetComponent<VRIKView>();
                vRIKViewCache.Add(player, vRIKView);
            }
        }

        /// <summary>
        /// Caches the NetworkedGrabbable component of the Object.
        /// </summary>
        /// <param name="networkObj">The NetworkedGrabbable's transform.</param>
        void CacheObject(Transform networkObj)
        {
            if (networkObj == null)
            {
                Debug.LogError("CacheObject called with a null Transform.");
                return;
            }

            IMovableNetworked iMovableNetworked;

            if (!iMovableNetworkedCache.TryGetValue(networkObj, out iMovableNetworked))
            {
                if (networkObj.TryGetComponent(out iMovableNetworked))
                    iMovableNetworkedCache.Add(networkObj, iMovableNetworked);
                else
                {
                    Debug.LogWarning($"CacheObject: {networkObj.name} does not have an IMovableNetworked component.");
                    return;
                }
            }

            iMovableNetworked.OriginalParent = this.transform;
        }

        /// <summary>
        /// Caches and sets the initial parent of the specified transform, 
        /// modifying its hierarchy based on whether it is in the vRIKViewCache.
        /// </summary>
        /// <param name="transform">The transform whose parent is being cached and set.</param>
        void CacheSetInitialParent(Transform transform)
        {
            if (vRIKViewCache.ContainsKey(transform))
            {
                transform.parent.parent = this.transform;
            }
            else
            {
                transform.parent = this.transform;
            }
        }

        #endregion
    }
}
