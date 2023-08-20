using UnityEngine;
using System.Collections;

namespace RootMotion.Demos
{

    /// <summary>
    /// Third person character controller. This class is based on the ThirdPersonCharacter.cs of the Unity Exmaple Assets.
    /// </summary>
    public class CharacterThirdPerson : CharacterBase
    {

        // Is the character always rotating to face the move direction or is he strafing?
        [System.Serializable]
        public enum MoveMode
        {
            Directional,
            Strafe
        }

        // Animation state
        public struct AnimState
        {
            public Vector3 moveDirection; // the forward speed
            public bool jump; // should the character be jumping?
            public bool crouch; // should the character be crouching?
            public bool onGround; // is the character grounded
            public bool isStrafing; // should the character always rotate to face the move direction or strafe?
            public float yVelocity; // y velocity of the character
            public bool doubleJump;
            public bool isBalance;
            public float walkSpeed;
        }

        [Header("References")]
        public CharacterAnimationBase characterAnimation; // the animation controller
        public UserControlThirdPerson userControl; // user input
        public CameraController cam; // Camera controller (optional). If assigned will update the camera in LateUpdate only if character moves

        [Header("Movement")]
        public MoveMode moveMode; // Is the character always rotating to face the move direction or is he strafing?
        public bool smoothPhysics = true; // If true, will use interpolation to smooth out the fixed time step.
        public float smoothAccelerationTime = 0.2f; // The smooth acceleration of the speed of the character (using Vector3.SmoothDamp)
        public float linearAccelerationSpeed = 3f; // The linear acceleration of the speed of the character (using Vector3.MoveTowards)
        public float platformFriction = 7f;                 // the acceleration of adapting the velocities of moving platforms
        public float groundStickyEffect = 4f;               // power of 'stick to ground' effect - prevents bumping down slopes.
        public float maxVerticalVelocityOnGround = 3f;      // the maximum y velocity while the character is grounded
        public float velocityToGroundTangentWeight = 0f;    // the weight of rotating character velocity vector to the ground tangent
        public float slipVelocityRatio = 20f;
        public float angularDrag_Default = 13f;
        public float angularDrag_Walk = 10f;

        [Header("Rotation")]
        public bool lookInCameraDirection; // should the character be looking in the same direction that the camera is facing
        public float turnSpeed = 5f;                    // additional turn speed added when the player is moving (added to animation root rotation)
        public float turnSpeedX = 20f;
        public float stationaryTurnSpeedMlp = 1f;           // additional turn speed added when the player is stationary (added to animation root rotation)

        [Header("Jumping and Falling")]
        public bool smoothJump = true; // If true, adds jump force over a few fixed time steps, not in a single step
        public float airSpeed = 6f; // determines the max speed of the character while airborne
        public float airControl = 2f; // determines the response speed of controlling the character while airborne
        public float jumpPower = 12f; // determines the jump force applied when jumping (and therefore the jump height)
        public float jumpRepeatDelayTime = 0f;          // amount of time that must elapse between landing and being able to jump again
        public bool doubleJumpEnabled;
        public float doubleJumpPowerMlp = 1f;

        [Header("Wall Running")]

        public LayerMask wallRunLayers; // walkable vertical surfaces
        public float wallRunMaxLength = 1f;                 // max duration of a wallrun
        public float wallRunMinMoveMag = 0.6f;              // the minumum magnitude of the user control input move vector
        public float wallRunMinVelocityY = -1f;             // the minimum vertical velocity of doing a wall run
        public float wallRunRotationSpeed = 1.5f;           // the speed of rotating the character to the wall normal
        public float wallRunMaxRotationAngle = 70f;         // max angle of character rotation
        public float wallRunWeightSpeed = 5f;               // the speed of blending in/out the wall running effect

        [Header("Crouching")]
        public float crouchCapsuleScaleMlp = 0.6f;          // the capsule collider scale multiplier while crouching

        [Header("Constraint")]
        public Transform constraint_HandL;
        public Transform constraint_HandR;
        public Transform transform_LArmUpper1;
        public Transform transform_RArmUpper1;

        /// <summary>
        /// Enable this while playing an animation that should be driven 100% by root motion, such as climbing walls
        /// </summary>
        public bool fullRootMotion { get; set; }

        public bool onGround { get; private set; }
        public AnimState animState = new AnimState();

