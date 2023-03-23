using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMotion : MonoBehaviour
{
    private class PlayerMotionInputs
    {
        private int _direction;
        private bool _jump;
        private bool _jumpHold;

        public int Direction { get => _direction; set => _direction = value; }
        public bool Jump { get => _jump; set => _jump = value; }
        public bool JumpHolding { get => _jumpHold; set => _jumpHold = value; }
    }

    private PlayerMotionInputs m_inputs = new();

    #region References

    private Rigidbody2D m_rigidbody;
    private BoxCollider2D m_collider;

    #endregion

    #region Running

    [Space(10)]
    [Header("Running")]
    [SerializeField] private float m_runningSpeed;

    [Space(10)]
    [Range(.1f, 10f)][SerializeField] private float m_runningSpeedAcceleration;
    [Range(.1f, 10f)][SerializeField] private float m_runningSpeedDeceleration;
    [Range(.1f, 10f)][SerializeField] private float m_runningSpeedTurn;

    #endregion

    #region Jumping

    [Space(10)]
    [Header("Jumping")]
    [SerializeField] private float m_jumpHeight;

    [Space(10)]
    [SerializeField] private int m_maxJumpsNumber = 1;
    private int m_jumpsNumber;

    [Space(10)]
    [SerializeField] private float m_gravityScale;
    [SerializeField] private float m_downGravityScale;
    private float m_currentGravity;
    [SerializeField] private float m_terminalVelocity = -10000f;

    [Space(10)]
    [Range(.1f, 10f)][SerializeField] private float m_onAirSpeedAcceleration;
    [Range(.1f, 10f)][SerializeField] private float m_onAirSpeedControl;
    [Range(0f, 5f)][SerializeField] private float m_variableJumpCutOff;
    private bool variableJumpDragDownStart = false;
    private bool m_isJumping = false;
    private bool m_canJump = true;

    [Range(0f, .25f)][SerializeField] private float m_coyoteTime;
    private float m_coyoteTimeCounter = 0f;
    [Range(0f, .25f)][SerializeField] private float m_jumpBufferTime;
    private float m_bufferTimeCounter = 0f;
    private bool m_earlyJumpInput = false;

    #endregion

    #region GroundCheck

    [Space(10)]
    [Header("Ground Check")]
    [SerializeField] private bool m_onGround;
    [SerializeField] private LayerMask m_groundLayerMask;

    #endregion

    private Vector2 m_desiredVelocity;
    public Vector2 DesiredVelocity { get => m_desiredVelocity; }

    private void Start()
    {
        m_rigidbody = GetComponent<Rigidbody2D>();
        m_collider = GetComponent<BoxCollider2D>();
    }

    private void Update()
    {
        OnGroundCheck();

        JumpingCalculation();
        MovementCalculation();
    }

    private void FixedUpdate()
    {
        GravityCalculation();

        SetVelocity();
    }

    // Check if player is on ground
    private void OnGroundCheck()
    {
        float _offset = Physics2D.defaultContactOffset;
        float _rayLength = _offset;

        float _rayOriginOffsetX = m_collider.size.x / 2 - _offset;
        float _rayOriginOffsetY = m_collider.size.y / 2 + _offset;

        Vector3 _midRayOrigin = transform.position + _rayOriginOffsetY * Vector3.down;
        Vector3 _leftRayOrigin = transform.position + _rayOriginOffsetX * Vector3.left + _rayOriginOffsetY * Vector3.down;
        Vector3 _rightRayOrigin = transform.position + _rayOriginOffsetX * Vector3.right + _rayOriginOffsetY * Vector3.down;

        RaycastHit2D _midHit = Physics2D.Raycast(_midRayOrigin, Vector3.down, _rayLength, m_groundLayerMask);
        RaycastHit2D _leftHit = Physics2D.Raycast(_leftRayOrigin, Vector3.down, _rayLength, m_groundLayerMask);
        RaycastHit2D _rightHit = Physics2D.Raycast(_rightRayOrigin, Vector3.down, _rayLength, m_groundLayerMask);

        m_onGround = (_midHit.collider != null || _leftHit.collider != null || _rightHit.collider != null);
    }

    // All calculation on moving 
    private void MovementCalculation()
    {
        m_desiredVelocity.x = m_inputs.Direction * m_runningSpeed;
    }

    // All calculation on jumping 
    private void JumpingCalculation()
    {
        // Coyote 
        if (OnGround) m_coyoteTimeCounter = m_coyoteTime;
        else m_coyoteTimeCounter -= Time.deltaTime;

        // Jump buffer
        if (!m_earlyJumpInput) m_bufferTimeCounter = m_jumpBufferTime;
        else m_bufferTimeCounter -= Time.deltaTime;

        if (!m_isJumping) m_canJump = (m_coyoteTimeCounter >= 0f);

        // Reset when touch ground
        if (m_isJumping && OnGround && m_desiredVelocity.y <= 0f)
        {
            m_isJumping = false;
            m_jumpsNumber = 0;
        }

        // Jump input
        if (m_inputs.Jump)
        {
            if (m_jumpsNumber >= m_maxJumpsNumber) m_canJump = false;

            if (m_canJump)
            {
                Jump(m_jumpHeight);
            }
            else
            {
                m_earlyJumpInput = true;
            }
        }

        // Drag down when not hold jump
        if (m_isJumping)
        {
            if (!m_inputs.JumpHolding && m_desiredVelocity.y > 0f && !variableJumpDragDownStart) variableJumpDragDownStart = true;
            if (variableJumpDragDownStart)
            {
                float _gravity = m_variableJumpCutOff * m_gravityScale * Physics2D.gravity.y;

                m_desiredVelocity.y += _gravity * Time.deltaTime;
            }
        }
        else
        {
            variableJumpDragDownStart = false;
        }

        // Jump buffer calculation
        if (m_earlyJumpInput)
        {
            if (m_canJump)
            {
                if (m_bufferTimeCounter >= 0f)
                {
                    Jump(m_jumpHeight);
                }

                m_earlyJumpInput = false;
            }
        }
    }

    // The changing of velocity 
    private float MovementVelocityChangeSpeed()
    {
        float _movementAcceleration = (OnGround ? m_runningSpeedAcceleration : m_onAirSpeedAcceleration) * m_runningSpeed;
        float _movementDeceleration = (OnGround ? m_runningSpeedDeceleration : m_onAirSpeedControl) * m_runningSpeed;
        float _movementTurnSpeed = ((OnGround ? m_runningSpeedTurn : m_onAirSpeedControl) * 2f) * m_runningSpeed;

        float _result = 0f;
        if (m_inputs.Direction != 0)
        {
            if (Mathf.Sign(m_inputs.Direction) != Mathf.Sign(m_desiredVelocity.x))
            {
                _result = _movementTurnSpeed;
            }
            else
            {
                _result = _movementAcceleration;
            }
        }
        else
        {
            _result = _movementDeceleration;
        }

        return _result * Time.deltaTime;
    }

    // Gravity
    private void GravityCalculation()
    {
        if (!OnGround && m_desiredVelocity.y < 0)
        {
            m_currentGravity = Physics2D.gravity.y * m_downGravityScale;
        }
        else
        {
            m_currentGravity = Physics2D.gravity.y * m_gravityScale;
        }

        m_desiredVelocity.y += m_currentGravity * Time.deltaTime;
        if (m_desiredVelocity.y < 0f && OnGround) m_desiredVelocity.y = 0f;
    }

    // Set desired velocity to rigidbody
    private void SetVelocity()
    {
        Vector2 _velocity = m_desiredVelocity;

        float velocityChangeSpeed = MovementVelocityChangeSpeed();
        _velocity.x = Mathf.MoveTowards(_velocity.x, m_desiredVelocity.x, velocityChangeSpeed);
        _velocity.y = Mathf.Clamp(m_desiredVelocity.y, m_terminalVelocity, Mathf.Infinity);

        m_rigidbody.velocity = _velocity;
    }

    // Public Method
    #region Public Method

    public bool OnGround { get => m_onGround; }

    // Call continuous on Update()
    public void Move(int _direction)
    {
        m_inputs.Direction = _direction;
    }

    // Call continuous on Update()
    public void Jump(bool _instanceInput, bool _holdInput)
    {
        m_inputs.Jump = _instanceInput;
        m_inputs.JumpHolding = _holdInput;
    }

    // Force player to jump in certain height
    public void Jump(float _jumpForce)
    {
        m_jumpsNumber++;
        m_isJumping = true;

        m_desiredVelocity.y = _jumpForce;
    }

    public void AddForce(Vector2 _force)
    {
        m_desiredVelocity += _force;
    }

    #endregion
}
