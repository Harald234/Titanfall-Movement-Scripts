using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EZCameraShake;

[DisallowMultipleComponent]
public class TitanfallMovement : MonoBehaviour
{

    #region Inspector Variables
   
    [Header("Debug")]
    [SerializeField] float speed;
    [SerializeField] float gravity;
    [SerializeField] int jumpCharges;
    [SerializeField] float slideTimer;
    [SerializeField] Vector3 move;
    [SerializeField] Vector3 input;
    [SerializeField] Vector3 Yvelocity;
    [SerializeField] Vector3 forwardDirection;

    [Header("Grounded Movement Config")]
    public float runSpeed;
    public float sprintSpeed;
    public float crouchSpeed;
    public float velocityAdjustSpeed;

    [Header("Jump Config")]
    public LayerMask groundMask;
    public Transform groundCheck;
    [Space]
    public int maxJumpCharges;
    public float normalGravity;
    public float jumpHeight;

    [Header("Slide Config")]
    public float slideSpeedIncrease;
    public float slideSpeedDecrease;

    float startHeight;
    float crouchHeight = 0.5f;

    public float maxSlideTimer;


    [Header("Air Movement Config")]
    public float airSpeedMultiplier;

    [Header("Climb Config")]
    public float climbSpeed;
    public float maxClimbTimer;
    public float sphereCastRadius;

    [Header("Mantle Config")]
    public float mantleHeightOffset;


    [Header("Wall Run Config")]
    public LayerMask wallMask;
    [Space]
    public float wallRunGravity;
    public float maxWallJumpTimer;
    [Space]
    public float wallRunSpeedIncrease;
    public float wallRunSpeedDecrease;

    [Header("Camera Config")]
    public Camera playerCamera;
    [Space]
    public float specialFov;
    public float cameraChangeTime;
    [Space]
    public float wallRunTilt;
    public float tilt;

    [Header("Animation Config")]
    public Animator handAnimator;

    [Header("Keybind Config")]
    public KeyCode forwardKey = KeyCode.W;
    public KeyCode backwardKey = KeyCode.S;
    public KeyCode leftKey = KeyCode.A;
    public KeyCode rightKey = KeyCode.D;
    [Space]
    public KeyCode sprintKey = KeyCode.LeftShift;
    [Space]
    public KeyCode jumpKey = KeyCode.Space;
    [Space]
    public KeyCode crouchKey = KeyCode.LeftControl;

    #endregion

    #region Private Variables
    //Private

    //Movement
    CharacterController controller;

    //Movement States
    bool isSprinting;
    bool isCrouching;
    bool isSliding;
    bool isWallRunning;
    bool isGrounded;

    //Crouching
    Vector3 crouchingCenter = new Vector3(0, 0.5f, 0);
    Vector3 standingCenter = new Vector3(0, 0, 0);

    //Wallrunning
    bool onLeftWall;
    bool onRightWall;
    bool hasWallRun = false;
    private RaycastHit leftWallHit;
    private RaycastHit rightWallHit;
    Vector3 wallNormal;
    Vector3 lastWall;

    //Wall-jump
    bool isWallJumping;
    float wallJumpTimer;

    //Climbing
    bool isClimbing;
    bool hasClimbed;
    bool canClimb;
    private RaycastHit wallHit;
    float climbTimer;

    //Mantle
    public RaycastHit mantleTop;
    public RaycastHit mantleBottom;

    //Camera
    float normalFov;
    CameraShakeInstance currentShake;

    #endregion

    #region Start
    void Start()
    {
        //Set bools variables and get references
        controller = GetComponent<CharacterController>();

        startHeight = transform.localScale.y;

        jumpCharges = maxJumpCharges;

        normalFov = playerCamera.fieldOfView;

        currentShake = EZCameraShake.CameraShaker.Instance.StartShake(.001f, .001f, .001f);

    }

    #endregion

    #region Speed Control
    void IncreaseSpeed(float speedIncrease)
    {
        speed += speedIncrease * Time.deltaTime;
    }

    void DecreaseSpeed(float speedDecrease)
    {
        speed -= speedDecrease * Time.deltaTime;
    }
    void SetSpeedSmooth(float newSpeed)
    {
        speed = Mathf.Lerp(speed, newSpeed, Time.deltaTime * velocityAdjustSpeed);
    }
    #endregion

