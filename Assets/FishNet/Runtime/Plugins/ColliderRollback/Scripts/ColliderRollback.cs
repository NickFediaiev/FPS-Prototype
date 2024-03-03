﻿using FishNet.Object;
using UnityEngine;

namespace FishNet.Component.ColliderRollback
{

    public class ColliderRollback : NetworkBehaviour
    {
        #region Types.
        internal enum BoundingBoxType
        {
            /// <summary>
            /// Disable this feature.
            /// </summary>
            Disabled,
            /// <summary>
            /// Manually specify the dimensions of a bounding box.
            /// </summary>
            Manual,
        }
        //PROSTART
        internal enum FrameRollbackTypes
        {
            LerpFirst,
            LerpMiddle,
            Exact
        }
        /// <summary>
        /// Used to store where colliders are during the snapshot.
        /// </summary>
        private struct ColliderSnapshot
        {
            public ColliderSnapshot(Transform t)
            {
                WorldPosition = t.position;
                WorldRotation = t.rotation;
            }

            /// <summary>
            /// WorldPosition of transform during snapshot.
            /// </summary>
            public Vector3 WorldPosition;
            /// <summary>
            /// WorldRotation of transform during snapshot.
            /// </summary>
            public Quaternion WorldRotation;

            public void UpdateValues(Transform t)
            {
                WorldPosition = t.position;
                WorldRotation = t.rotation;
            }
        }

        /// <summary>
        /// Used to store where colliders start before rollbacks.
        /// </summary>
        internal class RollingCollider
        {
            public RollingCollider(Transform t, ColliderRollback colliderRollback)
            {
                Transform = t;
                LocalPosition = t.localPosition;
                LocalRotation = t.localRotation;
            }

            /// <summary>
            /// Received when ReturnForward is called on ColliderRollback.
            /// </summary>
            public void Return()
            {
                Transform.localPosition = LocalPosition;
                Transform.localRotation = LocalRotation;
            }

            /// <summary>
            /// Received when Rollback is called on ColliderRollback.
            /// </summary>
            public void Rollback(FrameRollbackTypes rollbackType, int endFrame, float percent)
            {
                //Exact frame.
                if (rollbackType == FrameRollbackTypes.Exact)
                {
                    int index = GetSnapshotIndex(endFrame);
                    Transform.position = _snapshots[index].WorldPosition;
                    Transform.rotation = _snapshots[index].WorldRotation;
                }
                //Start frame.
                else if (rollbackType == FrameRollbackTypes.LerpFirst)
                {
                    //Lerp between actual position and the most recent snapshot.
                    int firstFrame = GetSnapshotIndex(0);
                    Transform.position = Vector3.Lerp(Transform.position, _snapshots[firstFrame].WorldPosition, percent);
                    Transform.rotation = Quaternion.Lerp(Transform.rotation, _snapshots[firstFrame].WorldRotation, percent);
                }
                //Middle frame.
                else if (rollbackType == FrameRollbackTypes.LerpMiddle)
                {
                    //Lerp between end frame and the one before it.
                    int firstFrame = GetSnapshotIndex(endFrame - 1);
                    int secondFrame = GetSnapshotIndex(endFrame);

                    Transform.position = Vector3.Lerp(_snapshots[firstFrame].WorldPosition, _snapshots[secondFrame].WorldPosition, percent);
                    Transform.rotation = Quaternion.Lerp(_snapshots[firstFrame].WorldRotation, _snapshots[secondFrame].WorldRotation, percent);
                }
            }

            #region Public.
            /// <summary>
            /// Transform collider is for.
            /// </summary>
            public readonly Transform Transform;
            /// <summary>
            /// LocalPosition of transform at start.
            /// </summary>
            public readonly Vector3 LocalPosition;
            /// <summary>
            /// LocalRotation of transform at start.
            /// </summary>
            public readonly Quaternion LocalRotation;
            #endregion

            #region Private.
            /// <summary>
            /// Current snapshots for this collider.
            /// </summary>
            private ColliderSnapshot[] _snapshots;
            /// <summary>
            /// Index to write a snapshot in.
            /// </summary>
            private int _writeIndex = 0;
            /// <summary>
            /// True if snapshots are being recycled rather than written for the first time.
            /// </summary>
            private bool _recycleSnapshots = false;
            #endregion