        protected Vector3 moveDirection; // The current move direction of the character in Strafe move mode
        private Animator animator;
        private Vector3 normal, platformVelocity, platformAngularVelocity;
        private RaycastHit hit;
        private float jumpLeg, jumpEndTime, forwardMlp, groundDistance, lastAirTime, stickyForce;
        private Vector3 wallNormal = Vector3.up;
        private Vector3 moveDirectionVelocity;
        private float wallRunWeight;
        private float lastWallRunWeight;
        private float fixedDeltaTime;
        private Vector3 fixedDeltaPosition;
        private Quaternion fixedDeltaRotation = Quaternion.identity;
        private bool fixedFrame;
        private float wallRunEndTime;
        private Vector3 gravity;
        private Vector3 verticalVelocity;
        private float velocityY;
        private bool doubleJumped;
        private bool jumpReleased;

        private bool isGround_Ice;
        private Vector3 initPos;
        private Quaternion initQuat;
        private float offset_Body;
        private FinalIK.FullBodyBipedIK ik;
        private EffectorOffset effectorOffset;
        private Vector3 pos_HandL;
        private Vector3 pos_HandR;
        private float angle_HandL;
        private float angle_HandR;
        private float weight;

        // Use this for initialization
        protected override void Start()
        {
            base.Start();

            animator = GetComponent<Animator>();
            if (animator == null) animator = characterAnimation.GetComponent<Animator>();

            wallNormal = -gravity.normalized;
            onGround = true;
            animState.onGround = true;

            if (cam != null) cam.enabled = false;

            initPos = transform.position;
            initQuat = transform.rotation;

            ik = characterAnimation.GetComponent<FinalIK.FullBodyBipedIK>();
            effectorOffset = GetComponent<EffectorOffset>();
        }

        void OnAnimatorMove()
        {
            Move(animator.deltaPosition, animator.deltaRotation);
        }

        // When the Animator moves
        public override void Move(Vector3 deltaPosition, Quaternion deltaRotation)
        {
            // Accumulate delta position, update in FixedUpdate to maintain consitency
            fixedDeltaTime += Time.deltaTime;
            fixedDeltaPosition += deltaPosition;
            fixedDeltaRotation *= deltaRotation;
        }

        void FixedUpdate()
        {
            gravity = fullRootMotion ? Vector3.zero : GetGravity();

            verticalVelocity = V3Tools.ExtractVertical(r.velocity, gravity, 1f);
            velocityY = verticalVelocity.magnitude;
            if (Vector3.Dot(verticalVelocity, gravity) > 0f) velocityY = -velocityY;

            // Smoothing out the fixed time step
            r.interpolation = smoothPhysics ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            characterAnimation.smoothFollow = smoothPhysics;

            // Move
            MoveFixed(fixedDeltaPosition);

            fixedDeltaTime = 0f;
            fixedDeltaPosition = Vector3.zero;

            r.MoveRotation(transform.rotation * fixedDeltaRotation);
            fixedDeltaRotation = Quaternion.identity;

            bool isStop = userControl.state.move == Vector3.zero;
            r.angularDrag = isStop ? angularDrag_Default : angularDrag_Walk;

            Rotate();

            GroundCheck(); // detect and stick to ground

            // Friction
            bool isHighFriction = isStop && groundDistance < airborneThreshold * 0.5f && !isGround_Ice;
            if (isHighFriction) HighFriction();
            else ZeroFriction();

            bool stopSlide = !fullRootMotion && onGround && isStop && r.velocity.magnitude < 0.5f && groundDistance < airborneThreshold * 0.5f && !isGround_Ice;

            // Individual gravity
            if (gravityTarget != null)
            {
                r.useGravity = false;

                if (!stopSlide) r.AddForce(gravity);
            }

            if (stopSlide)
            {
                r.useGravity = false;
                r.velocity = Vector3.zero;
            }
            else if (gravityTarget == null) r.useGravity = true;

            if (onGround)
            {
                // Jumping
                animState.jump = Jump();
                jumpReleased = false;
                doubleJumped = false;
            }
            else
            {
                if (!userControl.state.jump) jumpReleased = true;

                //r.AddForce(gravity * gravityMultiplier);
                if (jumpReleased && userControl.state.jump && !doubleJumped && doubleJumpEnabled)
                {
                    jumpEndTime = Time.time + 0.1f;
                    animState.doubleJump = true;

                    Vector3 jumpVelocity = userControl.state.move * airSpeed;
                    r.velocity = jumpVelocity;
                    r.velocity += transform.up * jumpPower * doubleJumpPowerMlp;
                    doubleJumped = true;
                }
            }

            // Scale the capsule colllider while crouching
            ScaleCapsule(userControl.state.crouch ? crouchCapsuleScaleMlp : 1f);

            fixedFrame = true;
        }

