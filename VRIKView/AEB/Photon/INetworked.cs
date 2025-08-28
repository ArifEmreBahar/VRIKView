using Photon.Pun;
using Photon.Realtime;
using AEB.Photon;
using UnityEngine;

namespace AEB.Photon
{
    /// <summary>
    /// Interface for networked objects with Photon.
    /// </summary>
    public interface INetworked
    {
        #region Properties

        /// <summary>
        /// Gets the PhotonView component attached to this object.
        /// </summary>
        public PhotonView PhotonView { get; set; }

        /// <summary>
        /// Who currently demands the ownership.
        /// </summary>
        public Player Demander { get; set; }

        /// <summary>
        /// Determines if ownership transfer is allowed based on the current state.
        /// </summary>
        public bool IsOwnershipTransferable { get => PhotonView.AmOwner || PhotonView.Owner == null; }

        /// <summary>
        /// Determines if ownership request is allowed based on the current state.
        /// </summary>
        public bool IsOwnershipRequestable { get => !PhotonView.AmOwner && PhotonNetwork.LocalPlayer == Demander && PhotonNetwork.Time - LastRequestTime > 3f; }

        /// <summary>
        /// Gets or sets the last time ownership was requested.
        /// </summary>
        public double LastRequestTime { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Requests ownership of the object.
        /// </summary>
        public void RequestOwnership()
        {
            if (PhotonView.Owner == null || !PhotonView.IsMine)
                PhotonView.RequestOwnership();
            LastRequestTime = PhotonNetwork.Time;
        }

        /// <summary>
        /// Transfers ownership of the object to a specified player.
        /// </summary>
        /// <param name="playerID">The ID of the player to transfer ownership to.</param>
        public void TransferOwnership(int playerID)
        {
            PhotonView.TransferOwnership(playerID);
        }

        #endregion
    }

    /// <summary>
    /// Interface for networked objects that can be moved, synchronized with Photon.
    /// </summary>
    public interface IMovableNetworked : INetworked, IPunObservable
    {
        #region Properties

        /// <summary>
        /// Gets the NetworkTransform component used for synchronizing the object's transform.
        /// </summary>
        NetworkTransform NetworkTransform { get; }

        /// <summary>
        /// Gets or sets the original parent Transform of the networked object.
        /// </summary>
        Transform OriginalParent { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Adds a reference motion to the networked object.
        /// </summary>
        /// <param name="deltaPosition">The delta position to add.</param>
        /// <param name="deltaRotation">The delta rotation to add.</param>
        public void AddReferanceMotion(Vector3 deltaPosition, Quaternion deltaRotation)
        {
            if (PhotonView.IsMine) return;

            NetworkTransform.AddReferanceMotion(deltaPosition, deltaRotation);
        }

        #endregion
    }
}
