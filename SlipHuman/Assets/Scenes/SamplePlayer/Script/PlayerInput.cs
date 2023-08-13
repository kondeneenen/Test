using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KinematicCharacterController;
using KinematicCharacterController.Examples;
using UnityEngine.InputSystem;
using TMPro;

namespace SamplePlayer
{
    public class PlayerInput : MonoBehaviour
    {
        public PlayerCamera OrbitCamera;
        public Transform CameraFollowPoint;
        public PlayerMove Character;
        public GameObject DebugText;

        private enum ECameraType
        {
            Rotate_RStick,
            Rotate_Mouse,
            FollowOnly,
            Num
        }

        private Gamepad _gamepad;
        private Keyboard _keyboard;
        private Mouse _mouse;
        private ECameraType _cameraType = ECameraType.Rotate_RStick;
        private PlayerMove.EOrientationMethod _orientationType = PlayerMove.EOrientationMethod.TowardsMovement;

        private TextMeshProUGUI _debugText;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;

            // カメラに注視天を設定
            OrbitCamera.SetFollowTransform(CameraFollowPoint);

            // キャラのコリジョンをカメラの地形コリジョンから除外
            OrbitCamera.IgnoredColliders.Clear();
            OrbitCamera.IgnoredColliders.AddRange(Character.GetComponentsInChildren<Collider>());

            // コントローラー取得
            _gamepad = Gamepad.current;
            _keyboard = Keyboard.current;
            _mouse = Mouse.current;

            // デバッグテキスト
            _debugText = DebugText.GetComponent<TextMeshProUGUI>();
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Cursor.lockState = CursorLockMode.Locked;
            }

            calcCharacterInput();

            calcDebugInput();
        }

        private void LateUpdate()
        {
            calcCameraInput();
        }

        private void calcCharacterInput()
        {
            // キャラへの入力
            PlayerCharacterInputs characterInputs = new PlayerCharacterInputs();

            if (_gamepad != null)
            {
                characterInputs.MoveAxisForward = _gamepad.leftStick.ReadValue().y;
                characterInputs.MoveAxisRight = _gamepad.leftStick.ReadValue().x;
                characterInputs.JumpDown = _gamepad.buttonEast.wasPressedThisFrame || _gamepad.buttonSouth.wasPressedThisFrame;
                characterInputs.ChargingDown = _gamepad.rightTrigger.wasPressedThisFrame;
            }

            if (_keyboard != null)
            {
                if (_keyboard.wKey.isPressed) characterInputs.MoveAxisForward = 1f;
                if (_keyboard.sKey.isPressed) characterInputs.MoveAxisForward = -1f;
                if (_keyboard.dKey.isPressed) characterInputs.MoveAxisRight = 1f;
                if (_keyboard.aKey.isPressed) characterInputs.MoveAxisRight = -1f;

                if (_keyboard.spaceKey.wasPressedThisFrame) characterInputs.JumpDown = true;
                if (_keyboard.qKey.wasPressedThisFrame) characterInputs.ChargingDown = true;
            }

            characterInputs.CameraRotation = OrbitCamera.transform.rotation;

            // 入力をキャラに反映
            Character.UpdateWithInput(ref characterInputs);
        }

        private void calcCameraInput()
        {
            float lookAxisUp = 0f;
            float lookAxisRight = 0f;
            float scrollInput = 0f;

            if (_gamepad != null)
            {
                lookAxisUp = _gamepad.rightStick.ReadValue().y;
                lookAxisRight = _gamepad.rightStick.ReadValue().x;
            }

            if (_mouse != null)
            {
                // マウスでカメラ操作
                if (_cameraType == ECameraType.Rotate_Mouse)
                {
                    lookAxisUp = _mouse.delta.value.y * 0.03f;
                    lookAxisRight = _mouse.delta.value.x * 0.03f;
                }

                // マウスホイールでカメラズーム
                scrollInput = _mouse.scroll.value.y;
            }

            Vector3 lookInputVector = new Vector3(lookAxisRight, lookAxisUp, 0f);

            // CursorLockMode.Locked の場合のみカメラ回転
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                lookInputVector = Vector3.zero;
            }

#if UNITY_WEBGL
            // 不具合回避のため WebGL では無効
            scrollInput = 0f;
#endif

            // 入力をカメラに反映
            bool isFollowOnly = _cameraType == ECameraType.FollowOnly;
            OrbitCamera.UpdateWithInput(Time.deltaTime, scrollInput, lookInputVector, isFollowOnly);
        }

        private void calcDebugInput()
        {
            if (_keyboard != null)
            {
                if (_keyboard.cKey.wasPressedThisFrame)
                {
                    if (++_cameraType >= ECameraType.Num) _cameraType = ECameraType.Rotate_RStick;
                }

                if (_keyboard.oKey.wasPressedThisFrame)
                {
                    if (++_orientationType >= PlayerMove.EOrientationMethod.Num) _orientationType = PlayerMove.EOrientationMethod.TowardsMovement;
                    Character.OrientationMethod = _orientationType;
                }

                if (_keyboard.fKey.wasPressedThisFrame)
                {
                    Character.Motor.ForceUnground(0.1f);
                    Character.AddVelocity(Vector3.one * 10f);
                }

                string str_CamRot = string.Empty;
                switch (_cameraType)
                {
                    case ECameraType.Rotate_RStick: str_CamRot = "RStick"; break;
                    case ECameraType.Rotate_Mouse: str_CamRot = "Mouse"; break;
                    case ECameraType.FollowOnly: str_CamRot = "None"; break;
                }

                string str_PlayerOrientation = string.Empty;
                switch(_orientationType)
                {
                    case PlayerMove.EOrientationMethod.TowardsMovement: str_PlayerOrientation = "Move"; break;
                    case PlayerMove.EOrientationMethod.TowardsCamera: str_PlayerOrientation = "Camera"; break;
                }

                _debugText.text = $"[Button]\n" +
                    $"A or B: Jump\n" +
                    $"ZR: Charging\n" +
                    $"\n" +
                    $"\n" +
                    $"[Debug Camera]\n" +
                    $"C: Rotate: {str_CamRot}\n" +
                    $"MouseScroll: Distance\n" +
                    $"\n" +
                    $"[Debug Player]\n" +
                    $"O: Orientation: {str_PlayerOrientation}\n" +
                    $"F: AddForce";
            }
        }
    }
}