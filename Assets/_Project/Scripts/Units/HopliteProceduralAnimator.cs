using System;
using UnityEngine;

namespace ProjectoRTS.Units
{
    /// <summary>
    /// Lightweight code-driven animation for the hoplite. The Animator is
    /// disabled and the humanoid bones are posed directly: idle, walk and a
    /// wind-up/thrust attack. The attack reports its hit frame so UnitCombat
    /// can apply damage in sync without animation clips or events.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HopliteProceduralAnimator : MonoBehaviour
    {
        [Header("Base pose")]
        [SerializeField] private float armDownAngle = 78f;
        [SerializeField] private float elbowBendAngle = 15f;
        [SerializeField] private float shieldArmDownAngle = 72f;
        [SerializeField] private float shieldElbowBendAngle = 24f;
        [SerializeField] private float shieldForwardX = 0f;
        [SerializeField] private float shieldForwardZ = 10f;

        [Header("Walk")]
        [SerializeField] private float legSwingAngle = 30f;
        [SerializeField] private float armSwingAngle = 14f;
        [SerializeField] private float bobHeight = 0.035f;
        [SerializeField] private float strideRate = 6f;

        [Header("Attack")]
        [SerializeField] private float attackDuration = 0.62f;
        [SerializeField] private float hitTime = 0.27f;
        [SerializeField] private float windupEnd = 0.18f;
        [SerializeField] private float thrustEnd = 0.34f;
        [SerializeField] private float windupArmAngle = 22f;
        [SerializeField] private float thrustArmAngle = 70f;
        [SerializeField] private float thrustElbowAngle = -5f;
        [SerializeField] private float spineLeanAngle = 10f;
        [SerializeField] private float windupArmTwist = 15f;
        [SerializeField] private float thrustArmTwist = 55f;
        [SerializeField] private float windupElbowTwist = 10f;
        [SerializeField] private float thrustElbowTwist = 20f;

        [Header("Attack variation")]
        [SerializeField] private float variationDuration = 0.06f;
        [SerializeField] private float variationHeight = 0.18f;
        [SerializeField] private float variationLateral = 0.12f;
        [SerializeField] private float lungeDistance = 0.14f;

        [Header("Grip")]
        [SerializeField] private float fingerCurl = 55f;
        [SerializeField] private float thumbCurl = 40f;

        private Animator _animator;
        private UnitMovement _movement;

        private Transform _hips, _spine, _chest;
        private Transform _leftUpperArm, _leftLowerArm, _rightUpperArm, _rightLowerArm;
        private Transform _leftUpperLeg, _leftLowerLeg, _rightUpperLeg, _rightLowerLeg;
        private Transform _rightSpear, _leftShield, _rightHand;
        private Transform[] _rightFingers;
        private Transform[] _leftFingers;
        private Quaternion[] _rightFingerRest;
        private Quaternion[] _leftFingerRest;

        private Quaternion _hipsRest, _spineRest, _chestRest;
        private Quaternion _leftUpperArmRest, _leftLowerArmRest, _rightUpperArmRest, _rightLowerArmRest;
        private Quaternion _leftUpperLegRest, _leftLowerLegRest, _rightUpperLegRest, _rightLowerLegRest;
        private Vector3 _hipsRestPosition;

        private float _walkPhase;
        private bool _attacking;
        private float _attackTimer;
        private bool _hitFired;
        private Action _onHit;
        private bool _debugHold;
        private float _currentAttackDuration;
        private float _currentHitTime;
        private float _currentWindupEnd;
        private float _currentThrustEnd;
        private float _thrustHeight;
        private float _thrustLateral;
        private float _lunge;
        private int _variant;
        private Transform _modelRoot;
        private Vector3 _modelRestPosition;

        public bool IsAttacking => _attacking;
        public int LastAttackVariant => _variant;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>(true);
            _movement = GetComponent<UnitMovement>();

            if (_animator == null)
            {
                enabled = false;
                return;
            }

            _modelRoot = _animator.transform;
            _modelRestPosition = _modelRoot.localPosition;
            _currentAttackDuration = attackDuration;
            _currentHitTime = hitTime;
            _currentWindupEnd = windupEnd;
            _currentThrustEnd = thrustEnd;
            _lunge = lungeDistance;

            _hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            _spine = _animator.GetBoneTransform(HumanBodyBones.Spine);
            _chest = _animator.GetBoneTransform(HumanBodyBones.Chest);
            _leftUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _leftLowerArm = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _rightLowerArm = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _leftUpperLeg = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            _leftLowerLeg = _animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            _rightUpperLeg = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            _rightLowerLeg = _animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);

