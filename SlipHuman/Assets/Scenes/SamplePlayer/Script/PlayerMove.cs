using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KinematicCharacterController;
using UnityEngine.Assertions;

namespace SamplePlayer
{

    public struct PlayerCharacterInputs
    {
        public float MoveAxisForward;
        public float MoveAxisRight;
        public Quaternion CameraRotation;
        public bool JumpDown;
        public bool ChargingDown;
    }

    public class PlayerMove : MonoBehaviour, ICharacterController
    {
        public enum ECharacterState
        {
            Default,
            Charging,
            Num
        }

        public enum EOrientationMethod
        {
            TowardsMovement,
            TowardsCamera,
            Num
        }

        public KinematicCharacterMotor Motor;

        [Header("Stable Movement")]
        public float MaxStableMoveSpeed = 10f;
        public float StableMovementSharpness = 15;
        public float OrientationSharpness = 10;
        public EOrientationMethod OrientationMethod = EOrientationMethod.TowardsMovement;

        [Header("Air Movement")]
        public float MaxAirMoveSpeed = 10f;
        public float AirAccelerationSpeed = 5f;
        public float Drag = 0.1f;

        [Header("Jumping")]
        public bool AllowJumpingWhenSliding = false; public float JumpSpeed = 10f;
        public float JumpPreGroundingGraceTime = 0f;
        public float JumpPostGroundingGraceTime = 0f;

        [Header("Charging")]
        public float ChargeSpeed = 15f;
        public float MaxChargeTime = 1.5f;
        public float StoppedTime = 1f;

        [Header("Misc")]
        public List<Collider> IgnoredColliders = new List<Collider>();
        public Vector3 Gravity = new Vector3(0, -30f, 0);
        public Transform MeshRoot;

        public ECharacterState CurrentCharacterState { get { return (ECharacterState)_state.CurState; } }
        public ECharacterState PrevCharacterState { get { return (ECharacterState)_state.PrevState; } }

        private Vector3 _moveInputVector;
        private Vector3 _lookInputVector;
        private bool _jumpRequested = false;
        private bool _jumpConsumed = false;
        private bool _jumpedThisFrame = false;
        private float _timeSinceJumpRequested = Mathf.Infinity;
        private float _timeSinceLastAbleToJump = 0f;
        private Vector3 _currentChargeVelocity;
        private bool _isStopped;
        private bool _requestStopVelocity = false;
        private float _timeSinceStopped = 0f;
        private Vector3 _internalVelocityAdd = Vector3.zero;

        private float _deltaTime;
        private Util.StateObserverEx _state = new Util.StateObserverEx((int)ECharacterState.Num);

        //--------------------------------------------------------------------------------
        // public method
        //--------------------------------------------------------------------------------

        public void Start()
        {
            Assert.IsNotNull(MeshRoot);

            Motor.CharacterController = this;

            _state.RegisterState(stateInitDefault, stateDefault, (int)ECharacterState.Default);
            _state.RegisterState(stateInitCharging, stateCharging, (int)ECharacterState.Charging);

            _state.RequestChangeState((int)ECharacterState.Default);
        }

        /// <summary>
        /// PlayerInput によって毎フレーム呼び出される
        /// 入力をキャラクターに通知
        /// </summary>
        public void UpdateWithInput(ref PlayerCharacterInputs inputs)
        {
            if (inputs.ChargingDown)
            {
                _state.RequestChangeState((int)ECharacterState.Charging);
            }

            // クランプ
            Vector3 moveInputVector = Vector3.ClampMagnitude(new Vector3(inputs.MoveAxisRight, 0f, inputs.MoveAxisForward), 1f);

            // キャラクター平面上のカメラの方向と回転を計算
            Vector3 cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.forward, Motor.CharacterUp).normalized;
            if (cameraPlanarDirection.sqrMagnitude == 0f)
            {
                cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.up, Motor.CharacterUp).normalized;
            }
            Quaternion cameraPlanarRotation = Quaternion.LookRotation(cameraPlanarDirection, Motor.CharacterUp);

            // 入力による移動量
            _moveInputVector = cameraPlanarRotation * moveInputVector;

            // 入力による進行方向
            switch (OrientationMethod)
            {
                case EOrientationMethod.TowardsMovement:
                    _lookInputVector = _moveInputVector.normalized;
                    break;
                case EOrientationMethod.TowardsCamera:
                    _lookInputVector = cameraPlanarDirection;
                    break;
            }