        protected virtual void Update()
        {
            // Fill in animState
            animState.onGround = onGround;
            animState.moveDirection = GetMoveDirection();
            animState.yVelocity = Mathf.Lerp(animState.yVelocity, velocityY, Time.deltaTime * 10f);
            animState.crouch = userControl.state.crouch;
            animState.isStrafing = moveMode == MoveMode.Strafe;
            animState.isBalance = userControl.state.balance != 0f;

            //
            // 前傾・後傾から戻る入力 input_Balance の反映
            //

            float input_Balance = userControl.state.balance;
            float waldSpeed_Target = 0f;
            float offset_Body_Target = 0f;
            float angleVelocity = 0f;
            float weight_Target = 0f;

            if (input_Balance == 0f)
            {
                waldSpeed_Target = 0f;
                offset_Body_Target = 0f;
                angleVelocity = 0f;
                weight_Target = 0f;
            }
            else if (input_Balance < 0f)
            {
                waldSpeed_Target = input_Balance * 1.2f;
                offset_Body_Target = -0.1f;
                angleVelocity = Mathf.Lerp(Time.deltaTime * 400f, Time.deltaTime * 1200f, Mathf.Abs(input_Balance));
                weight_Target = 1f;
            }
            else
            {
                waldSpeed_Target = 0f;
                offset_Body_Target = 0.1f;
                angleVelocity = Mathf.Lerp(-Time.deltaTime * 400f, -Time.deltaTime * 1200f, Mathf.Abs(input_Balance));
                weight_Target = 1f;
            }

            // 歩行アニメーション速度
            // 前傾から戻ろうとするときのみ再生
            animState.walkSpeed = Lerp(animState.walkSpeed, waldSpeed_Target, 0.3f, Time.deltaTime);

            // Body と Shoulder の前後オフセット
            // 前傾から戻る場合にへっぴり腰にし、後傾から戻る場合に腰を突き出す
            // 前傾から戻る場合に肩を前にし、後傾から戻る場合に肩を後ろにする
            offset_Body = Lerp(offset_Body, offset_Body_Target, 0.2f, Time.deltaTime);
            effectorOffset.bodyOffset = new Vector3(0f, 0f, offset_Body);
            effectorOffset.leftShoulderOffset = new Vector3(0f, 0f, -offset_Body * 0.5f);
            effectorOffset.rightShoulderOffset = new Vector3(0f, 0f, -offset_Body * 0.5f);

            // 両手のコンストレイントを設定することで、腕を回す
            float angleX = Mathf.Lerp(10f, 20f, Mathf.Abs(input_Balance));

            Vector3 axis_HandR = transform.right;
            axis_HandR = Quaternion.AngleAxis(-10f, transform.forward) * axis_HandR;
            pos_HandR = Quaternion.AngleAxis(angleX, transform.forward) * axis_HandR * 0.6f;
            angle_HandR += angleVelocity;
            pos_HandR = Quaternion.AngleAxis(angle_HandR, axis_HandR) * pos_HandR;
            constraint_HandR.position = transform_RArmUpper1.position + pos_HandR;

            Vector3 axis_HandL = -transform.right;
            axis_HandL = Quaternion.AngleAxis(10f, transform.forward) * axis_HandL;
            pos_HandL = Quaternion.AngleAxis(-angleX, transform.forward) * axis_HandL * 0.6f;
            angle_HandL += angleVelocity;
            pos_HandL = Quaternion.AngleAxis(-angle_HandL, axis_HandL) * pos_HandL;
            constraint_HandL.position = transform_LArmUpper1.position + pos_HandL;

            // バランス入力が無い場合はデフォルトアニメに補間する
            weight = Lerp(weight, weight_Target, 0.2f, Time.deltaTime);
            ik.solver.leftHandEffector.positionWeight = weight;
            ik.solver.rightHandEffector.positionWeight = weight;

            Debug.DrawLine(transform_RArmUpper1.position, transform_RArmUpper1.position + axis_HandR, Color.red);
            Debug.DrawLine(transform_RArmUpper1.position, transform_RArmUpper1.position + pos_HandR, Color.yellow);
            Debug.DrawLine(transform_LArmUpper1.position, transform_LArmUpper1.position + axis_HandL, Color.red);
            Debug.DrawLine(transform_LArmUpper1.position, transform_LArmUpper1.position + pos_HandL, Color.yellow);

            if (userControl.state.isReset)
            {
                transform.SetPositionAndRotation(initPos, initQuat);
            }
        }