            /// <summary>
            /// Fills snapshots with current value.
            /// </summary>
            public void ResetSnapshots(int count)
            {
                if (count <= 0)
                {
                    Debug.LogError("Cannot reset snapshots with count less than 1.");
                    return;
                }

                _snapshots = new ColliderSnapshot[count];
                //Reset data as if new.
                _writeIndex = 0;
                _recycleSnapshots = false;
            }

            /// <summary>
            /// Adds a snapshot for this collider.
            /// </summary>
            public void AddSnapshot()
            {
                //Not yet recycling, make a new snapshot.
                if (!_recycleSnapshots)
                    _snapshots[_writeIndex] = new ColliderSnapshot(Transform);
                //Snapshot array traversed already, start recycling.
                else
                    _snapshots[_writeIndex].UpdateValues(Transform);

                _writeIndex++;
                if (_writeIndex >= _snapshots.Length)
                {
                    _writeIndex = 0;
                    _recycleSnapshots = true;
                }
            }

            /// <summary>
            /// Gets a snapshot on the specified index.
            /// </summary>
            /// <returns></returns>
            private int GetSnapshotIndex(int historyCount)
            {
                /* Since write index is increased after a write
                 * we must reduce it by 1 to get to the last
                 * write index, before removing history count. */
                int index = (_writeIndex - 1) - historyCount;
                //If negative value start taking from the back.
                if (index < 0)
                {
                    /* Cannot take from back, snapshots aren't filled yet.
                     * Instead take the oldest snapshot, which in this case
                     * would be index 0. */
                    if (!_recycleSnapshots)
                        return 0;
                    //Snapshots filled, take from back.
                    else
                        return (_snapshots.Length + index);
                }
                //Not a negative value, return as is.
                else
                {
                    return index;
                }
            }
        }
        //PROEND
        #endregion

        #region Serialized.
        /// <summary>
        /// How to configure the bounding box check.
        /// </summary>
        [Tooltip("How to configure the bounding box check.")]
        [SerializeField]
        private BoundingBoxType _boundingBox = BoundingBoxType.Disabled;
        /// <summary>
        /// Physics type to generate a bounding box for.
        /// </summary>
        [Tooltip("Physics type to generate a bounding box for.")]
        [SerializeField]
        private RollbackPhysicsType _physicsType = RollbackPhysicsType.Physics;
        /// <summary>
        /// Size for the bounding box. This is only used when BoundingBox is set to Manual.
        /// </summary>
        [Tooltip("Size for the bounding box.. This is only used when BoundingBox is set to Manual.")]
        [SerializeField]
        private Vector3 _boundingBoxSize = new Vector3(3f, 3f, 3f);
        /// <summary>
        /// Objects holding colliders which can rollback.
        /// </summary>
        [Tooltip("Objects holding colliders which can rollback.")]
        [SerializeField]
        private GameObject[] _colliderParents = new GameObject[0];
        #endregion

        //PROSTART
        #region Private.
        /// <summary>
        /// Rollback data about ColliderParents.
        /// </summary>
        private RollingCollider[] _rollingColliders = new RollingCollider[0];
        /// <summary>
        /// True if rolled back.
        /// </summary>
        private bool _rolledBack = false;
        /// <summary>
        /// Maximum snapshots allowed. Generated at runtime using snapshot interval and max rollback time.
        /// </summary>
        private int _maxSnapshots = 0;
        /// <summary>
        /// True if initialized.
        /// </summary>
        private bool _initialized = false;
        /// <summary>
        /// Becomes true once bounding box is made.
        /// </summary>
        private bool _boundingBoxCreated;
        #endregion

        public override void OnStartServer()
        {
            base.OnStartServer();
            CreateBoundingBox();
            ChangeEventSubscriptions(true);
            Initialize();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            ChangeEventSubscriptions(false);
        }

        /// <summary>
        /// Creates a bounding box collider around this object.
        /// </summary>
        private void CreateBoundingBox()
        {
            if (_boundingBoxCreated)
                return;
            //If here then mark created as true.
            _boundingBoxCreated = true;

            if (_boundingBox == BoundingBoxType.Disabled)
                return;

            int? layer = base.RollbackManager.BoundingBoxLayerNumber;
            if (layer == null)
                return;

            //Make go to add bounds to.
            GameObject go = new GameObject("Rollback Bounding Box");
            go.transform.SetParent(transform);
            go.transform.SetPositionAndRotation(transform.position, transform.rotation);

            if (_boundingBox == BoundingBoxType.Manual)
            {
                go.layer = layer.Value;
                /* Flags check isn't working, not sure why yet. Maybe because enum is part
                 * of a different class? */
                if (_physicsType == RollbackPhysicsType.Physics)
                    go.AddComponent<BoxCollider>();
                else if (_physicsType == RollbackPhysicsType.Physics2D)
                    go.AddComponent<BoxCollider2D>();
                go.transform.localScale = _boundingBoxSize;
            }
        }

