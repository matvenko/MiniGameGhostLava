using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sample {
public class GhostScript : MonoBehaviour
{
    private Animator Anim;
    private CharacterController Ctrl;
    private Vector3 MoveDirection = Vector3.zero;
    // Cache hash values
    private static readonly int IdleState = Animator.StringToHash("Base Layer.idle");
    private static readonly int MoveState = Animator.StringToHash("Base Layer.move");
    private static readonly int SurprisedState = Animator.StringToHash("Base Layer.surprised");
    private static readonly int AttackState = Animator.StringToHash("Base Layer.attack_shift");
    private static readonly int DissolveState = Animator.StringToHash("Base Layer.dissolve");
    private static readonly int AttackTag = Animator.StringToHash("Attack");
    // dissolve
    [SerializeField] private SkinnedMeshRenderer[] MeshR;
    private float Dissolve_value = 1;
    private bool DissolveFlg = false;
    private const int maxHP = 3;
    private int HP = maxHP;
    private Text HP_text;
    private Vector3 spawnPosition;
    private bool isDead;

    // moving speed
    [SerializeField] private float Speed = 4;
    [SerializeField] private float deathAnimDuration = 1.2f;

    void Start()
    {
        Anim = this.GetComponent<Animator>();
        Ctrl = this.GetComponent<CharacterController>();
        var hpObj = GameObject.Find("Canvas/HP");
        if (hpObj != null) HP_text = hpObj.GetComponent<Text>();
        if (HP_text != null) HP_text.text = "HP " + HP.ToString();
        spawnPosition = transform.position;
    }

    //---------------------------------------------------------------------
    // called by LavaHazard when the character walks into lava - every
    // touch is instantly fatal and hands off to GameOverManager
    //---------------------------------------------------------------------
    public void FallIntoLava()
    {
        Die();
    }

    // called by an enemy's catch trigger - same fatal sequence as lava
    public void CaughtByEnemy()
    {
        Die();
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        Ctrl.enabled = false;
        if (GameOverManager.Instance != null)
            GameOverManager.Instance.TriggerGameOver(this);
    }

    // plays the dissolve-away animation over deathAnimDuration; the caller
    // (GameOverManager) awaits this before showing the Game Over popup
    public IEnumerator PlayDeathAnimation()
    {
        Anim.CrossFade(DissolveState, 0.1f, 0, 0);
        float t = 0f;
        while (t < deathAnimDuration)
        {
            t += Time.deltaTime;
            Dissolve_value = 1f - Mathf.Clamp01(t / deathAnimDuration);
            for (int i = 0; i < MeshR.Length; i++)
            {
                MeshR[i].material.SetFloat("_Dissolve", Dissolve_value);
            }
            yield return null;
        }
    }

    // called by GameOverManager when the player presses Continue; the Y
    // component is always taken from the original spawn height so callers
    // only need to supply a valid X/Z tile position
    public void RespawnAt(Vector3 position)
    {
        HP = maxHP;
        if (HP_text != null) HP_text.text = "HP " + HP.ToString();

        Ctrl.enabled = false;
        transform.position = new Vector3(position.x, spawnPosition.y, position.z);
        transform.rotation = Quaternion.identity;
        Ctrl.enabled = true;

        Dissolve_value = 1f;
        for (int i = 0; i < MeshR.Length; i++)
        {
            MeshR[i].material.SetFloat("_Dissolve", Dissolve_value);
        }
        DissolveFlg = false;
        Anim.CrossFade(IdleState, 0.1f, 0, 0);
        isDead = false;
    }

    void Update()
    {
        if (isDead) return;
        STATUS();
        GRAVITY();
        Respawn();
        // this character status
        if(!PlayerStatus.ContainsValue( true ))
        {
            MOVE();
            PlayerAttack();
            Damage();
        }
        else if(PlayerStatus.ContainsValue( true ))
        {
            int status_name = 0;
            foreach(var i in PlayerStatus)
            {
                if(i.Value == true)
                {
                    status_name = i.Key;
                    break;
                }
            }
            if(status_name == Dissolve)
            {
                PlayerDissolve();
            }
            else if(status_name == Attack)
            {
                PlayerAttack();
            }
            else if(status_name == Surprised)
            {
                // nothing method
            }
        }
        // Dissolve
        if(HP <= 0 && !DissolveFlg)
        {
            Anim.CrossFade(DissolveState, 0.1f, 0, 0);
            DissolveFlg = true;
        }
        // processing at respawn
        else if(HP == maxHP && DissolveFlg)
        {
            DissolveFlg = false;
        }
    }

    //---------------------------------------------------------------------
    // character status
    //---------------------------------------------------------------------
    private const int Dissolve = 1;
    private const int Attack = 2;
    private const int Surprised = 3;
    private Dictionary<int, bool> PlayerStatus = new Dictionary<int, bool>
    {
        {Dissolve, false },
        {Attack, false },
        {Surprised, false },
    };
    //------------------------------
    private void STATUS ()
    {
        // during dissolve
        if(DissolveFlg && HP <= 0)
        {
            PlayerStatus[Dissolve] = true;
        }
        else if(!DissolveFlg)
        {
            PlayerStatus[Dissolve] = false;
        }
        // during attacking
        if(Anim.GetCurrentAnimatorStateInfo(0).tagHash == AttackTag)
        {
            PlayerStatus[Attack] = true;
        }
        else if(Anim.GetCurrentAnimatorStateInfo(0).tagHash != AttackTag)
        {
            PlayerStatus[Attack] = false;
        }
        // during damaging
        if(Anim.GetCurrentAnimatorStateInfo(0).fullPathHash == SurprisedState)
        {
            PlayerStatus[Surprised] = true;
        }
        else if(Anim.GetCurrentAnimatorStateInfo(0).fullPathHash != SurprisedState)
        {
            PlayerStatus[Surprised] = false;
        }
    }
    // dissolve shading
    private void PlayerDissolve ()
    {
        Dissolve_value -= Time.deltaTime;
        for(int i = 0; i < MeshR.Length; i++)
        {
            MeshR[i].material.SetFloat("_Dissolve", Dissolve_value);
        }
        if(Dissolve_value <= 0)
        {
            Ctrl.enabled = false;
        }
    }
    // play a animation of Attack
    private void PlayerAttack ()
    {
        if(Input.GetKeyDown(KeyCode.Q))
        {
            Anim.CrossFade(AttackState,0.1f,0,0);
        }
    }
    //---------------------------------------------------------------------
    // gravity for fall of this character
    //---------------------------------------------------------------------
    private void GRAVITY ()
    {
        if(Ctrl.enabled)
        {
            if(CheckGrounded())
            {
                if(MoveDirection.y < -0.1f)
                {
                    MoveDirection.y = -0.1f;
                }
            }
            MoveDirection.y -= 0.1f;
            Ctrl.Move(MoveDirection * Time.deltaTime);
        }
    }
    //---------------------------------------------------------------------
    // whether it is grounded
    //---------------------------------------------------------------------
    private bool CheckGrounded()
    {
        if (Ctrl.isGrounded && Ctrl.enabled)
        {
            return true;
        }
        Ray ray = new Ray(this.transform.position + Vector3.up * 0.1f, Vector3.down);
        float range = 0.2f;
        return Physics.Raycast(ray, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }
    //---------------------------------------------------------------------
    // for slime moving
    //---------------------------------------------------------------------
    private void MOVE ()
    {
        // velocity - combine held keys so opposite pairs cancel out and
        // adjacent pairs (e.g. W+A) move/face diagonally. Arrow keys work
        // as aliases for WASD, and the on-screen joystick (mobile) feeds
        // into the same x/z accumulator.
        float x = 0f;
        float z = 0f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) z -= 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) z += 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x += 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x -= 1f;

        if (VirtualJoystick.Instance != null)
        {
            Vector2 joy = VirtualJoystick.Instance.InputDirection;
            x -= joy.x;
            z -= joy.y;
        }

        // gamepad left stick - separate custom axes (not the shared
        // "Horizontal"/"Vertical") so this never double-counts keyboard input
        x -= Input.GetAxis("GamepadHorizontal");
        z -= Input.GetAxis("GamepadVertical");

        if (x != 0f || z != 0f)
        {
            Vector3 dir = new Vector3(x, 0f, z).normalized;
            Vector3 rot = Quaternion.LookRotation(dir).eulerAngles;
            MOVE_Velocity(dir * Speed, rot);
        }

        KEY_DOWN();
        KEY_UP();
    }
    //---------------------------------------------------------------------
    // value for moving
    //---------------------------------------------------------------------
    private void MOVE_Velocity (Vector3 velocity, Vector3 rot)
    {
        MoveDirection = new Vector3 (velocity.x, MoveDirection.y, velocity.z);
        if(Ctrl.enabled)
        {
            Ctrl.Move(MoveDirection * Time.deltaTime);
        }
        MoveDirection.x = 0;
        MoveDirection.z = 0;
        this.transform.rotation = Quaternion.Euler(rot);
    }
    //---------------------------------------------------------------------
    // whether arrow key is key down
    //---------------------------------------------------------------------
    private void KEY_DOWN ()
    {
        if (Input.GetKeyDown(KeyCode.W))
        {
            Anim.CrossFade(MoveState, 0.1f, 0, 0);
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            Anim.CrossFade(MoveState, 0.1f, 0, 0);
        }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            Anim.CrossFade(MoveState, 0.1f, 0, 0);
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            Anim.CrossFade(MoveState, 0.1f, 0, 0);
        }
    }
    //---------------------------------------------------------------------
    // whether arrow key is key up
    //---------------------------------------------------------------------
    private void KEY_UP ()
    {
        if (Input.GetKeyUp(KeyCode.W))
        {
            if(!Input.GetKey(KeyCode.S) && !Input.GetKey(KeyCode.A) && !Input.GetKey(KeyCode.D))
            {
                Anim.CrossFade(IdleState, 0.1f, 0, 0);
            }
        }
        else if (Input.GetKeyUp(KeyCode.S))
        {
            if(!Input.GetKey(KeyCode.W) && !Input.GetKey(KeyCode.A) && !Input.GetKey(KeyCode.D))
            {
                Anim.CrossFade(IdleState, 0.1f, 0, 0);
            }
        }
        else if (Input.GetKeyUp(KeyCode.A))
        {
            if(!Input.GetKey(KeyCode.W) && !Input.GetKey(KeyCode.S) && !Input.GetKey(KeyCode.D))
            {
                Anim.CrossFade(IdleState, 0.1f, 0, 0);
            }
        }
        else if (Input.GetKeyUp(KeyCode.D))
        {
            if(!Input.GetKey(KeyCode.W) && !Input.GetKey(KeyCode.S) && !Input.GetKey(KeyCode.A))
            {
                Anim.CrossFade(IdleState, 0.1f, 0, 0);
            }
        }
    }
    //---------------------------------------------------------------------
    // damage
    //---------------------------------------------------------------------
    private void Damage ()
    {
        // Damaged by outside field.
        if(Input.GetKeyUp(KeyCode.E))
        {
            Anim.CrossFade(SurprisedState, 0.1f, 0, 0);
            HP--;
            if (HP_text != null) HP_text.text = "HP " + HP.ToString();
        }
    }
    //---------------------------------------------------------------------
    // respawn
    //---------------------------------------------------------------------
    private void Respawn ()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            // player HP
            HP = maxHP;
            
            Ctrl.enabled = false;
            this.transform.position = spawnPosition; // player position
            this.transform.rotation = Quaternion.Euler(Vector3.zero); // player facing
            Ctrl.enabled = true;
            
            // reset Dissolve
            Dissolve_value = 1;
            for(int i = 0; i < MeshR.Length; i++)
            {
                MeshR[i].material.SetFloat("_Dissolve", Dissolve_value);
            }
            // reset animation
            Anim.CrossFade(IdleState, 0.1f, 0, 0);
        }
    }
}
}