        protected virtual void LateUpdate()
        {
            if (cam == null) return;

            cam.UpdateInput();

            if (!fixedFrame && r.interpolation == RigidbodyInterpolation.None) return;

            // Update camera only if character moves
            cam.UpdateTransform(r.interpolation == RigidbodyInterpolation.None ? Time.fixedDeltaTime : Time.deltaTime);

            fixedFrame = false;
        }

        private void MoveFixed(Vector3 deltaPosition)
        {
            // Process horizontal wall-running
            WallRun();

            Vector3 velocity = fixedDeltaTime > 0f ? deltaPosition / fixedDeltaTime : Vector3.zero;

            // Add velocity of the rigidbody the character is standing on
            if (!fullRootMotion)
            {
                velocity += V3Tools.ExtractHorizontal(platformVelocity, gravity, 1f);

                if (onGround)
                {
                    // Rotate velocity to ground tangent
                    if (velocityToGroundTangentWeight > 0f)
                    {
                        Quaternion rotation = Quaternion.FromToRotation(transform.up, normal);
                        velocity = Quaternion.Lerp(Quaternion.identity, rotation, velocityToGroundTangentWeight) * velocity;
                    }
                }
                else
                {
                    // Air move
                    //Vector3 airMove = new Vector3 (userControl.state.move.x * airSpeed, 0f, userControl.state.move.z * airSpeed);
                    Vector3 airMove = V3Tools.ExtractHorizontal(userControl.state.move * airSpeed, gravity, 1f);
                    velocity = Vector3.Lerp(r.velocity, airMove, Time.deltaTime * airControl);
                }

                if (onGround && Time.time > jumpEndTime)
                {
                    r.velocity = r.velocity - transform.up * stickyForce * Time.deltaTime;
                }

                // Vertical velocity
                Vector3 verticalVelocity = V3Tools.ExtractVertical(r.velocity, gravity, 1f);
                Vector3 horizontalVelocity = V3Tools.ExtractHorizontal(velocity, gravity, 1f);
                Vector3 slipVelocity = Vector3.zero;

                // 滑る地面では重力による斜面方向速度が発生（滑らない地面では摩擦力として釣り合っている）
                if (isGround_Ice)
                {
                    slipVelocity = V3Tools.ExtractHorizontal(gravity * Time.deltaTime, normal, slipVelocityRatio);
                    r.constraints = RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
                }
                else
                {
                    r.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
                }

                if (onGround)
                {
                    if (Vector3.Dot(verticalVelocity, gravity) < 0f)
                    {
                        verticalVelocity = Vector3.ClampMagnitude(verticalVelocity, maxVerticalVelocityOnGround);
                    }
                }

                r.velocity = horizontalVelocity + verticalVelocity + slipVelocity;
            }
            else
            {
                r.velocity = velocity;
            }

            // Dampering forward speed on the slopes (Not working since Unity 2017.2)
            //float slopeDamper = !onGround? 1f: GetSlopeDamper(-deltaPosition / Time.deltaTime, normal);
            //forwardMlp = Mathf.Lerp(forwardMlp, slopeDamper, Time.deltaTime * 5f);
            forwardMlp = 1f;
        }