        /// <summary>
        /// Subscribes or unsubscribes to events needed for rolling back.
        /// </summary>
        /// <param name="subscribe"></param>
        private void ChangeEventSubscriptions(bool subscribe)
        {
            RollbackManager rm = base.RollbackManager;
            if (rm == null)
                return;

            if (subscribe)
                rm.RegisterColliderRollback(this);
            else
                rm.UnregisterColliderRollback(this);
        }

        /// <summary>
        /// Called when a snapshot should be created.
        /// </summary>
        internal void CreateSnapshot()
        {
            //Can't generate a snapshot while rolled back.
            if (_rolledBack)
                return;

            for (int i = 0; i < _rollingColliders.Length; i++)
            {
                if (_rollingColliders[i] == null)
                    continue;
                _rollingColliders[i].AddSnapshot();
            }
        }


        /// <summary>
        /// Called when a rollback should occur.
        /// </summary>
        /// 
        internal void Rollback(float time)
        {
            //Already rolled back.
            if (_rolledBack)
            {
                base.NetworkManager.LogWarning("Colliders are already rolled back. Returning colliders forward first.");
                Return();
            }

            FrameRollbackTypes rollbackType;
            int endFrame;
            float percent;

            float decimalFrame = (time / (float)base.TimeManager.TickDelta);
            //Out of frames.
            if (decimalFrame >= (_maxSnapshots - 1))
            {
                rollbackType = FrameRollbackTypes.LerpMiddle;
                endFrame = (_maxSnapshots - 1);
                percent = 1f;
            }
            //Within frames.
            else
            {
                percent = (decimalFrame % 1);

                /* Rolling back at least 2 frames.
                 * If only rolling back one frame decimalFrame
                 * would be less than 1, since index of 0 would be
                 * the first frame. */
                if (decimalFrame > 1f)
                {
                    rollbackType = FrameRollbackTypes.LerpMiddle;
                    endFrame = Mathf.CeilToInt(decimalFrame);
                }
                //Not rolling back more than 1 frame.
                else
                {
                    endFrame = 0;
                    rollbackType = FrameRollbackTypes.LerpFirst;
                }
            }

            int count = _rollingColliders.Length;
            for (int i = 0; i < count; i++)
                _rollingColliders[i].Rollback(rollbackType, endFrame, percent);

            _rolledBack = true;
        }

        /// <summary>
        /// Called when a specific collider should return.
        /// </summary>
        internal void Return()
        {
            if (!_rolledBack)
                return;

            int count = _rollingColliders.Length;
            for (int i = 0; i < count; i++)
                _rollingColliders[i].Return();

            _rolledBack = false;
        }

        /// <summary>
        /// Creates rolling collider values.
        /// </summary>
        private void Initialize()
        {
            //Not going to make an event for this since it only occurs OnEnable.
            for (int i = 0; i < _rollingColliders.Length; i++)
                _rollingColliders[i].ResetSnapshots(_maxSnapshots);

            if (_initialized)
                return;

            _maxSnapshots = Mathf.CeilToInt(base.RollbackManager.MaximumRollbackTime / (float)base.TimeManager.TickDelta);
            if (_maxSnapshots < 2)
                _maxSnapshots = 2;
            _rollingColliders = new RollingCollider[_colliderParents.Length];

            /* Generate a rolling collider for each
             * collider parent. */
            for (int i = 0; i < _colliderParents.Length; i++)
            {
                if (_colliderParents[i].gameObject == null)
                    continue;

                /* Creates a new rolling collider and fills the snapshots with it's current
                 * position. If you were to roll back before all snapshots could fill
                 * with new data an incorrect rollback position/rotation would be returned
                 * but the chances of this happening are slim to none, and impossible after
                 * the MAX_ROLLBACK_TIME duration has passed. */
                _rollingColliders[i] = new RollingCollider(_colliderParents[i].transform, this);
                _rollingColliders[i].ResetSnapshots(_maxSnapshots);
            }

            _initialized = true;
        }

        //PROEND
    }

}
