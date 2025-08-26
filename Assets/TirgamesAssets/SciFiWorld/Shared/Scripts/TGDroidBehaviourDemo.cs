using UnityEngine;
using UnityEngine.UI;

namespace TGRobotsWheeled
{

    [RequireComponent(typeof(CharacterController))]
    public class TGDroidBehaviourDemo : MonoBehaviour
    {
        public float moveSpeed = 5f;
        public float rotationSpeed = 180f;
        public float acceleration = 10f;
        public float rotationAcceleration = 90f;
        public Text uiState;

        private CharacterController characterController;
        private TGDroidStateManager droidManager;
        private float currentMoveSpeed = 0f;
        private float currentRotationSpeed = 0f;

        void Start()
        {
            characterController = GetComponent<CharacterController>();
            droidManager = GetComponent<TGDroidStateManager>();
            displayState();
        }

        void Update()
        {
            HandleMovement();
            HandleStates();
        }

        void HandleMovement()
        {
            if (droidManager.State == TGDroidStateManager.TDroidState.Sleep) return;
            // Get user input
            float moveInput = Input.GetAxis("Vertical"); // W (1), S (-1)
            float rotationInput = Input.GetAxis("Horizontal"); // A (-1), D (1)

            // Handle forward/backward movement speed with acceleration
            if (moveInput != 0)
            {
                currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, moveInput * moveSpeed, acceleration * Time.deltaTime);
            }
            else
            {
                currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, 0, acceleration * Time.deltaTime);
            }

            // Handle rotation speed with acceleration
            if (rotationInput != 0)
            {
                currentRotationSpeed = Mathf.MoveTowards(currentRotationSpeed, rotationInput * rotationSpeed, rotationAcceleration * Time.deltaTime);
            }
            else
            {
                currentRotationSpeed = Mathf.MoveTowards(currentRotationSpeed, 0, rotationAcceleration * Time.deltaTime);
            }

            // Apply movement and rotation
            Vector3 moveDirection = transform.forward * currentMoveSpeed;
            characterController.SimpleMove(moveDirection);

            transform.Rotate(0, currentRotationSpeed * Time.deltaTime, 0);
        }


        void HandleStates()
        {
            // change state by pressing right mouse button
            if (Input.GetMouseButtonDown(1))
            {
                int state = (int)droidManager.State;
                state++;
                if (state >= System.Enum.GetValues(typeof(TGDroidStateManager.TDroidState)).Length)
                {
                    state = 0;
                }
                droidManager.State = (TGDroidStateManager.TDroidState)state;
                droidManager.Shooting = false;
                displayState();
            }
            // shoot/roload if state is Combat
            if (droidManager.State == TGDroidStateManager.TDroidState.Combat)
            {
                if (Input.GetKeyDown(KeyCode.R))
                {
                    droidManager.Reload = true;
                }
                droidManager.Shooting = Input.GetMouseButton(0);

            }
        }


        void displayState()
        {
            if (uiState) {
                string stateName =droidManager.State.ToString().ToUpper();
                uiState.text = stateName;
            }
        }

    }

}