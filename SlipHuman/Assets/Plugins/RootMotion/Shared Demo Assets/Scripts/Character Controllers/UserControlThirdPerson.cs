using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

namespace RootMotion.Demos
{

    /// <summary>
    /// User input for a third person character controller.
    /// </summary>
    public class UserControlThirdPerson : MonoBehaviour
    {

        // Input state
        public struct State
        {
            public Vector3 move;
            public Vector3 lookPos;
            public bool crouch;
            public bool jump;
            public int actionIndex;
        }

        public bool walkByDefault;        // toggle for walking state
        public bool canCrouch = true;
        public bool canJump = true;

        public State state = new State();           // The current state of the user input

        protected Transform cam;                    // A reference to the main camera in the scenes transform

        private Gamepad _gamepad;
        private Keyboard _keyboard;


        protected virtual void Start()
        {

            // コントローラー取得
            _gamepad = Gamepad.current;
            _keyboard = Keyboard.current;

            // get the transform of the main camera
            cam = Camera.main.transform;
        }

        protected virtual void Update()
        {

            float h = 0f;
            float v = 0f;
            bool isDash = false;
            if (_gamepad != null)
            {
                h = _gamepad.leftStick.ReadValue().x;
                v = _gamepad.leftStick.ReadValue().y;

                state.crouch = canCrouch && _gamepad.rightTrigger.wasPressedThisFrame;
                state.jump = canJump && _gamepad.buttonEast.wasPressedThisFrame || _gamepad.buttonSouth.wasPressedThisFrame;
                isDash = _gamepad.rightShoulder.isPressed;
            }

            if (_keyboard != null)
            {
                if (_keyboard.wKey.isPressed) v = 1f;
                if (_keyboard.sKey.isPressed) v = -1f;
                if (_keyboard.dKey.isPressed) h = 1f;
                if (_keyboard.aKey.isPressed) h = -1f;

                state.crouch |= canCrouch && _keyboard.cKey.wasPressedThisFrame;
                state.jump |= canJump && _keyboard.spaceKey.wasPressedThisFrame;
                isDash |= _keyboard.leftShiftKey.isPressed;
            }

            // calculate move direction
            Vector3 move = cam.rotation * new Vector3(h, 0f, v).normalized;

            // Flatten move vector to the character.up plane
            if (move != Vector3.zero)
            {
                Vector3 normal = transform.up;
                Vector3.OrthoNormalize(ref normal, ref move);
                state.move = move;
            }
            else state.move = Vector3.zero;

            bool walkToggle = isDash;

            // We select appropriate speed based on whether we're walking by default, and whether the walk/run toggle button is pressed:
            float walkMultiplier = (walkByDefault ? walkToggle ? 1 : 0.5f : walkToggle ? 0.5f : 1);

            state.move *= walkMultiplier;

            // calculate the head look target position
            state.lookPos = transform.position + cam.forward * 100f;
        }
    }
}

