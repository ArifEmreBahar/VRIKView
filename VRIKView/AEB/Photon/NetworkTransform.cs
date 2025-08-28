using Photon.Pun;
using UnityEngine;

namespace AEB.Photon
{
    /// <summary>
    /// Represents a network transform that synchronizes position, rotation, and scale over the network.
    /// </summary>
    [System.Serializable]
    public class NetworkTransform
    {
        #region Fields

        /// <summary>
        /// Indicates whether to synchronize position.
        /// </summary>
        public bool SyncPosition = true;

        /// <summary>
        /// Indicates whether to synchronize rotation.
        /// </summary>
        public bool SyncRotation = true;

        /// <summary>
        /// Indicates whether to synchronize scale.
        /// </summary>
        public bool SyncScale = false;

        /// <summary>
        /// Determines if local or world coordinates are used for synchronization.
        /// </summary>
        public bool UseLocal = true;

        Vector3 _position;
        Quaternion _rotation;
        Vector3 _scale;

        Vector3 _syncStartPosition = Vector3.zero;
        Vector3 _syncEndPosition = Vector3.zero;
        Quaternion _syncStartRotation = Quaternion.identity;
        Quaternion _syncEndRotation = Quaternion.identity;
        Vector3 _syncStartScale = Vector3.zero;
        Vector3 _syncEndScale = Vector3.zero;

        float _lastSynchronizationTime = 0f;
        float _syncDelay = 0f;
        float _syncTime = 0f;
        float _teleportDistance = 1.5f;
        bool _firstTake;

        Transform _refLocal;
        Transform _refRemote;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current position.
        /// </summary>
        public Vector3 Position => _position;

        /// <summary>
        /// Gets the current rotation.
        /// </summary>
        public Quaternion Rotation => _rotation;

        /// <summary>
        /// Gets the current scale.
        /// </summary>
        public Vector3 Scale => _scale;

        /// <summary>
        /// Indicates whether data is being received.
        /// </summary>
        /// <remarks>
        /// NOTE: This might not be the most healty way to check it, consider adding previous value check or something better.
        /// </remarks>
        public bool IsReceivingData => _syncEndPosition != null && _syncEndPosition != Vector3.zero;

        /// <summary>
        /// Gets or sets the distance threshold at which interpolation is skipped and the object teleports.
        /// </summary>
        public float TeleportDistance { get => _teleportDistance; set => _teleportDistance = value; }

        #endregion

        #region Public

        /// <summary>
        /// Sends the current transform state over the network.
        /// </summary>
        /// <param name="stream">The PhotonStream used for networking.</param>
        public void Send(PhotonStream stream)
        {
            if (stream == null) return;

            if (SyncPosition)
                stream.SendNext(_syncEndPosition = _syncStartPosition = _position);

            if (SyncRotation)
                stream.SendNext(_syncEndRotation = _syncStartRotation = _rotation);

            if (SyncScale)
                stream.SendNext(_syncEndScale = _scale);
        }

        /// <summary>
        /// Receives the transform state from the network.
        /// </summary>
        /// <param name="stream">The PhotonStream used for networking.</param>
        public void Receive(PhotonStream stream)
        {
            if (stream == null) return;

            if (SyncPosition)
            {
                _syncStartPosition = GetPosition(_refRemote);
                _syncEndPosition = (Vector3)stream.ReceiveNext();
            }
               
            if (SyncRotation)
            {
                _syncStartRotation = GetRotation(_refRemote);
                _syncEndRotation = (Quaternion)stream.ReceiveNext();
            }
                
            if (SyncScale)
                _syncEndScale = (Vector3)stream.ReceiveNext();

            _syncTime = 0f;
            _syncDelay = Mathf.Max(Time.time - _lastSynchronizationTime, 0.0001f);
            _lastSynchronizationTime = Time.time;
        }

        /// <summary>
        /// Updates the state with the local transform.
        /// </summary>
        /// <param name="transform">The Transform to update.</param>
        public void UpdateLocal(Transform transform)
        {
            if (transform == null) return;
            if (_refLocal == null || transform != _refLocal) _refLocal = transform;

            if (SyncPosition)
                _position = GetPosition(transform);
            if (SyncRotation)
                _rotation = GetRotation(transform);
            if (SyncScale)
                _scale = transform.localScale;
        }

        /// <summary>
        /// Updates the transform with the received network state.
        /// </summary>
        /// <param name="transform">The Transform to update.</param>
        public void UpdateRemote(Transform transform)
        {
            if (transform == null) return;
            if (_refRemote == null || transform != _refRemote) _refRemote = transform;
            if (_syncEndPosition == Vector3.zero && _syncEndRotation == Quaternion.identity && _syncEndScale == Vector3.zero) return;
            if (_firstTake) { ApplyReceivedDataImmediately(transform); _firstTake = false; return; }

            _syncTime += Time.fixedDeltaTime;
            float syncValue = Mathf.Clamp01(_syncTime / _syncDelay);  
             
            if (SyncPosition)
            {
                if (Vector3.Distance(_syncStartPosition, _syncEndPosition) >= _teleportDistance) syncValue = 1;
                SetPosition(transform, Vector3.Lerp(_syncStartPosition, _syncEndPosition, syncValue));
            }
            if (SyncRotation)
            {
                if (_syncStartRotation != Quaternion.identity && _syncEndRotation != Quaternion.identity)
                {
                    _syncStartRotation.Normalize();
                    _syncEndRotation.Normalize();

                    transform.rotation = Quaternion.Lerp(_syncStartRotation, _syncEndRotation, syncValue);
                }
                else
                    transform.rotation = _syncEndRotation;
            }
            if (SyncScale) transform.localScale = Vector3.Lerp(_syncStartScale, _syncEndScale, syncValue);
        }

        /// <summary>
        /// Adds reference motion to the networked object.
        /// </summary>
        /// <param name="deltaPosition">The change in position to add.</param>
        /// <param name="deltaRotation">The change in rotation to add.</param>
        public void AddReferanceMotion(Vector3 deltaPosition, Quaternion deltaRotation)
        {
            if (UseLocal) return;

            _refRemote.position += deltaPosition;

            _syncEndPosition += deltaPosition;
            _syncStartPosition += deltaPosition;

            _syncEndRotation = deltaRotation * _syncEndRotation;
            _syncStartRotation = deltaRotation * _syncStartRotation;
        }

        #region Private

        void ApplyReceivedDataImmediately(Transform transform)
        {
            if (transform == null) return;

            if (SyncPosition) SetPosition(transform, _syncEndPosition);
            if (SyncRotation) SetRotation(transform, _syncEndRotation);
            if (SyncScale) transform.localScale = _syncEndScale;
        }

        Vector3 GetPosition(Transform transform)
        {
            if (transform == null) return default;

            return UseLocal ? transform.localPosition : transform.position;
        }

        Quaternion GetRotation(Transform transform)
        {
            if (transform == null) return default;

            return UseLocal ? transform.localRotation : transform.rotation;
        }

        Vector3 SetPosition(Transform transform, Vector3 position)
        {
            if (transform == null) return default;

            if (UseLocal)
                return transform.localPosition = position;
            else
                return transform.position = position;
        }

        Quaternion SetRotation(Transform transform, Quaternion rotation)
        {
            if (transform == null) return default;

            if (UseLocal)
                return transform.localRotation = rotation;
            else
                return transform.rotation = rotation;
        }

        #endregion
    }
}
#endregion