        // Processing horizontal wall running
        private void WallRun()
        {
            bool canWallRun = CanWallRun();

            // Remove flickering in and out of wall-running
            if (wallRunWeight > 0f && !canWallRun) wallRunEndTime = Time.time;
            if (Time.time < wallRunEndTime + 0.5f) canWallRun = false;

            wallRunWeight = Mathf.MoveTowards(wallRunWeight, (canWallRun ? 1f : 0f), Time.deltaTime * wallRunWeightSpeed);

            if (wallRunWeight <= 0f)
            {
                // Reset
                if (lastWallRunWeight > 0f)
                {
                    Vector3 frw = V3Tools.ExtractHorizontal(transform.forward, gravity, 1f);
                    transform.rotation = Quaternion.LookRotation(frw, -gravity);
                    wallNormal = -gravity.normalized;
                }
            }

            lastWallRunWeight = wallRunWeight;

            if (wallRunWeight <= 0f) return;

            // Make sure the character won't fall down
            if (onGround && velocityY < 0f) r.velocity = V3Tools.ExtractHorizontal(r.velocity, gravity, 1f);

            // transform.forward flattened
            Vector3 f = V3Tools.ExtractHorizontal(transform.forward, gravity, 1f);

            // Raycasting to find a walkable wall
            RaycastHit velocityHit = new RaycastHit();
            velocityHit.normal = -gravity.normalized;
            Physics.Raycast(onGround ? transform.position : capsule.bounds.center, f, out velocityHit, 3f, wallRunLayers);

            // Finding the normal to rotate to
            wallNormal = Vector3.Lerp(wallNormal, velocityHit.normal, Time.deltaTime * wallRunRotationSpeed);

            // Clamping wall normal to max rotation angle
            wallNormal = Vector3.RotateTowards(-gravity.normalized, wallNormal, wallRunMaxRotationAngle * Mathf.Deg2Rad, 0f);

            // Get transform.forward ortho-normalized to the wall normal
            Vector3 fW = transform.forward;
            Vector3 nW = wallNormal;
            Vector3.OrthoNormalize(ref nW, ref fW);

            // Rotate from upright to wall normal
            transform.rotation = Quaternion.Slerp(Quaternion.LookRotation(f, -gravity), Quaternion.LookRotation(fW, wallNormal), wallRunWeight);
        }

        // Should the character be enabled to do a wall run?
        private bool CanWallRun()
        {
            if (fullRootMotion) return false;
            if (Time.time < jumpEndTime - 0.1f) return false;
            if (Time.time > jumpEndTime - 0.1f + wallRunMaxLength) return false;
            if (velocityY < wallRunMinVelocityY) return false;
            if (userControl.state.move.magnitude < wallRunMinMoveMag) return false;
            return true;
        }

        // Get the move direction of the character relative to the character rotation
        private Vector3 GetMoveDirection()
        {
            switch (moveMode)
            {
                case MoveMode.Directional:
                    moveDirection = Vector3.SmoothDamp(moveDirection, new Vector3(0f, 0f, userControl.state.move.magnitude), ref moveDirectionVelocity, smoothAccelerationTime);
                    moveDirection = Vector3.MoveTowards(moveDirection, new Vector3(0f, 0f, userControl.state.move.magnitude), Time.deltaTime * linearAccelerationSpeed);
                    return moveDirection * forwardMlp;
                case MoveMode.Strafe:
                    moveDirection = Vector3.SmoothDamp(moveDirection, userControl.state.move, ref moveDirectionVelocity, smoothAccelerationTime);
                    moveDirection = Vector3.MoveTowards(moveDirection, userControl.state.move, Time.deltaTime * linearAccelerationSpeed);
                    return transform.InverseTransformDirection(moveDirection);
            }

            return Vector3.zero;
        }

        // Rotate the character
        protected virtual void Rotate()
        {
            if (gravityTarget != null) r.MoveRotation(Quaternion.FromToRotation(transform.up, transform.position - gravityTarget.position) * transform.rotation);
            if (platformAngularVelocity != Vector3.zero) r.MoveRotation(Quaternion.Euler(platformAngularVelocity) * transform.rotation);

            float angle = GetAngleFromForward(GetForwardDirection());

            if (userControl.state.move == Vector3.zero) angle *= (1.01f - (Mathf.Abs(angle) / 180f)) * stationaryTurnSpeedMlp;

            // Rotating the character
            //RigidbodyRotateAround(characterAnimation.GetPivotPoint(), transform.up, angle * Time.deltaTime * turnSpeed);
            r.MoveRotation(Quaternion.AngleAxis(angle * Time.deltaTime * turnSpeed, transform.up) * r.rotation);

            Vector3 torque = transform.right * userControl.state.balance * turnSpeedX;
            r.AddTorque(torque, ForceMode.Acceleration);
        }

        // Which way to look at?
        private Vector3 GetForwardDirection()
        {
            bool isMoving = userControl.state.move != Vector3.zero;

            switch (moveMode)
            {
                case MoveMode.Directional:
                    if (isMoving) return userControl.state.move;
                    return lookInCameraDirection ? userControl.state.lookPos - r.position : transform.forward;
                case MoveMode.Strafe:
                    if (isMoving) return userControl.state.lookPos - r.position;
                    return lookInCameraDirection ? userControl.state.lookPos - r.position : transform.forward;
            }

            return Vector3.zero;
        }