            foreach (Transform t in _animator.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Spear") _rightSpear = t;
                if (t.name == "PeltastShield") _leftShield = t;
            }

            _rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            CacheFingers();

            if (_hips != null)
            {
                _hipsRest = _hips.localRotation;
                _hipsRestPosition = _hips.localPosition;
            }

            if (_spine != null) _spineRest = _spine.localRotation;
            if (_chest != null) _chestRest = _chest.localRotation;
            if (_leftUpperArm != null) _leftUpperArmRest = _leftUpperArm.localRotation;
            if (_leftLowerArm != null) _leftLowerArmRest = _leftLowerArm.localRotation;
            if (_rightUpperArm != null) _rightUpperArmRest = _rightUpperArm.localRotation;
            if (_rightLowerArm != null) _rightLowerArmRest = _rightLowerArm.localRotation;
            if (_leftUpperLeg != null) _leftUpperLegRest = _leftUpperLeg.localRotation;
            if (_leftLowerLeg != null) _leftLowerLegRest = _leftLowerLeg.localRotation;
            if (_rightUpperLeg != null) _rightUpperLegRest = _rightUpperLeg.localRotation;
            if (_rightLowerLeg != null) _rightLowerLegRest = _rightLowerLeg.localRotation;

            // The procedural animator owns the pose from now on.
            _animator.enabled = false;
        }

        private void LateUpdate()
        {
            if (_hips == null || _debugHold)
            {
                return;
            }

            float speed = 0f;
            float maxSpeed = 3f;

            if (_movement != null && _movement.Agent != null)
            {
                maxSpeed = Mathf.Max(0.1f, _movement.Agent.speed);

                if (_movement.Agent.enabled && _movement.Agent.isOnNavMesh)
                {
                    speed = _movement.Agent.velocity.magnitude;
                }
            }

            float speedNormalized = Mathf.Clamp01(speed / maxSpeed);
            _walkPhase += Time.deltaTime * strideRate * speedNormalized;

            if (_attacking)
            {
                _attackTimer += Time.deltaTime;

                if (!_hitFired && _attackTimer >= _currentHitTime)
                {
                    _hitFired = true;

                    if (_onHit != null)
                    {
                        _onHit();
                    }
                }

                if (_attackTimer >= _currentAttackDuration)
                {
                    _attacking = false;
                    _onHit = null;
                }
            }

            ApplyPose(speedNormalized);
        }

        private void ApplyPose(float speedNormalized)
        {
            float swing = Mathf.Sin(_walkPhase) * speedNormalized;
            float bob = Mathf.Abs(Mathf.Sin(_walkPhase * 2f)) * bobHeight * speedNormalized;

            float rightArmExtra = 0f;
            float rightElbowExtra = 0f;
            float rightArmTwist = 0f;
            float rightElbowTwist = 0f;
            float spineLean = 0f;
            float spineTwist = 0f;
            float shieldExtraZ = 0f;
            float windupWeight = 0f;
            float thrustWeight = 0f;

            if (_attacking)
            {
                float windup = Mathf.Clamp01(_attackTimer / Mathf.Max(0.01f, _currentWindupEnd));
                float thrust = Mathf.Clamp01((_attackTimer - _currentWindupEnd) / Mathf.Max(0.01f, _currentThrustEnd - _currentWindupEnd));
                float recover = Mathf.Clamp01((_attackTimer - _currentThrustEnd) / Mathf.Max(0.01f, _currentAttackDuration - _currentThrustEnd));

                windupWeight = windup * (1f - thrust);
                thrustWeight = thrust * (1f - recover);

                rightArmExtra = (-windupArmAngle * windupWeight) + (thrustArmAngle * thrustWeight);
                rightElbowExtra = (25f * windupWeight) + (thrustElbowAngle * thrustWeight);
                rightArmTwist = (windupArmTwist * windupWeight) - (thrustArmTwist * thrustWeight);
                rightElbowTwist = (windupElbowTwist * windupWeight) - (thrustElbowTwist * thrustWeight);
                spineLean = spineLeanAngle * thrustWeight;
                spineTwist = (-8f * windupWeight) + (8f * thrustWeight);
                shieldExtraZ = 15f * thrustWeight;
            }

            if (_rightUpperArm != null)
            {
                _rightUpperArm.localRotation = _rightUpperArmRest *
                    Quaternion.Euler(-armDownAngle + rightArmExtra, 0f, rightArmTwist);
            }

            if (_leftUpperArm != null)
            {
                _leftUpperArm.localRotation = _leftUpperArmRest *
                    Quaternion.Euler(-shieldArmDownAngle + shieldForwardX, 0f, shieldForwardZ + shieldExtraZ);
            }

            if (_rightLowerArm != null)
            {
                _rightLowerArm.localRotation = _rightLowerArmRest *
                    Quaternion.Euler(elbowBendAngle + rightElbowExtra, 0f, rightElbowTwist);
            }

            if (_leftLowerArm != null)
            {
                _leftLowerArm.localRotation = _leftLowerArmRest *
                    Quaternion.Euler(shieldElbowBendAngle, 0f, (shieldForwardZ * 0.5f) + shieldExtraZ);
            }

            if (_spine != null)
            {
                _spine.localRotation = _spineRest * Quaternion.Euler(spineLean, spineTwist, 0f);
            }

            if (_chest != null)
            {
                _chest.localRotation = _chestRest * Quaternion.Euler(spineLean * 0.4f, spineTwist * 0.5f, 0f);
            }

            if (_hips != null)
            {
                _hips.localPosition = _hipsRestPosition + (Vector3.up * bob);
            }

            if (_leftUpperLeg != null)
            {
                _leftUpperLeg.localRotation = _leftUpperLegRest;
            }

            if (_rightUpperLeg != null)
            {
                _rightUpperLeg.localRotation = _rightUpperLegRest;
            }

            if (_leftLowerLeg != null)
            {
                _leftLowerLeg.localRotation = _leftLowerLegRest;
            }

            if (_rightLowerLeg != null)
            {
                _rightLowerLeg.localRotation = _rightLowerLegRest;
            }

            if (_attacking && (windupWeight > 0.001f || thrustWeight > 0.001f))
            {
                AimAttackPose(windupWeight, thrustWeight);
            }
            else
            {
                AimWalkPose(swing);
            }

            ApplyGripPose();

            if (_leftShield != null)
            {
                _leftShield.rotation = Quaternion.LookRotation(-transform.forward, Vector3.up);
            }

            if (_modelRoot != null)
            {
                _modelRoot.localPosition = _modelRestPosition + (Vector3.forward * (_lunge * thrustWeight));
            }
        }

        private void AimWalkPose(float swing)
        {
            if (_rightUpperArm != null)
            {
                Vector3 direction = _rightUpperArm.TransformDirection(Vector3.up);
                Vector3 target = LocalDirection(new Vector3(0.05f, -0.75f, 0.25f));
                direction = Vector3.Slerp(direction, target, 0.8f);
                AimBone(_rightUpperArm, direction);
            }

            if (_leftUpperArm != null)
            {
                Vector3 direction = _leftUpperArm.TransformDirection(Vector3.up);
                Vector3 target = LocalDirection(new Vector3(-0.05f, -0.65f, 0.15f));
                direction = Vector3.Slerp(direction, target, 0.8f);
                AimBone(_leftUpperArm, direction);
            }

            if (_rightUpperLeg != null)
            {
                Vector3 direction = _rightUpperLeg.TransformDirection(Vector3.up);
                Vector3 target = LocalDirection(new Vector3(0.05f, -1f, (legSwingAngle / 60f) * swing));
                direction = Vector3.Slerp(direction, target, 0.8f);
                AimBone(_rightUpperLeg, direction);
            }

            if (_leftUpperLeg != null)
            {
                Vector3 direction = _leftUpperLeg.TransformDirection(Vector3.up);
                Vector3 target = LocalDirection(new Vector3(-0.05f, -1f, -((legSwingAngle / 60f) * swing)));
                direction = Vector3.Slerp(direction, target, 0.8f);
                AimBone(_leftUpperLeg, direction);
            }

            if (_leftShield != null)
            {
                _leftShield.rotation = Quaternion.LookRotation(-transform.forward, Vector3.up);
            }

            if (_rightHand != null)
            {
                Vector3 handTarget = LocalDirection(new Vector3(0.05f, 0.05f, 1f));
                AimBoneAxis(_rightHand, Vector3.forward, handTarget);
            }
        }

        private void AimAttackPose(float windupWeight, float thrustWeight)
        {
            if (_rightUpperArm != null)
            {
                Vector3 direction = _rightUpperArm.TransformDirection(Vector3.up);
                direction = Vector3.Slerp(direction, LocalDirection(new Vector3(0.15f, 0.6f, -0.35f)), windupWeight);
                direction = Vector3.Slerp(direction, LocalDirection(new Vector3(_thrustLateral * 0.5f, -0.15f + _thrustHeight, 1f)), thrustWeight);
                AimBone(_rightUpperArm, direction);
            }

            if (_rightLowerArm != null)
            {
                Vector3 direction = _rightLowerArm.TransformDirection(Vector3.up);
                direction = Vector3.Slerp(direction, LocalDirection(new Vector3(0.1f, 0.5f, 0.3f)), windupWeight);
                direction = Vector3.Slerp(direction, LocalDirection(new Vector3(_thrustLateral, -0.05f + (_thrustHeight * 0.5f), 1f)), thrustWeight);
                AimBone(_rightLowerArm, direction);
            }

            if (_rightHand != null)
            {
                Vector3 target = LocalDirection(new Vector3(_thrustLateral, _thrustHeight, 1f));
                AimBoneAxis(_rightHand, Vector3.forward, target);
            }

            if (_leftUpperArm != null && thrustWeight > 0.001f)
            {
                Vector3 direction = _leftUpperArm.TransformDirection(Vector3.up);
                direction = Vector3.Slerp(direction, LocalDirection(new Vector3(-0.2f, -0.3f, 0.7f)), thrustWeight * 0.6f);
                AimBone(_leftUpperArm, direction);
            }

            if (_leftShield != null)
            {
                _leftShield.rotation = Quaternion.LookRotation(-transform.forward, Vector3.up);
            }
        }

        private Vector3 LocalDirection(Vector3 localDirection)
        {
            return transform.TransformDirection(localDirection);
        }

        private static void AimBone(Transform bone, Vector3 targetDirection)
        {
            if (bone == null || targetDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector3 current = bone.TransformDirection(Vector3.up);
            Quaternion delta = Quaternion.FromToRotation(current, targetDirection.normalized);
            bone.rotation = delta * bone.rotation;
        }

        private static void AimBoneAxis(Transform bone, Vector3 localAxis, Vector3 targetDirection)
        {
            if (bone == null || targetDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector3 current = bone.TransformDirection(localAxis);
            Quaternion delta = Quaternion.FromToRotation(current, targetDirection.normalized);
            bone.rotation = delta * bone.rotation;
        }

        private void CacheFingers()
        {
            UnityEngine.HumanBodyBones[] rightBones = new UnityEngine.HumanBodyBones[]
            {
                UnityEngine.HumanBodyBones.RightThumbProximal,
                UnityEngine.HumanBodyBones.RightThumbIntermediate,
                UnityEngine.HumanBodyBones.RightThumbDistal,
                UnityEngine.HumanBodyBones.RightIndexProximal,
                UnityEngine.HumanBodyBones.RightIndexIntermediate,
                UnityEngine.HumanBodyBones.RightIndexDistal,
                UnityEngine.HumanBodyBones.RightMiddleProximal,
                UnityEngine.HumanBodyBones.RightMiddleIntermediate,
                UnityEngine.HumanBodyBones.RightMiddleDistal,
                UnityEngine.HumanBodyBones.RightRingProximal,
                UnityEngine.HumanBodyBones.RightRingIntermediate,
                UnityEngine.HumanBodyBones.RightRingDistal,
                UnityEngine.HumanBodyBones.RightLittleProximal,
                UnityEngine.HumanBodyBones.RightLittleIntermediate,
                UnityEngine.HumanBodyBones.RightLittleDistal
            };

            UnityEngine.HumanBodyBones[] leftBones = new UnityEngine.HumanBodyBones[]
            {
                UnityEngine.HumanBodyBones.LeftThumbProximal,
                UnityEngine.HumanBodyBones.LeftThumbIntermediate,
                UnityEngine.HumanBodyBones.LeftThumbDistal,
                UnityEngine.HumanBodyBones.LeftIndexProximal,
                UnityEngine.HumanBodyBones.LeftIndexIntermediate,
                UnityEngine.HumanBodyBones.LeftIndexDistal,
                UnityEngine.HumanBodyBones.LeftMiddleProximal,
                UnityEngine.HumanBodyBones.LeftMiddleIntermediate,
                UnityEngine.HumanBodyBones.LeftMiddleDistal,
                UnityEngine.HumanBodyBones.LeftRingProximal,
                UnityEngine.HumanBodyBones.LeftRingIntermediate,
                UnityEngine.HumanBodyBones.LeftRingDistal,
                UnityEngine.HumanBodyBones.LeftLittleProximal,
                UnityEngine.HumanBodyBones.LeftLittleIntermediate,
                UnityEngine.HumanBodyBones.LeftLittleDistal
            };

            _rightFingers = new Transform[rightBones.Length];
            _rightFingerRest = new Quaternion[rightBones.Length];
            for (int i = 0; i < rightBones.Length; i++)
            {
                _rightFingers[i] = _animator.GetBoneTransform(rightBones[i]);
                if (_rightFingers[i] != null)
                {
                    _rightFingerRest[i] = _rightFingers[i].localRotation;
                }
            }

            _leftFingers = new Transform[leftBones.Length];
            _leftFingerRest = new Quaternion[leftBones.Length];
            for (int i = 0; i < leftBones.Length; i++)
            {
                _leftFingers[i] = _animator.GetBoneTransform(leftBones[i]);
                if (_leftFingers[i] != null)
                {
                    _leftFingerRest[i] = _leftFingers[i].localRotation;
                }
            }
        }

        private void ApplyGripPose()
        {
            ApplyFingerCurl(_rightFingers, _rightFingerRest);
            ApplyFingerCurl(_leftFingers, _leftFingerRest);
        }

        private void ApplyFingerCurl(Transform[] fingers, Quaternion[] rest)
        {
            if (fingers == null || rest == null)
            {
                return;
            }

            for (int i = 0; i < fingers.Length; i++)
            {
                Transform finger = fingers[i];
                if (finger == null)
                {
                    continue;
                }

                float angle = i < 3 ? thumbCurl : fingerCurl;
                finger.localRotation = rest[i] * Quaternion.Euler(0f, 0f, -angle);
            }
        }

        /// <summary>Starts the attack and invokes onHit at the strike frame.</summary>
        public void PlayAttack(Action onHit)
        {
            PickAttackVariation();
            _attacking = true;
            _attackTimer = 0f;
            _hitFired = false;
            _onHit = onHit;
            _debugHold = false;
        }

        private void PickAttackVariation()
        {
            int variant = UnityEngine.Random.Range(0, 4);
            float durationScale = 1f;
            float height = 0f;
            float lateral = UnityEngine.Random.Range(-variationLateral, variationLateral);
            float lunge = lungeDistance;

            switch (variant)
            {
                case 0:
                    durationScale = 1f;
                    height = 0f;
                    break;
                case 1:
                    durationScale = 1.06f;
                    height = variationHeight;
                    lunge *= 1.1f;
                    break;
                case 2:
                    durationScale = 0.96f;
                    height = -variationHeight * 1.4f;
                    lunge *= 0.9f;
                    break;
                default:
                    durationScale = 0.84f;
                    height = -0.04f;
                    lateral *= 1.3f;
                    lunge *= 0.7f;
                    break;
            }

            durationScale += UnityEngine.Random.Range(-variationDuration, variationDuration);
            _currentAttackDuration = attackDuration * durationScale;
            float windupRatio = windupEnd / Mathf.Max(0.01f, attackDuration);
            float thrustRatio = thrustEnd / Mathf.Max(0.01f, attackDuration);
            _currentWindupEnd = _currentAttackDuration * windupRatio;
            _currentThrustEnd = _currentAttackDuration * thrustRatio;
            _currentHitTime = Mathf.Lerp(_currentWindupEnd, _currentThrustEnd, 0.65f);
            _thrustHeight = height + UnityEngine.Random.Range(-0.025f, 0.025f);
            _thrustLateral = lateral;
            _lunge = lunge;
            _variant = variant;
        }

        /// <summary>Testing hook: freezes the attack pose at a normalized time.</summary>
        public void DebugPoseAttack(float normalizedTime)
        {
            _attacking = true;
            _attackTimer = Mathf.Clamp01(normalizedTime) * _currentAttackDuration;
            _hitFired = true;
            _debugHold = true;
            ApplyPose(0f);
        }

        /// <summary>Testing hook: freezes the idle pose.</summary>
        public void DebugPoseIdle()
        {
            _attacking = false;
            _debugHold = true;
            ApplyPose(0f);
        }

        /// <summary>Testing hook: freezes a walk pose at a normalized phase.</summary>
        public void DebugPoseWalk(float normalizedPhase)
        {
            _walkPhase = normalizedPhase * Mathf.PI * 2f;
            _attacking = false;
            _debugHold = true;
            ApplyPose(1f);
        }
    }
}
