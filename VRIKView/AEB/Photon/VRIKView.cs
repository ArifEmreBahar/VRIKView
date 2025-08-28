using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using RootMotion.FinalIK;
using AEB.FinalIK;

namespace AEB.Photon
{
    [System.Serializable]
    public class XRCharacter
    {
        public SyncUnit Body;
        public SyncUnit Ground;
        //public SyncUnit Head;

        public SyncUnit[] Units => new SyncUnit[] { Body, Ground };
    }

    [RequireComponent(typeof(PhotonView))]
    public class VRIKView : MonoBehaviourPun, IPunObservable
    {
        #region Fields

        /// <summary>
        /// Array of VRIK components.
        /// </summary>
        public VRIK[] VRIKs;

        /// <summary>
        /// The XRCharacter instance containing sync units.
        /// </summary>
        public XRCharacter XRCharacter;

        /// <summary>
        /// References for VRIK calibration.
        /// </summary>
        public VRIKCalibrationRefs vRIKCalibrationRefs;

        /// <summary>
        /// Calibration data for VRIK.
        /// </summary>
        public VRIKCalibrator.CalibrationData Data;

        #endregion

        #region Variables

        List<SyncUnit> _allSyncUnits = new List<SyncUnit>();
        Transform _proxyHolder;
        bool _initialized = false;
        const string PROXY_PREFIX = "Proxy-";

        #endregion

        #region Unity

        void Start()
        {
            Initiate();
        }

        void FixedUpdate()
        {
            if (!_initialized) return;

            if (photonView.IsMine)
                UpdateLocal();
            else
                UpdateRemote();
        }

        #endregion

        #region Public

        /// <summary>
        /// Adds reference motion to all VRIK solvers and SyncUnits. 
        /// </summary>
        /// <param name="deltaPosition">The change in position to be applied.</param>
        /// <param name="deltaRotation">The change in rotation to be applied.</param>
        /// <param name="referancePivot">The pivot point for the reference motion.</param>
        public void AddReferanceMotion(Vector3 deltaPosition, Quaternion deltaRotation, Vector3 referancePivot)
        {
            foreach (VRIK vRIK in VRIKs)
                vRIK.solver.AddPlatformMotion(deltaPosition, deltaRotation, referancePivot);

            if (photonView.IsMine) return;

            foreach (SyncUnit unit in _allSyncUnits)
                unit.AddReferanceMotion(deltaPosition, deltaRotation);
        }

        #endregion

        #region Private

        void Initiate()
        {
            if (_initialized) return;

            CalibrateAll();

            if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom) return;

            SetProxyHolder();
            PrepareSyncUnits();
            PrepareIKs();

            _initialized = true;
        }

        void PrepareSyncUnits()
        {
            _allSyncUnits.AddRange(XRCharacter.Units);

            if (photonView.IsMine) return;

            foreach (SyncUnit syncUnit in XRCharacter.Units)
            {
                Transform target;
                if (syncUnit.UseProxyTarget)
                    target = CreateGetProxy(syncUnit.Origin);
                else
                    target = syncUnit.Origin;

                syncUnit.Target = target;
            }
        }

        void PrepareIKs()
        {
            foreach (VRIK vRIK in VRIKs)
            {
                SyncUnit headUnit = new SyncUnit(vRIK.solver.spine.headTarget);
                SyncUnit leftArmUnit = new SyncUnit(vRIK.solver.leftArm.target);
                SyncUnit rightArmUnit = new SyncUnit(vRIK.solver.rightArm.target);

                headUnit.NetworkTransform.UseLocal = leftArmUnit.NetworkTransform.UseLocal = rightArmUnit.NetworkTransform.UseLocal = false;

                if (!photonView.IsMine)
                {
                    vRIK.solver.spine.headTarget = headUnit.Target = CreateGetProxy(vRIK.solver.spine.headTarget);
                    vRIK.solver.leftArm.target = leftArmUnit.Target = CreateGetProxy(vRIK.solver.leftArm.target); ;
                    vRIK.solver.rightArm.target = rightArmUnit.Target = CreateGetProxy(vRIK.solver.rightArm.target);
                }

                _allSyncUnits.Add(headUnit);
                _allSyncUnits.Add(leftArmUnit);
                _allSyncUnits.Add(rightArmUnit);
            }
        }

        void SetProxyHolder()
        {
            if (photonView.IsMine) return;
            if (_proxyHolder != null) return;
  
            _proxyHolder = new GameObject("ProxyHolder").transform;
            _proxyHolder.parent = transform.parent;
            _proxyHolder.localPosition = Vector3.zero;
        }

        void RefreshIKs()
        {
            // NOTE: We loop this twice because the order of VRIKs in the list is unknown.
            // This ensures that all references are refreshed properly.
            for (int i = 0; i < 2; i++)
                foreach (VRIK vRIK in VRIKs)
                {
                    vRIK.enabled = false;
                    vRIK.enabled = true;
                }
        }

        Transform CreateGetProxy(Transform target)
        { 
            Transform proxy = new GameObject().transform;
            proxy.name = "Proxy-" + target.name;
            proxy.parent = _proxyHolder;

            return proxy;
        }

        void CalibrateAll(bool fresh = false)
        {
            VRIKCalibratorHelper calibrator = new VRIKCalibratorHelperPlayerPrefs();
            VRIKCalibrator.CalibrationData data = null;
            foreach (VRIK vRIK in VRIKs)
            {
                data = calibrator.GetCalibrationData(vRIK.GetInstanceID());
                if (data != null) break;
            }

            foreach (VRIK vRIK in VRIKs)
            {
                if (fresh || data != null)
                    calibrator.Calibrate(vRIK, vRIKCalibrationRefs, data);
                else
                    calibrator.CacheCalibrationData(vRIK.GetInstanceID(), calibrator.Calibrate(vRIK, vRIKCalibrationRefs));
            }

            Data = calibrator.GetCalibrationData(VRIKs[0].GetInstanceID());

            RefreshIKs();
        }

        void UpdateRemote()
        {           
            foreach (SyncUnit unit in _allSyncUnits)
                unit.UpdateRemote();
        }

        void UpdateLocal()
        {
            foreach (SyncUnit syncBlock in _allSyncUnits)
                syncBlock.UpdateLocal();
        }

        #endregion

        #region Photon

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (!_initialized) return;

            if (stream.IsWriting)
                foreach (SyncUnit syncBlock in _allSyncUnits)
                    syncBlock.Send(stream);
            else
                foreach (SyncUnit syncBlock in _allSyncUnits)
                    syncBlock.Receive(stream);
        }

        #endregion
    }
}