    #region Update
    void Update()
    {
        HandleInput();
        CheckWallRun();

        if (!isGrounded) 
        {
            CheckClimbing();
        }


        if (isGrounded && !isSliding)
        {
            GroundedMovement();
        }
        else if (!isGrounded && !isWallRunning && !isClimbing)
        {
            AirMovement();
        }
        else if (isSliding)
        {
            SlideMovement();
            DecreaseSpeed(slideSpeedDecrease);
            slideTimer -= 1f * Time.deltaTime;
            if (slideTimer <= 0)
            {
                isSliding = false;
            }
        }
        else if (isWallRunning)
        {
            WallRunMovement();
            DecreaseSpeed(wallRunSpeedDecrease);

        }
        else if (isClimbing)
        {
            ClimbMovement();
            climbTimer -= 1f * Time.deltaTime;
            if (climbTimer < 0)
            {
                isClimbing = false;
                hasClimbed = true;
            }
        }

        CameraEffects();
        UpdateAnims();

    }

    void FixedUpdate()
    {
        CheckGround();
        controller.Move(move);
        ApplyGravity();
    }

    #endregion

    #region Camera
    void CameraEffects()
    {
        float fov = isWallRunning ? specialFov : isSliding ? specialFov : normalFov;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, fov, cameraChangeTime * Time.deltaTime);

