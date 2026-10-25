using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Rawr.Enhance
{
    public class FireElemental
    {
        private float _ap = 0f;
        private float _sp = 0f;
        private CombatStats _cs;
        private float _critbuffs = 0f;
        private float _petMeleeMissRate = 0f;
        private float _petMeleeMultipliers = 0f;
        private float _petSpellMissRate = 0f;
        private float _petSpellMultipliers = 0f;

        private float meleeTotaldps = 0f;
        private float novaTotaldps = 0f;
        private float blastTotaldps = 0f;
        private float shieldTotaldps = 0f;
        private float totaldps = 0f;
        private float totalMana = 0f;
        private int novaUses = 0;
        private int blastUses = 0;
        private int meleeUses = 0;
        private int shieldTicks = 0;   // Rebuffed 0.3.9

        // Rebuffed 0.3.9: per landed non-crit hit, owner sheet spell power sp at the summon (Whitemane dummy logs 14.10.,
        // two summons at sp 1071 and 2047: exact there, linear in between and beyond = indicated)
        public static float RebuffedPerHit(string kind, float sp)
        {
            if (kind == "melee") return 185.0f + 0.5958f * sp;
            if (kind == "blast") return 812.5f + 0.2083f * sp;
            if (kind == "nova") return 1078.9f + 0.5024f * sp;
            if (kind == "shield") return 95.6f + 0.01434f * sp;
            return 0f;
        }
        
        public FireElemental(float ap, float sp, float intellect, CombatStats cs, float critbuffs,
                             float petMeleeMissRate, float petMeleeMultipliers, float petSpellMissRate, float petSpellMultipliers)
        {
            _ap = ap; 
            _sp = sp;
            _cs = cs;
            _petMeleeMissRate = petMeleeMissRate;
            _petMeleeMultipliers = petMeleeMultipliers;
            _petSpellMissRate = petSpellMissRate;
            _petSpellMultipliers = petSpellMultipliers;

            meleeTotaldps = 0f;
            novaTotaldps = 0f;
            blastTotaldps = 0f;
            shieldTotaldps = 0f;
            totaldps = 0f;
            totalMana = 4000 + 15 * (0.3f * intellect);
            CalculateUses();
            CalculateDamage();
        }

        public float getDPS()
        {
            return totaldps;
        }

        public string getDPSOutput()
        {
            return string.Format("{0}*{1} Melee dps\n{2} Fire Blast dps\n{3} Fire Nova dps\n{4} Fire Shield dps",
                totaldps.ToString("F2", CultureInfo.InvariantCulture), 
                meleeTotaldps.ToString("F2", CultureInfo.InvariantCulture),
                blastTotaldps.ToString("F2", CultureInfo.InvariantCulture),
                novaTotaldps.ToString("F2", CultureInfo.InvariantCulture),
                shieldTotaldps.ToString("F2", CultureInfo.InvariantCulture));
        }

        private void CalculateDamage()
        {
            float meleeBasedps = RebuffedPerHit("melee", _sp) * meleeUses / 120f;   // Rebuffed 0.3.9: log values; glancing as for any melee
            float blastBasedps = RebuffedPerHit("blast", _sp) * blastUses / 120f;
            float novaBasedps = RebuffedPerHit("nova", _sp) * novaUses / 120f;
            float shieldBasedps = RebuffedPerHit("shield", _sp) * shieldTicks / 120f;
            // crit (raid log: melee 9.0 % x2.12, spells 9.9-10.6 % x1.53; dummy without debuffs: melee 3.8 %, spells ~6 %):
            // melee 4 %, spells 6 %, plus the caller's crit debuff; x2 melee, x1.5 spells (EnhSim-Rebuffed 0.3.7 the same)
            float meleeCrit = 0.04f + _critbuffs, spellCrit = 0.06f + _critbuffs;

            float meleeNormal = meleeBasedps * (1 - _petMeleeMissRate - meleeCrit - _cs.GlancingRate);
            float meleeCrits = meleeBasedps * meleeCrit * 2f;
            float meleeGlances = meleeBasedps * _cs.GlancingHitModifier;
            meleeTotaldps = (meleeNormal + meleeCrits + meleeGlances) * _petMeleeMultipliers * _cs.FireElementalUptime;

            float blastNormal = blastBasedps * (1 - _petSpellMissRate - spellCrit);
            float blastCrits = blastBasedps * spellCrit * 1.5f;
            blastTotaldps = (blastNormal + blastCrits) * _petSpellMultipliers * _cs.FireElementalUptime;

            float novaNormal = novaBasedps * (1 - _petSpellMissRate - spellCrit);
            float novaCrits = novaBasedps * spellCrit * 1.5f;
            novaTotaldps = (novaNormal + novaCrits) * _petSpellMultipliers * _cs.FireElementalUptime * _cs.MultiTargetMultiplier;

            float shieldNormal = shieldBasedps * (1 - _petSpellMissRate - spellCrit);
            float shieldCrits = shieldBasedps * spellCrit * 1.5f;
            shieldTotaldps = (shieldNormal + shieldCrits) * _petSpellMultipliers * _cs.FireElementalUptime * _cs.MultiTargetMultiplier;

            totaldps = meleeTotaldps + novaTotaldps + blastTotaldps + shieldTotaldps;
        }

        private void CalculateUses()
        {
            // Rebuffed 0.3.9: measured per life on Whitemane (dummies 14.10.): Fire Nova
            // and Fire Blast every 7.04 s = 14 each, melee 2.1 s swing with a gap per Nova cast = 37-39, Fire Shield every 3.0 s
            // = 38-40; mana never ran out. The 2.3.23 round robin below is no longer used.
            novaUses = 14; blastUses = 14; meleeUses = 38; shieldTicks = 40;
            if (novaUses > 0) return;
            float time = 2f; // he tends to take a couple of seconds to get in range and start
            int sequence = 1;
            novaUses = 0;
            blastUses = 0;
            meleeUses = 0;
            while (totalMana > 0 && time < 120f)
            {
                switch (sequence)
                {
                    case (int)AbilitySequence.nova:
                        if (totalMana > 207)
                        {
                            novaUses++;
                            totalMana -= 207;
                            time += 2.5f;
                        }
                        break;
                    case (int)AbilitySequence.blast:
                        if (totalMana > 276)
                        {
                            blastUses++;
                            totalMana -= 276;
                            time += 4f;
                        }
                        break;
                    case (int)AbilitySequence.melee:
                        meleeUses++;
                        time += 2f;
                        break;
                }
                sequence++;
                if (sequence == 4) sequence = 1;
            }
        }

        private enum AbilitySequence
        {
            nova = 1,
            blast = 2,
            melee = 3,
        }
    }
}
