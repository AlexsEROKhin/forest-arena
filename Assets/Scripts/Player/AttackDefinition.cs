using System;
using UnityEngine;

namespace LocalPvp.Player
{
    public enum AttackType
    {
        Basic,
        Air,
        Dash,
        Kick
    }

    [Serializable]
    public sealed class AttackDefinition
    {
        public AttackType type;
        public float damageMultiplier = 1f;
        public float rangeMultiplier = 1f;
        public float height = 1.1f;
        public float forwardOffset = 0.5f;
        public float verticalOffset;
        public float horizontalKnockback = 1f;
        public float verticalKnockback = 0.45f;
        public float extraCooldown;
        public bool strongFeedback;
        public bool aerialBounce;
        public Color telegraphColor;
        public Color activeColor;

        public static AttackDefinition Basic() => new AttackDefinition
        {
            type = AttackType.Basic,
            telegraphColor = new Color(1f, 0.85f, 0.15f, 0.45f),
            activeColor = new Color(1f, 0.2f, 0.1f, 0.8f)
        };

        public static AttackDefinition Air() => new AttackDefinition
        {
            type = AttackType.Air,
            damageMultiplier = 1.4f,
            rangeMultiplier = 1.1f,
            height = 1.35f,
            verticalOffset = -0.45f,
            horizontalKnockback = 0.65f,
            verticalKnockback = -0.7f,
            aerialBounce = true,
            telegraphColor = new Color(0.25f, 0.75f, 1f, 0.5f),
            activeColor = new Color(0.8f, 0.15f, 1f, 0.8f)
        };

        public static AttackDefinition Dash() => new AttackDefinition
        {
            type = AttackType.Dash,
            damageMultiplier = 1.35f,
            rangeMultiplier = 2f,
            forwardOffset = 0.85f,
            horizontalKnockback = 1.5f,
            extraCooldown = 0.12f,
            strongFeedback = true,
            telegraphColor = new Color(1f, 0.5f, 0.1f, 0.55f),
            activeColor = new Color(1f, 0.05f, 0.05f, 0.9f)
        };

        public static AttackDefinition Kick() => new AttackDefinition
        {
            type = AttackType.Kick,
            damageMultiplier = 0.8f,
            rangeMultiplier = 0.82f,
            height = 0.65f,
            forwardOffset = 0.48f,
            verticalOffset = -0.28f,
            horizontalKnockback = 1.65f,
            verticalKnockback = 0.18f,
            telegraphColor = new Color(0.25f, 1f, 0.55f, 0.45f),
            activeColor = new Color(0.1f, 1f, 0.35f, 0.85f)
        };
    }
}