        if (isWallRunning)
        {
            if (onRightWall)
            {
                tilt = Mathf.Lerp(tilt, wallRunTilt, cameraChangeTime * Time.deltaTime);
            }
            else if (onLeftWall)
            {
                tilt = Mathf.Lerp(tilt, -wallRunTilt, cameraChangeTime * Time.deltaTime);
            }
        }
        else
        {
            tilt = Mathf.Lerp(tilt, 0f, cameraChangeTime * Time.deltaTime);
        }
    }
    #endregion

    #region Input
    void HandleInput()
    {
        bool wasIdle = input.z == 0 && input.x == 0;
        input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        input.x = 0;
        input.z = 0;
        //Pressing Forward Key
        if (Input.GetKey(forwardKey) && !Input.GetKey(backwardKey)) input.z = 1;
        //Pressing Backward Key
        if (Input.GetKey(backwardKey) && !Input.GetKey(forwardKey)) input.z = -1;
        //Pressing Left Key
        if (Input.GetKey(leftKey) && !Input.GetKey(rightKey)) input.x = -1;
        //Pressing Right Key
        if (Input.GetKey(rightKey) && !Input.GetKey(leftKey)) input.x = 1;
        #endregion

        //If player is standing still remove all shake
        if (!wasIdle && (input.z == 0 && input.x == 0))
        {
            currentShake.StartFadeOut(.05f);
        }
        //If player is beginning to run apply run shake
        if ((input.z != 0 || input.x != 0) && wasIdle)
        {
            if (!isCrouching && !isSprinting && isGrounded)
            {

                currentShake.StartFadeOut(.0001f);
                currentShake = EZCameraShake.CameraShaker.Instance.StartShake(.2f, .6f, .001f);
                //EZCameraShake.CameraShaker.Instance.


            }
        }
        if (!isWallRunning)
        {
            input = transform.TransformDirection(input);
            input = Vector3.ClampMagnitude(input, 1f);
        }


        if (Input.GetKey(crouchKey) && isGrounded)
        {
            Crouch();
        }
        if (Input.GetKeyUp(crouchKey))
        {
            ExitCrouch();
        }

        if (Input.GetKeyDown(sprintKey) && isGrounded && !isCrouching)
        {
            currentShake.StartFadeOut(.1f);
            currentShake = EZCameraShake.CameraShaker.Instance.StartShake(.4f, .55f, .001f);


            isSprinting = true;
        }
        if (Input.GetKeyUp(sprintKey))
        {
            isSprinting = false;
        }

        if (Input.GetKeyDown(jumpKey) && jumpCharges > 0)
        {
            Jump();
        }
        //Mantle

        //Check for mantle
        Vector3 mantleCheckPos = new Vector3(transform.position.x, transform.position.y + mantleHeightOffset, transform.position.z);
        Vector3 mantleCheckPosTop = new Vector3(transform.position.x, transform.position.y + .1f, transform.position.z);

        if (!Physics.SphereCast(mantleCheckPos, sphereCastRadius, transform.forward, out mantleTop, 0.8f) && Physics.SphereCast(mantleCheckPosTop, sphereCastRadius, transform.forward, out mantleTop, 0.8f))
        {
            Mantle();
        }
        
    }
    #endregion

    #region Mantle
    void Mantle()
    {
        //Stop climbing if we are
        isClimbing = false;
        climbTimer = 0;
        //Do mantle
        Jump(1.5f);
        //Play animation
        handAnimator.Play("Mantle");
        //Apply camera shake
        EZCameraShake.CameraShaker.Instance.ShakeOnce(2f, .5f, .1f, .1f);

    }
    #endregion

    #region Animations
    void UpdateAnims()
    {

        //Check Animations
        handAnimator.SetBool("Grounded", isGrounded);

        //General Movement
        handAnimator.SetFloat("MoveDirectionX", move.x);
        handAnimator.SetFloat("MoveDirectionY", move.y);

        handAnimator.SetFloat("Speed", speed);
        if (input.z == 0 && input.x == 0) handAnimator.SetFloat("Speed", 0f);

        //Ground Movement
        handAnimator.SetBool("Sprinting", isSprinting);
        handAnimator.SetBool("Crouching", isCrouching);
        //Special Movement
        handAnimator.SetBool("Climbing", isClimbing);

        if (isWallRunning)
        {
            handAnimator.SetBool("Wall Left", onLeftWall);
            handAnimator.SetBool("Wall Right", onRightWall);
        }
        else
        {
            handAnimator.SetBool("Wall Left", false);
            handAnimator.SetBool("Wall Right", false);
        }

        //Keep in mind the mantle animation is played in the Mantle() function!



    }
    #endregion

    #region Player Movement

    void CheckGround()
    {
        bool inAir = !isGrounded;
        isGrounded = Physics.CheckSphere(groundCheck.position, 0.25f, groundMask);
        //If we just hit the floor set our y velocity to zero
        if (isGrounded && inAir) Yvelocity.y = 0;
        if (isGrounded)
        {
            jumpCharges = maxJumpCharges;
            hasWallRun = false;
            hasClimbed = false;
            climbTimer = maxClimbTimer;
        }
    }

    void CheckWallRun()
    {

        onRightWall = Physics.Raycast(transform.position, transform.right, out rightWallHit, 0.7f, wallMask);
        onLeftWall = Physics.Raycast(transform.position, -transform.right, out leftWallHit, 0.7f, wallMask);
        if (!isGrounded)
        {
            if ((onRightWall || onLeftWall) && !isWallRunning)
            {
                TestWallRun();
            }
            else if (!onRightWall && !onLeftWall && isWallRunning || isGrounded && isWallRunning)
            {
                ExitWallRun();
            }
        }


    }

    void CheckClimbing()
    {
        canClimb = Physics.SphereCast(transform.position, sphereCastRadius, transform.forward, out wallHit, 0.7f, wallMask);
        float wallAngle = Vector3.Angle(-wallHit.normal, transform.forward);
        if (wallAngle < 15 && canClimb && !hasClimbed && Input.GetKey(jumpKey))
        {
            isClimbing = true;
        }
        else
        {
            isClimbing = false;
        }
    }

    void GroundedMovement()
    {
        //speed = isSprinting ? sprintSpeed : isCrouching ? crouchSpeed : runSpeed;
        if (isSprinting) 
        {
            SetSpeedSmooth(sprintSpeed);
        } 
        else if (isCrouching) 
        {
            SetSpeedSmooth(crouchSpeed);
        } 
        else 
        {
            SetSpeedSmooth(runSpeed);


        }

        if (input.x != 0)
        {
            move.x += input.x * speed;
        }
        else
        {
            move.x = 0;
        }
        if (input.z != 0)
        {
            move.z += input.z * speed;
        }
        else
        {
            move.z = 0;
        }

        move = Vector3.ClampMagnitude(move, speed);
    }

    void AirMovement()
    {
        move.x += input.x * airSpeedMultiplier;
        move.z += input.z * airSpeedMultiplier;
        if (isWallJumping)
        {
            move += forwardDirection * airSpeedMultiplier;
            wallJumpTimer -= 1f * Time.deltaTime;
            if (wallJumpTimer <= 0)
            {
                isWallJumping = false;
            }
        }

        move = Vector3.ClampMagnitude(move, speed);
    }

    void SlideMovement()
    {
        move += forwardDirection;
        move = Vector3.ClampMagnitude(move, speed);
    }

    void WallRunMovement()
    {
        if (input.z > (forwardDirection.z - 10f) && input.z < (forwardDirection.z + 10f))
        {
            move += forwardDirection;
        }
        else if (input.z < (forwardDirection.z - 10f) && input.z > (forwardDirection.z + 10f))
        {
            move.x = 0;
            move.z = 0;
            ExitWallRun();
        }
        move.x += input.x * airSpeedMultiplier;

        move = Vector3.ClampMagnitude(move, speed);
    }

    void ClimbMovement()
    {
        forwardDirection = Vector3.up;
        move.x += input.x * airSpeedMultiplier;
        move.z += input.z * airSpeedMultiplier;

        Yvelocity += forwardDirection;
        speed = climbSpeed;

        move = Vector3.ClampMagnitude(move, speed);
        Yvelocity = Vector3.ClampMagnitude(Yvelocity, speed);



    }
    #endregion

    #region Crouch
    void Crouch()
    {
        if (!isCrouching)
        {
            currentShake.StartFadeOut(.0001f);
            currentShake = EZCameraShake.CameraShaker.Instance.StartShake(.2f, .2f, .001f);

        }
        //Bug that would allow player to sprint while crouched
        isSprinting = false;

        controller.height = crouchHeight;
        controller.center = crouchingCenter;
        transform.localScale = new Vector3(transform.localScale.x, crouchHeight, transform.localScale.z);
        if (speed > runSpeed)
        {
            isSliding = true;
            forwardDirection = transform.forward;
            if (isGrounded)
            {
                IncreaseSpeed(slideSpeedIncrease);
            }
            slideTimer = maxSlideTimer;
        }
        isCrouching = true;
    }

    void ExitCrouch()
    {
        controller.height = (startHeight * 2);
        controller.center = standingCenter;
        transform.localScale = new Vector3(transform.localScale.x, startHeight, transform.localScale.z);
        isCrouching = false;
        isSliding = false;
    }
    #endregion

    #region Wallrun
    void TestWallRun()
    {
        wallNormal = onRightWall ? rightWallHit.normal : leftWallHit.normal;
        if (hasWallRun)
        {
            float wallAngle = Vector3.Angle(wallNormal, lastWall);
            if (wallAngle > 15)
            {
                WallRun();
            }
        }
        else
        {
            hasWallRun = true;
            WallRun();
        }
    }

    void WallRun()
    {
        isWallRunning = true;
        jumpCharges = maxJumpCharges;
        IncreaseSpeed(wallRunSpeedIncrease);
        Yvelocity = new Vector3(0f, 0f, 0f);

        forwardDirection = Vector3.Cross(wallNormal, Vector3.up);

        if (Vector3.Dot(forwardDirection, transform.forward) < 0)
        {
            forwardDirection = -forwardDirection;
        }
    }

    void ExitWallRun()
    {
        if (isWallRunning) IncreaseSpeed(wallRunSpeedIncrease);
        isWallRunning = false;
        lastWall = wallNormal;
        forwardDirection = wallNormal;
        isWallJumping = true;
        wallJumpTimer = maxWallJumpTimer;
       
    }
    #endregion

    #region Jump
    void Jump()
    {
        if (!isWallRunning && !isClimbing)
        {
            jumpCharges -= 1;
        }
        else if (isWallRunning)
        {
            ExitWallRun();
        }
        hasClimbed = false;
        climbTimer = maxClimbTimer;
        Yvelocity.y = Mathf.Sqrt(jumpHeight * -2f * normalGravity);
    }
    void Jump(float jumpMultiplier)
    {
        if (!isWallRunning && !isClimbing)
        {
            jumpCharges -= 1;
        }
        else if (isWallRunning)
        {
            ExitWallRun();
        }
        hasClimbed = false;
        climbTimer = maxClimbTimer;
        Yvelocity.y = Mathf.Sqrt(jumpHeight * jumpMultiplier * -2f * normalGravity);
    }
    #endregion

    #region Gravity
    void ApplyGravity()
    {
        gravity = isWallRunning ? wallRunGravity : isClimbing ? 0f : normalGravity;
        if (!isGrounded)
        {
            Yvelocity.y += gravity;

        }
        
        controller.Move(Yvelocity);
    }
    #endregion
}