            // ジャンプ入力
            if (inputs.JumpDown)
            {
                _timeSinceJumpRequested = 0f;
                _jumpRequested = true;
            }
        }

        /// <summary>
        /// 外力を加える
        /// </summary>
        public void AddVelocity(Vector3 velocity)
        {
            _internalVelocityAdd += velocity;
        }

        //--------------------------------------------------------------------------------
        // system method (Called by KinematicCharacterMotor)
        //--------------------------------------------------------------------------------

        /// <summary>
        /// KinematicCharacterMotor.UpdatePhase1 によって毎フレーム呼び出される（FixedUpdate）
        /// </summary>
        public void BeforeCharacterUpdate(float deltaTime)
        {
            _deltaTime = deltaTime;
            _state.ExecuteState();
        }

        /// <summary>
        /// KinematicCharacterMotor.UpdatePhase1 によって毎フレーム呼び出される（FixedUpdate）
        /// コリジョンが有効か
        /// </summary>
        public bool IsColliderValidForCollisions(Collider coll)
        {
            if (IgnoredColliders.Count == 0)
            {
                return true;
            }

            if (IgnoredColliders.Contains(coll))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 地面コリジョン計算前に KinematicCharacterMotor.UpdatePhase1 によって呼び出される（FixedUpdate）
        /// </summary>
        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
        {
        }

        /// <summary>
        /// 地面コリジョン計算時に KinematicCharacterMotor.UpdatePhase1 によって呼び出される（FixedUpdate）
        /// </summary>
        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
        }

        /// <summary>
        /// 地面コリジョン計算後に KinematicCharacterMotor.UpdatePhase1 によって呼び出される（FixedUpdate）
        /// </summary>
        public void PostGroundingUpdate(float deltaTime)
        {
            // 接地トリガ
            if (Motor.GroundingStatus.IsStableOnGround && !Motor.LastGroundingStatus.IsStableOnGround)
            {
                OnLanded();
            }
            else if (!Motor.GroundingStatus.IsStableOnGround && Motor.LastGroundingStatus.IsStableOnGround)
            {
                OnLeaveStableGround();
            }
        }

        /// <summary>
        /// 地面コリジョン計算前に KinematicCharacterMotor.UpdatePhase2 によって毎フレーム呼び出される（FixedUpdate）
        /// キャラクターの回転計算（ここでのみ回転の更新を行う）
        /// </summary>
        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case ECharacterState.Default:
                    stateDefault_UpdateRotation(ref currentRotation, deltaTime);
                    break;
            }
        }

        /// <summary>
        /// 地面コリジョン計算前に KinematicCharacterMotor.UpdatePhase2 によって毎フレーム呼び出される（FixedUpdate）
        /// キャラクターの速度計算（ここでのみ速度の更新を行う）
        /// </summary>
        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case ECharacterState.Default:
                    stateDefault_UpdateVelocity(ref currentVelocity, deltaTime);
                    break;
                case ECharacterState.Charging:
                    stateCharging_UpdateVelocity(ref currentVelocity, deltaTime);
                    break;
            }

            // 外力
            if (_internalVelocityAdd.sqrMagnitude > 0f)
            {
                currentVelocity += _internalVelocityAdd;
                _internalVelocityAdd = Vector3.zero;
            }
        }

        /// <summary>
        /// オブジェクトとの接触時に  KinematicCharacterMotor.UpdatePhase2 によって呼び出される（FixedUpdate）
        /// </summary>
        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
            Debug.Log("OnMovementHit");

            switch (CurrentCharacterState)
            {
                case ECharacterState.Default:
                    break;
                case ECharacterState.Charging:
                    // 衝突によるチャージ停止
                    if (!_isStopped && !hitStabilityReport.IsStable && Vector3.Dot(-hitNormal, _currentChargeVelocity.normalized) > 0.5f)
                    {
                        _requestStopVelocity = true;
                        _isStopped = true;
                    }
                    break;
            }
        }

        /// <summary>
        /// KinematicCharacterMotor.UpdatePhase2 によって毎フレーム呼び出される（FixedUpdate）
        /// </summary>
        public void OnDiscreteCollisionDetected(Collider hitCollider)
        {
            Debug.Log("OnDiscreteCollisionDetected");
        }

        /// <summary>
        /// KinematicCharacterMotor.UpdatePhase2 によって毎フレーム呼び出される（FixedUpdate）
        /// キャラクターの座標更新後に呼び出される
        /// </summary>
        public void AfterCharacterUpdate(float deltaTime)
        {
        }

        //--------------------------------------------------------------------------------
        // private method
        //--------------------------------------------------------------------------------

        private void stateDefault_UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            if (_lookInputVector != Vector3.zero && OrientationSharpness > 0f)
            {
                // 現在の視線方向から目標の視線方向に補間
                Vector3 smoothedLookInputDirection = Vector3.Slerp(Motor.CharacterForward, _lookInputVector, 1 - Mathf.Exp(-OrientationSharpness * deltaTime)).normalized;

                // 現在の回転を設定（KinematicCharacterMotor によって使用される）
                currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Motor.CharacterUp);
            }
        }

        private void stateDefault_UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            Vector3 targetMovementVelocity = Vector3.zero;

            if (Motor.GroundingStatus.IsStableOnGround)
            {
                // 斜面では速度の向きを変更
                currentVelocity = Motor.GetDirectionTangentToSurface(currentVelocity, Motor.GroundingStatus.GroundNormal) * currentVelocity.magnitude;

                // 目標速度を算出
                Vector3 inputRight = Vector3.Cross(_moveInputVector, Motor.CharacterUp);
                Vector3 reorientedInput = Vector3.Cross(Motor.GroundingStatus.GroundNormal, inputRight).normalized * _moveInputVector.magnitude;
                targetMovementVelocity = reorientedInput * MaxStableMoveSpeed;

                // 速度を補間
                currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 1f - Mathf.Exp(-StableMovementSharpness * deltaTime));
            }
            else
            {
                // 空中移動
                if (_moveInputVector.sqrMagnitude > 0f)
                {
                    targetMovementVelocity = _moveInputVector * MaxAirMoveSpeed;

                    // 空中移動では不安定な斜面を上らないようにする
                    if (Motor.GroundingStatus.FoundAnyGround)
                    {
                        Vector3 perpenticularObstructionNormal = Vector3.Cross(Vector3.Cross(Motor.CharacterUp, Motor.GroundingStatus.GroundNormal), Motor.CharacterUp).normalized;
                        targetMovementVelocity = Vector3.ProjectOnPlane(targetMovementVelocity, perpenticularObstructionNormal);
                    }

                    Vector3 velocityDiff = Vector3.ProjectOnPlane(targetMovementVelocity - currentVelocity, Gravity);
                    currentVelocity += velocityDiff * AirAccelerationSpeed * deltaTime;
                }

                // 重力
                currentVelocity += Gravity * deltaTime;

                // 空気抵抗
                currentVelocity *= (1f / (1f + (Drag * deltaTime)));
            }

            // ジャンプ
            _jumpedThisFrame = false;
            _timeSinceJumpRequested += deltaTime;
            if (_jumpRequested)
            {
                // ジャンプ可能か
                if (!_jumpConsumed && ((AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround) || _timeSinceLastAbleToJump <= JumpPostGroundingGraceTime))
                {
                    // 接地解除前にジャンプ方向を算出
                    Vector3 jumpDirection = Motor.CharacterUp;
                    if (Motor.GroundingStatus.FoundAnyGround && !Motor.GroundingStatus.IsStableOnGround)
                    {
                        jumpDirection = Motor.GroundingStatus.GroundNormal;
                    }

                    // 次回更新時にキャラクターの接地計算をスキップ
                    // さもないと、ジャンプしても接地したままになってしまう
                    Motor.ForceUnground();

                    currentVelocity += (jumpDirection * JumpSpeed) - Vector3.Project(currentVelocity, Motor.CharacterUp);

                    // ジャンプ状態をリセット
                    _jumpRequested = false;
                    _jumpConsumed = true;
                    _jumpedThisFrame = true;
                }
            }
        }

        private void stateCharging_UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            // 停止リクエストを処理
            if (_requestStopVelocity)
            {
                currentVelocity = Vector3.zero;
                _requestStopVelocity = false;
            }

            if (_isStopped)
            {
                // 停止時は重力のみ考慮
                currentVelocity += Gravity * deltaTime;
            }
            else
            {
                // チャージ時は一定速度
                float previousY = currentVelocity.y;
                currentVelocity = _currentChargeVelocity;
                currentVelocity.y = previousY;
                currentVelocity += Gravity * deltaTime;
            }
        }

        private void stateInitDefault()
        {

        }

        private void stateDefault()
        {
            // ジャンプ関連パラメータ
            {
                // ジャンプ前の接地猶予期間
                if (_jumpRequested && _timeSinceJumpRequested > JumpPreGroundingGraceTime)
                {
                    _jumpRequested = false;
                }

                if (AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround)
                {
                    // 接地しているならリセット
                    if (!_jumpedThisFrame)
                    {
                        _jumpConsumed = false;
                    }
                    _timeSinceLastAbleToJump = 0f;
                }
                else
                {
                    // 直前のジャンプからの時間（猶予期間のために）
                    _timeSinceLastAbleToJump += _deltaTime;
                }
            }
        }

        private void stateInitCharging()
        {
            _currentChargeVelocity = Motor.CharacterForward * ChargeSpeed;
            _isStopped = false;
            _timeSinceStopped = 0f;
        }

        private void stateCharging()
        {
            if (_isStopped)
            {
                _timeSinceStopped += _deltaTime;
            }

            // チャージ時間終了による停止
            if (!_isStopped && _state.StateTime > MaxChargeTime)
            {
                _requestStopVelocity = true;
                _isStopped = true;
            }

            // 停止フェーズの終了で、直前のステートに戻る
            if (_timeSinceStopped > StoppedTime)
            {
                _state.RequestChangeState((int)PrevCharacterState);
            }
        }

        protected void OnLanded()
        {
            Debug.Log("OnLanded");
        }

        protected void OnLeaveStableGround()
        {
            Debug.Log("OnLeaveStableGround");
        }
    }
}