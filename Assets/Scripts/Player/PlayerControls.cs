using System;
using UnityEngine;

namespace LocalPvp.Player
{
    [Serializable]
    public struct PlayerControls
    {
        public KeyCode left;
        public KeyCode right;
        public KeyCode jump;
        public KeyCode attack;
        public KeyCode kick;
        public KeyCode dodge;

        public PlayerControls(KeyCode left, KeyCode right, KeyCode jump, KeyCode attack, KeyCode kick, KeyCode dodge)
        {
            this.left = left;
            this.right = right;
            this.jump = jump;
            this.attack = attack;
            this.kick = kick;
            this.dodge = dodge;
        }

        public float ReadHorizontal()
        {
            return (Input.GetKey(right) ? 1f : 0f) - (Input.GetKey(left) ? 1f : 0f);
        }

        public bool JumpPressed()
        {
            return Input.GetKeyDown(jump);
        }

        public bool JumpReleased() => Input.GetKeyUp(jump);

        public bool AttackPressed() => Input.GetKeyDown(attack);

        public bool KickPressed() => Input.GetKeyDown(kick);

        public bool DodgePressed() => Input.GetKeyDown(dodge)
            && !Input.GetKey(attack)
            && !Input.GetKey(kick);

        public bool DodgeHeld() => Input.GetKey(dodge);
    }
}
