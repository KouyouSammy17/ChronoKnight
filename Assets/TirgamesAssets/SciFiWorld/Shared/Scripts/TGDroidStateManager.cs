using System;
using System.Collections.Generic;
using UnityEngine;

namespace TGRobotsWheeled
{

    public class TGDroidStateManager : MonoBehaviour
    {
        [System.Serializable]
        public enum TDroidState { Sleep, Idle, Alarmed, Combat }

        [System.Serializable]
        public enum TDroidShootMode { Single, Double }

        [System.Serializable]
        public class TDroidStateParams
        {
            public string Name;
            public AudioClip Sound;
            [ColorUsage(false, true)]
            public Color EmissionColor;
            public TDroidStateParams(string name, Color eColor)
            {
                Name = name;
                EmissionColor = eColor;
            }
        }


        [System.Serializable]
        public class TDroidAnimatorParams
        {
            [Header("Animator parameters")]
            public string State = "State";
            public string Moving = "Moving";
            public string ShootMode = "ShootMode";
            public string IsShooting = "IsShooting";
            public string Reloading = "Reloading";
            public string WheelL = "WheelL";
            public string WheelR = "WheelR";

            [Header("Movement")]
            [Tooltip("(360/Animation length(s)) * Animation speed")]
            public float WheelAnimAngularSpeed = 3600;
        }