        protected virtual bool Jump()
        {
            // check whether conditions are right to allow a jump:
            if (!userControl.state.jump) return false;
            if (userControl.state.crouch) return false;
            if (!characterAnimation.animationGrounded) return false;
            if (Time.time < lastAirTime + jumpRepeatDelayTime) return false;

            // Jump
            onGround = false;
            jumpEndTime = Time.time + 0.1f;

            Vector3 jumpVelocity = userControl.state.move * airSpeed;
            jumpVelocity += transform.up * jumpPower;

            if (smoothJump)
            {
                StopAllCoroutines();
                StartCoroutine(JumpSmooth(jumpVelocity - r.velocity));
            }
            else
            {
                r.velocity = jumpVelocity;
            }

            return true;
        }

        // Add jump velocity smoothly to avoid puppets launching to space when unpinned during jump acceleration
        private IEnumerator JumpSmooth(Vector3 jumpVelocity)
        {
            int steps = 0;
            int stepsToTake = 3;
            while (steps < stepsToTake)
            {
                r.AddForce((jumpVelocity) / stepsToTake, ForceMode.VelocityChange);
                steps++;
                yield return new WaitForFixedUpdate();
            }
        }

        // Is the character grounded?
        private void GroundCheck()
        {
            Vector3 platformVelocityTarget = Vector3.zero;
            platformAngularVelocity = Vector3.zero;
            float stickyForceTarget = 0f;
            isGround_Ice = false;

            // Spherecasting
            hit = GetSpherecastHit();

            normal = hit.normal;
            groundDistance = Vector3.Project(r.position - hit.point, transform.up).magnitude;

            // if not jumping...
            //bool findGround = Time.time > jumpEndTime && velocityY < jumpPower * 0.5f;
            // 上記だと、ダッシュで斜面を駆け上がるときに velocityY < jumpPower * 0.5f が false になってしまってジャンプ判定されてしまうので除外
            bool findGround = Time.time > jumpEndTime;

            if (findGround)
            {
                bool g = onGround;
                onGround = false;

                // The distance of considering the character grounded
                float groundHeight = !g ? airborneThreshold * 0.5f : airborneThreshold;

                //Vector3 horizontalVelocity = r.velocity;
                Vector3 horizontalVelocity = V3Tools.ExtractHorizontal(r.velocity, gravity, 1f);

                float velocityF = horizontalVelocity.magnitude;

                if (groundDistance < groundHeight)
                {
                    // Force the character on the ground
                    stickyForceTarget = groundStickyEffect * velocityF * groundHeight;

                    // On moving platforms
                    if (hit.rigidbody != null)
                    {
                        platformVelocityTarget = hit.rigidbody.GetPointVelocity(hit.point);
                        platformAngularVelocity = Vector3.Project(hit.rigidbody.angularVelocity, transform.up);
                    }

                    if (hit.collider != null && hit.collider.sharedMaterial != null)
                    {
                        // hit.collider.material は使用しない
                        // さもないと、呼び出したタイミングで新たな material を生成してしまう
                        // 例えば、Ice の場合 Ice (Instance) という名前のマテリアルになってしまう
                        isGround_Ice = hit.collider.sharedMaterial.name == "Ice";
                    }

                    // Flag the character grounded
                    onGround = true;
                }
            }

            // Interpolate the additive velocity of the platform the character might be standing on
            platformVelocity = Vector3.Lerp(platformVelocity, platformVelocityTarget, Time.deltaTime * platformFriction);
            if (fullRootMotion) stickyForce = 0f;

            stickyForce = stickyForceTarget;//Mathf.Lerp(stickyForce, stickyForceTarget, Time.deltaTime * 5f);

            // remember when we were last in air, for jump delay
            if (!onGround) lastAirTime = Time.time;
        }

        public static float Lerp(float a, float b, float ratio, float deltaTime)
        {
            ratio = Mathf.Clamp(ratio, 0f, 1f);
            return Mathf.Lerp(a, b, 1f - Mathf.Pow(1f - ratio, DeltaTime2TimeRatio(deltaTime)));
        }

        public static float DeltaTime2TimeRatio(float deltaTime)
        {
            return deltaTime * cDeltaTime2TimeRatio;
        }

        static float cDeltaTime2TimeRatio = 60f;
    }
}
