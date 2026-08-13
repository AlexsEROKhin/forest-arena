using LocalPvp.Player;

namespace LocalPvp.Items
{
    public interface IAttackReceiver
    {
        bool ReceiveAttack(int attackId, AttackType attackType, PlayerCombat attacker);
    }
}
