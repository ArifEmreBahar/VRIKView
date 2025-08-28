using Photon.Pun;
using UnityEngine;

namespace AEB.Photon
{
    /// <summary>
    /// Represents a unit for synchronizing transforms across the network.
    /// </summary>
    [System.Serializable]
    public class SyncUnit
    {
        public SyncUnit(Transform origin, Transform target = null, bool useProxyTarget = false)
        {
            _origin = origin;
            _target = target != null ? target : origin;
            _useProxyTarget = useProxyTarget;

            _networkTransform = new NetworkTransform();
        }

        #region Fields

        [SerializeField] bool _useProxyTarget;
        [SerializeField] Transform _origin;
        [SerializeField] Transform _target;
        [SerializeField] NetworkTransform _networkTransform;

        #endregion

        #region Properties

         /// <summary>
        /// Gets whether this SyncUnit uses a proxy target instead of the origin.
        /// </summary>
        public bool UseProxyTarget => _useProxyTarget;

        /// <summary>
        /// Gets the origin transform used as the source for synchronization.
        /// </summary>
        public Transform Origin => _origin;

        /// <summary>
        /// Gets or sets the target transform that will be updated with synchronized data.
        /// </summary>
        public Transform Target { get => _target; set => _target = value; }

        /// <summary>
        /// Gets the underlying NetworkTransform that handles synchronization logic.
        /// </summary>
        public NetworkTransform NetworkTransform => _networkTransform;

        #endregion

        #region Public

		/// <summary>
        /// Updates the network state using the current values of the origin transform.
        /// </summary>
        public void UpdateLocal()
        {
            if (_origin == null) return;
            _networkTransform.UpdateLocal(_origin);
        }

        /// <summary>
        /// Updates the target transform with the most recently received network state.
        /// </summary>
        public void UpdateRemote()
        {
            if (_target == null) return;
            _networkTransform.UpdateRemote(_target);
        }

        /// <summary>
        /// Sends the current synchronization data into the Photon stream.
        /// </summary>
        public void Send(PhotonStream stream)
        {
            _networkTransform.Send(stream);
        }

        /// <summary>
        /// Receives synchronization data from the Photon stream.
        /// </summary>
        public void Receive(PhotonStream stream)
        {
            _networkTransform.Receive(stream);
        }

        /// <summary>
        /// Applies reference motion deltas to the networked object.
        /// </summary>
        public void AddReferanceMotion(Vector3 deltaPosition, Quaternion deltaRotation)
        {
            _networkTransform.AddReferanceMotion(deltaPosition, deltaRotation);
        }

        #endregion
    }
}