        [System.Serializable]
        public class TDroidAudioParams
        {
            public AudioSource StatesAudio;
            public AudioSource MovementAudio;
            public AudioClip Reload;
            [Header("Movement audio params")]
            public AnimationCurve MoveCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1));
            public float MoveCurveScale = 0.2f;
            public AnimationCurve PitchCurve = new AnimationCurve(new Keyframe(0, 0.9f), new Keyframe(1, 1));
        }


        [System.Serializable]
        public class TDroidTransformParams
        {
            public Transform Main;
            public Transform WheelL;
            public Transform WheelR;
            public Transform WeaponL;
            public Transform WeaponR;
        }


        [System.Serializable]
        public class TDroidShootParams
        {
            public GameObject MuzzlePrefab;
            public GameObject BulletPrefab;
        }

        [System.Serializable]
        public class TDroidMovementParams
        {
            public float SpeedMin = 0.1f;
            public float SpeedMax = 3f;
            public float RotMin = 1.0f;
            public float RotMax = 45.0f;
        }


        public class TDroidMovementData
        {
            public float Speed;
            public float SpeedNorm;
            public float RotSpeed;
            public float RotSpeedNorm;
            public float WheelsSpeed;
            public float WheelsSpeedNorm;
            public bool Moving;
            public float AnimWheelL;
            public float AnimWheelR;
        }

        [Header("Initial Settings")]
        public TDroidState InitialState = TDroidState.Sleep;
        public TDroidShootMode InitialShootMode = TDroidShootMode.Single;

        [Header("Parameters")]
        public List<TDroidStateParams> StateParams = new List<TDroidStateParams>();
        public TDroidAnimatorParams AnimatorParams = new TDroidAnimatorParams();
        public TDroidAudioParams AudioParams = new TDroidAudioParams();
        public TDroidTransformParams TransformParams = new TDroidTransformParams();
        public TDroidShootParams ShootParams = new TDroidShootParams();
        public TDroidMovementParams MovementParams = new TDroidMovementParams();

        [Header("Other")]
        public float UpdateRate = 60;

        // Events
        public event Action<Transform> OnShoot;
        public event Action OnReloadFinished;

        // in game properties
        public TDroidState State { get { return _state; } set { setState(value); } }
        public TDroidShootMode ShootMode { get { return _shootMode; } set { setShootMode(value); } }
        public bool Shooting { get { return _shooting; } set { setShooting(value); } }
        public TDroidMovementData MovementData = new TDroidMovementData();
        public bool Reload { get { return _reloading; } set { setReload(value); } }

        private TDroidShootMode _shootMode;
        private bool _shooting;
        private TDroidState _state;
        private bool _reloading;
        private Animator animCtrl;
        private Material droidMat;
        // Wheels
        private Vector3 wheelLlastPos;
        private Vector3 wheelRlastPos;
        private float wheelsRadius;
        private float updateRateTimer;
        // Main transform
        private Vector3 mainLastPos;
        private Quaternion mainLastRot;



        private void Reset()
        {
            TransformParams.Main = transform;
            Color col = getDefaultMatAndColor();
            StateParams.Add(new TDroidStateParams("Sleep", col));
            StateParams.Add(new TDroidStateParams("Idle", col));
            StateParams.Add(new TDroidStateParams("Alarmed", col));
            StateParams.Add(new TDroidStateParams("Combat", col));
        }


        Color getDefaultMatAndColor()
        {
            Renderer rend = GetComponentInChildren<Renderer>();
            Color col = Color.black;
            if (rend != null)
            {
                Material mat = rend.sharedMaterial;
                if (mat.IsKeywordEnabled("_EMISSION"))
                {
                    col = mat.GetColor("_EmissionColor");
                }
            }
            return col;
        }


        private void Start()
        {
            Init();
        }

        void Init()
        {
            animCtrl = GetComponent<Animator>();
            droidMat = GetComponentInChildren<Renderer>().material;
            _state = InitialState;
            animCtrl.SetInteger(AnimatorParams.State, (int)_state);
            ShootMode = InitialShootMode;
            updateStateColor();
            // wheels movement initial data
            wheelLlastPos = TransformParams.WheelL.position;
            wheelRlastPos = TransformParams.WheelR.position;
            wheelsRadius = Mathf.Abs(TransformParams.WheelL.position.y - TransformParams.Main.position.y);
            // main body movement initial data
            mainLastPos = TransformParams.Main.position;
            mainLastRot = TransformParams.Main.rotation;
        }


        private void Update()
        {
            updateMovement();
        }

        private void setState(TDroidState value)
        {
            if (value != _state)
            {
                _state = value;
                onStateChanged();
            }
            _state = value;
        }



        void onStateChanged()
        {
            updateStateSound();
            updateStateColor();
            animCtrl.SetInteger(AnimatorParams.State, (int)_state);
        }



        void updateStateSound()
        {
            AudioClip stateSound = StateParams[(int)_state].Sound;
            if (stateSound != null && AudioParams.StatesAudio)
            {
                AudioParams.StatesAudio.PlayOneShot(stateSound);
            }
        }

        void updateStateColor()
        {
            Color col = StateParams[(int)_state].EmissionColor;
            droidMat.SetColor("_EmissionColor", col);
        }


        void setShootMode(TDroidShootMode value)
        {
            _shootMode = value;
            animCtrl.SetInteger(AnimatorParams.ShootMode, (int)_shootMode);
        }

        void setShooting(bool value)
        {
            if (!_reloading)
            {
                _shooting = value;
                animCtrl.SetBool(AnimatorParams.IsShooting, _shooting);
            }
        }

        void setReload(bool value)
        {
            if (!_reloading && !_shooting)
            {
                _reloading = true;
                animCtrl.SetTrigger(AnimatorParams.Reloading);
            }
        }

        void updateMovement()
        {
            updateRateTimer += Time.deltaTime;
            if (updateRateTimer > 1f / (UpdateRate == 0 ? 1000 : UpdateRate))
            {
                // Calculate the animation speed based on the movement of each wheel
                float speedL = CalculateWheelSpeed(TransformParams.WheelL, ref wheelLlastPos, ref MovementData.AnimWheelL, updateRateTimer);
                float speedR = CalculateWheelSpeed(TransformParams.WheelR, ref wheelRlastPos, ref MovementData.AnimWheelR, updateRateTimer);
                MovementData.WheelsSpeed = Mathf.Max(speedL, speedR);
                MovementData.WheelsSpeedNorm = Mathf.Clamp(MovementData.WheelsSpeed, 0f, MovementParams.SpeedMax) / MovementParams.SpeedMax;
                animCtrl.SetFloat(AnimatorParams.WheelL, MovementData.AnimWheelL);
                animCtrl.SetFloat(AnimatorParams.WheelR, MovementData.AnimWheelR);
                // calculate current movement speed
                calculateMovement(updateRateTimer);
                animCtrl.SetBool(AnimatorParams.Moving, MovementData.Moving);
                updateMovementSound(updateRateTimer);
                updateRateTimer = 0;
            }
        }


        public void updateMovementSound(float deltaTime)
        {
            if (AudioParams.MovementAudio)
            {
                float volumeMove = AudioParams.MoveCurve.Evaluate(MovementData.WheelsSpeedNorm) * AudioParams.MoveCurveScale;
                float pitchMove = AudioParams.PitchCurve.Evaluate(MovementData.WheelsSpeedNorm);
                // Adjust the volume and pitch based on the normalized values
                AudioParams.MovementAudio.volume = Mathf.Lerp(AudioParams.MovementAudio.volume, volumeMove, deltaTime * 5f);
                AudioParams.MovementAudio.pitch = Mathf.Lerp(AudioParams.MovementAudio.pitch, pitchMove, deltaTime * 5f);
            }
        }


        void calculateMovement(float deltaTime)
        {
            // Get the current position, rotation, and time
            Vector3 currentPosition = TransformParams.Main.position;
            Quaternion currentRotation = TransformParams.Main.rotation;
            // Calculate the distance moved since the last frame
            float distance = Vector3.Distance(currentPosition, mainLastPos);
            // Calculate the angular difference in degrees since the last frame
            float angularDifference = Quaternion.Angle(currentRotation, mainLastRot);
            // Ensure deltaTime has a minimum value to avoid division by zero
            deltaTime = Mathf.Max(deltaTime, 0.0001f);
            // Calculate linear speed (distance per second)
            MovementData.Speed = distance / deltaTime;
            MovementData.SpeedNorm = Mathf.Clamp(MovementData.Speed, 0f, MovementParams.SpeedMax) / MovementParams.SpeedMax;
            // Calculate angular speed (degrees per second)
            MovementData.RotSpeed = angularDifference / deltaTime;
            MovementData.RotSpeedNorm = Mathf.Clamp(MovementData.RotSpeed, 0f, MovementParams.RotMax) / MovementParams.RotMax;
            // Check if the object is moving or rotating based on the thresholds
            MovementData.Moving = MovementData.Speed > MovementParams.SpeedMin || MovementData.RotSpeed > MovementParams.RotMin;
            // Update the last position, rotation, and time
            mainLastPos = currentPosition;
            mainLastRot = currentRotation;
        }


        float CalculateWheelSpeed(Transform wheelTransform, ref Vector3 lastPosition, ref float animSpeed, float deltaTime)
        {
            // Calculate the linear movement of the wheel
            Vector3 movementVector = wheelTransform.position - lastPosition;
            float distance = movementVector.magnitude;
            // Avoid division by zero (e.g., on the first frame or very small deltaTime)
            if (deltaTime <= Mathf.Epsilon)
            {
                lastPosition = wheelTransform.position;
                return 0f;
            }
            // Calculate linear speed (distance traveled per second)
            float linearSpeed = distance / deltaTime;
            // Determine the direction of movement relative to the MainTransform
            float direction = Vector3.Dot(TransformParams.Main.forward, movementVector.normalized);
            // Calculate angular speed in degrees per second
            float angularSpeedInDegrees = (linearSpeed / (2 * Mathf.PI * wheelsRadius)) * 360f;
            // Normalize angular speed to match AnimatorParams.WheelMaxAngularSpeed
            float normalizedSpeed = angularSpeedInDegrees / AnimatorParams.WheelAnimAngularSpeed;
            // Update the last position and time for the next calculation
            lastPosition = wheelTransform.position;
            // Apply the direction (positive or negative)
            animSpeed = Mathf.Clamp(normalizedSpeed * Mathf.Sign(direction), -1f, 1f);
            return linearSpeed;
        }

        void MakeShot(Transform origin)
        {
            if (origin)
            {
                if (ShootParams.MuzzlePrefab)
                {
                    GameObject muzzle = Instantiate(ShootParams.MuzzlePrefab, origin.position, origin.rotation);
                }
                if (ShootParams.BulletPrefab)
                {
                    GameObject bullet = Instantiate(ShootParams.BulletPrefab, origin.position, origin.rotation);
                }
                OnShoot?.Invoke(origin);
            }
            else
            {
                Debug.LogError("Missing weapon shoot origin transform");
            }
        }


        // Animation Event
        public void AnimEventShoot(int value)
        {
            switch (value)
            {
                // Left gun
                case 0:
                    MakeShot(TransformParams.WeaponL);
                    break;
                // Right gun
                case 1:
                    MakeShot(TransformParams.WeaponR);
                    break;
                // Both guns
                case 2:
                    MakeShot(TransformParams.WeaponL);
                    MakeShot(TransformParams.WeaponR);
                    break;
                default:
                    break;
            }
        }

        public void AnimEventReloadStarted()
        {
            if (AudioParams.StatesAudio && AudioParams.Reload)
            {
                AudioParams.StatesAudio.PlayOneShot(AudioParams.Reload);
            }
        }


        public void AnimEventReloadFinished()
        {
            _reloading = false;
            OnReloadFinished?.Invoke();
        }

    }
}
